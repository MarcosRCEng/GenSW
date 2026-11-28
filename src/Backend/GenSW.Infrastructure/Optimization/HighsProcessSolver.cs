using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using GenSW.Domain.Optimization;

[assembly: InternalsVisibleTo("GenSW.Infrastructure.Tests")]

namespace GenSW.Infrastructure.Optimization;

public sealed class HighsProcessSolver : IOptimizationSolver
{
    private const string WorkerAssembly = "GenSW.OptimizationWorker.dll";
    private readonly TimeSpan watchdog;
    private readonly bool useSandbox;

    public HighsProcessSolver() : this(TimeSpan.FromSeconds(10), useSandbox: true) { }

    internal HighsProcessSolver(TimeSpan watchdog, bool useSandbox = false)
    {
        this.watchdog = watchdog;
        this.useSandbox = useSandbox;
    }

    public async Task<OptimizationSolverResult> SolveAsync(OptimizationNativeModel model, TimeSpan budget, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var worker = Path.Combine(AppContext.BaseDirectory, WorkerAssembly);
        var host = FindDotnetHost();
        if (!File.Exists(worker) || host is null)
            return new(OptimizationNativeStatus.Error, "WorkerNativoIndisponivel");

        var startInfo = useSandbox ? SandboxedStartInfo(host) : DirectTestStartInfo(host, worker);
        if (startInfo is null)
            return new(OptimizationNativeStatus.Error, "IsolamentoWorkerIndisponivel");

        using var process = new Process { StartInfo = startInfo };
        using var timeout = new CancellationTokenSource(watchdog);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var started = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            started = process.Start();
            if (!started)
                return new(OptimizationNativeStatus.Error, "FalhaInicioWorker");

            var discardErrors = process.StandardError.BaseStream.CopyToAsync(Stream.Null, execution.Token);
            var remaining = budget - Stopwatch.GetElapsedTime(startedAt);
            if (remaining <= TimeSpan.Zero)
                return new(OptimizationNativeStatus.TimeLimit, "OrcamentoEsgotado");

            await JsonSerializer.SerializeAsync(process.StandardInput.BaseStream, new OptimizationSolverRequest(model, remaining.TotalSeconds),
                cancellationToken: execution.Token);
            await process.StandardInput.BaseStream.FlushAsync(execution.Token);
            process.StandardInput.Close();

            var result = await JsonSerializer.DeserializeAsync<OptimizationSolverResult>(process.StandardOutput.BaseStream,
                cancellationToken: execution.Token);
            await process.WaitForExitAsync(execution.Token);
            await discardErrors;
            return process.ExitCode == 0 && result is not null
                ? result
                : new(OptimizationNativeStatus.Error, "FalhaWorkerNativo");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new(OptimizationNativeStatus.TimeLimit, "WatchdogExcedido");
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or JsonException or InvalidOperationException)
        {
            return new(OptimizationNativeStatus.Error, "FalhaWorkerNativo");
        }
        finally
        {
            if (started)
                await StopAsync(process);
        }
    }

    private static ProcessStartInfo? SandboxedStartInfo(string host)
    {
        const string sandbox = "/usr/bin/bwrap";
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64 || !File.Exists(sandbox))
            return null;

        var dotnetDirectory = Path.GetDirectoryName(host)!;
        if (!Directory.Exists(Path.Combine(dotnetDirectory, "host")) ||
            !Directory.Exists(Path.Combine(dotnetDirectory, "shared", "Microsoft.NETCore.App")))
            return null;

        var info = StartInfo(sandbox, "/");
        foreach (var argument in new[]
        {
            "--unshare-user", "--disable-userns", "--unshare-pid", "--unshare-net", "--unshare-ipc", "--unshare-uts",
            "--cap-drop", "ALL", "--die-with-parent", "--new-session", "--clearenv",
            "--proc", "/proc", "--dev", "/dev", "--size", "16777216", "--tmpfs", "/tmp", "--chdir", "/worker",
            "--setenv", "DOTNET_EnableDiagnostics", "0",
            "--setenv", "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1"
        })
            info.ArgumentList.Add(argument);

        // Bind platform libraries and the exact shipped solver assets. The API directory, its
        // configuration and the host's temporary directory are outside the mount namespace.
        foreach (var directory in new[] { "/lib/x86_64-linux-gnu", "/usr/lib/x86_64-linux-gnu", "/lib64", "/usr/lib64" })
        {
            if (Directory.Exists(directory)) ReadOnlyBind(info, directory, directory);
        }
        ReadOnlyBind(info, host, "/dotnet/dotnet");
        ReadOnlyBind(info, Path.Combine(dotnetDirectory, "host"), "/dotnet/host");
        ReadOnlyBind(info, Path.Combine(dotnetDirectory, "shared", "Microsoft.NETCore.App"), "/dotnet/shared/Microsoft.NETCore.App");

        foreach (var asset in new[]
        {
            WorkerAssembly, "GenSW.OptimizationWorker.runtimeconfig.json", "GenSW.OptimizationWorker.deps.json",
            "GenSW.Domain.dll", "Highs.Native.dll"
        })
        {
            var source = Path.Combine(AppContext.BaseDirectory, asset);
            if (!File.Exists(source)) return null;
            ReadOnlyBind(info, source, $"/worker/{asset}");
        }

        const string native = "highs.so";
        const string packaged = "runtimes/linux-x64/native/highs.so";
        var library = File.Exists(Path.Combine(AppContext.BaseDirectory, packaged)) ? packaged : native;
        var librarySource = Path.Combine(AppContext.BaseDirectory, library);
        if (!File.Exists(librarySource)) return null;
        ReadOnlyBind(info, librarySource, $"/worker/{library}");

        info.ArgumentList.Add("--");
        info.ArgumentList.Add("/dotnet/dotnet");
        info.ArgumentList.Add($"/worker/{WorkerAssembly}");
        return info;
    }

    private static void ReadOnlyBind(ProcessStartInfo info, string source, string destination)
    {
        info.ArgumentList.Add("--ro-bind");
        info.ArgumentList.Add(source);
        info.ArgumentList.Add(destination);
    }

    private static ProcessStartInfo DirectTestStartInfo(string host, string worker)
    {
        var info = StartInfo(host, AppContext.BaseDirectory);
        info.ArgumentList.Add(worker);
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("SystemRoot") is { } systemRoot)
            info.Environment["SystemRoot"] = systemRoot;
        return info;
    }

    private static ProcessStartInfo StartInfo(string executable, string directory)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
        };
        info.Environment.Clear();
        info.Environment["DOTNET_EnableDiagnostics"] = "0";
        return info;
    }

    private static string? FindDotnetHost()
    {
        var name = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var current = Environment.ProcessPath;
        if (string.Equals(Path.GetFileName(current), name, StringComparison.OrdinalIgnoreCase) && File.Exists(current))
            return current;

        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var installed = Path.GetFullPath(Path.Combine(runtime, "..", "..", "..", name));
        return File.Exists(installed) ? installed : null;
    }

    private static async Task StopAsync(Process process)
    {
        try
        {
            if (process.HasExited)
                return;

            process.Kill(entireProcessTree: true);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await process.WaitForExitAsync(cleanup.Token);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or OperationCanceledException)
        {
            // The child may have exited between HasExited and Kill.
        }
    }
}
