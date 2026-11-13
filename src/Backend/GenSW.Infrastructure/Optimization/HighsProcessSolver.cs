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

    public HighsProcessSolver() : this(TimeSpan.FromSeconds(10)) { }

    internal HighsProcessSolver(TimeSpan watchdog) => this.watchdog = watchdog;

    public async Task<OptimizationSolverResult> SolveAsync(OptimizationNativeModel model, TimeSpan budget, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var worker = Path.Combine(AppContext.BaseDirectory, WorkerAssembly);
        var host = FindDotnetHost();
        if (!File.Exists(worker) || host is null)
            return new(OptimizationNativeStatus.Error, "WorkerNativoIndisponivel");

        using var process = new Process { StartInfo = StartInfo(host, worker) };
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

    private static ProcessStartInfo StartInfo(string host, string worker)
    {
        var info = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        info.ArgumentList.Add(worker);
        info.Environment.Clear();
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("SystemRoot") is { } systemRoot)
            info.Environment["SystemRoot"] = systemRoot;
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
