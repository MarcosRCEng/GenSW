using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit.Sdk;

namespace GenSW.API.Tests;

internal sealed class EphemeralPostgreSql : IAsyncDisposable
{
    private const string TemporaryDirectoryPrefix = "gensw-postgresql-tests-";
    private const string DatabaseName = "gensw_auth_tests";
    private const string UserName = "gensw_test";

    private readonly string binDirectory;
    private readonly string rootDirectory;
    private readonly string dataDirectory;
    private bool serverStarted;

    private EphemeralPostgreSql(string binDirectory, string rootDirectory, int port)
    {
        this.binDirectory = binDirectory;
        this.rootDirectory = rootDirectory;
        dataDirectory = Path.Combine(rootDirectory, "data");
        Port = port;
    }

    public int Port { get; private set; }

    public string ConnectionString =>
        $"Host=127.0.0.1;Port={Port};Database={DatabaseName};Username={UserName};Pooling=False;Timeout=5;Command Timeout=30";

    public Task BackupAsync(string destination) => RunAsync("pg_dump",
        ["-h", "127.0.0.1", "-p", Port.ToString(), "-U", UserName, "-Fc", "-f", destination, DatabaseName], TimeSpan.FromSeconds(30));

    public Task RestoreAsync(string source) => RunAsync("pg_restore",
        ["-h", "127.0.0.1", "-p", Port.ToString(), "-U", UserName, "--clean", "--if-exists", "-d", DatabaseName, source], TimeSpan.FromSeconds(30));

    public static async Task<EphemeralPostgreSql> StartAsync(int? initialPort = null)
    {
        var binDirectory = FindPostgreSqlBinDirectory();

        if (binDirectory is null)
        {
            throw SkipException.ForSkip(
                "PostgreSQL integration skipped: initdb, pg_ctl, createdb, pg_dump and pg_restore were not found.");
        }

        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"{TemporaryDirectoryPrefix}{Guid.NewGuid():N}");
        var instance = new EphemeralPostgreSql(binDirectory, rootDirectory, GetAvailablePort());
        Directory.CreateDirectory(rootDirectory);

        try
        {
            await instance.RunAsync(
                "initdb",
                [
                    "-D", instance.dataDirectory,
                    $"--username={UserName}",
                    "--auth=trust",
                    "--encoding=UTF8",
                    "--no-locale",
                ],
                TimeSpan.FromMinutes(1));

            // Recheck after initdb and retry only an observed bind collision: releasing
            // a probe socket cannot reserve its port across parallel test processes.
            instance.Port = initialPort ?? GetAvailablePort();
            var logPath = Path.Combine(rootDirectory, "postgresql.log");
            for (var attempt = 0; ; attempt++)
            {
                var logOffset = File.Exists(logPath) ? File.ReadAllText(logPath).Length : 0;
                try
                {
                    await instance.RunAsync("pg_ctl",
                        ["-D", instance.dataDirectory, "-l", logPath,
                         "-o", $"-p {instance.Port} -h 127.0.0.1 -F", "-w", "start"],
                        TimeSpan.FromMinutes(1), redirectOutput: false);
                    break;
                }
                catch (InvalidOperationException) when (attempt < 2 && PortWasOccupied(logPath, logOffset, instance.Port))
                {
                    instance.Port = GetAvailablePort();
                }
            }
            instance.serverStarted = true;

            await instance.RunAsync(
                "createdb",
                [
                    "-h", "127.0.0.1",
                    "-p", instance.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "-U", UserName,
                    DatabaseName,
                ],
                TimeSpan.FromSeconds(30));

            return instance;
        }
        catch
        {
            await instance.DisposeAsync();
            throw;
        }
    }

    private static bool PortWasOccupied(string logPath, int offset, int port)
    {
        if (!File.Exists(logPath)) return false;
        var log = File.ReadAllText(logPath);
        var recent = log[Math.Min(offset, log.Length)..];
        if (recent.Contains("Address already in use", StringComparison.OrdinalIgnoreCase)) return true;
        // Windows can report a localized access-denied error for an exclusive
        // socket. Confirm a listener occupies the port rather than retrying any
        // permission or configuration error.
        return recent.Contains("could not bind IPv4 address", StringComparison.OrdinalIgnoreCase)
            && System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                .Any(endpoint => endpoint.Port == port &&
                    (IPAddress.IsLoopback(endpoint.Address) || endpoint.Address.Equals(IPAddress.Any)));
    }

    public async ValueTask DisposeAsync()
    {
        Exception? stopFailure = null;

        if (serverStarted)
        {
            try
            {
                await RunAsync(
                    "pg_ctl",
                    ["-D", dataDirectory, "-m", "immediate", "-w", "stop"],
                    TimeSpan.FromSeconds(30));
                serverStarted = false;
            }
            catch (Exception exception)
            {
                stopFailure = exception;
            }
        }

        await DeleteTemporaryDirectorySafelyAsync();

        if (stopFailure is not null)
        {
            throw new InvalidOperationException(
                "The isolated PostgreSQL test server could not be stopped cleanly.",
                stopFailure);
        }
    }

    private async Task RunAsync(
        string executableName,
        IReadOnlyCollection<string> arguments,
        TimeSpan timeout,
        bool redirectOutput = true)
    {
        var executable = Path.Combine(
            binDirectory,
            OperatingSystem.IsWindows() ? $"{executableName}.exe" : executableName);
        var startInfo = new ProcessStartInfo(executable)
        {
            CreateNoWindow = true,
            RedirectStandardError = redirectOutput,
            RedirectStandardOutput = redirectOutput,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {executableName}.");
        var standardOutput = redirectOutput
            ? process.StandardOutput.ReadToEndAsync()
            : Task.FromResult(string.Empty);
        var standardError = redirectOutput
            ? process.StandardError.ReadToEndAsync()
            : Task.FromResult(string.Empty);
        using var timeoutCancellation = new CancellationTokenSource(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException($"{executableName} exceeded its {timeout} test timeout.");
        }

        var output = await standardOutput;
        var error = await standardError;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{executableName} exited with code {process.ExitCode}. " +
                $"Output: {LimitOutput(output)} Error: {LimitOutput(error)}");
        }
    }

    private async Task DeleteTemporaryDirectorySafelyAsync()
    {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var resolvedTarget = Path.GetFullPath(rootDirectory);
        var directoryName = Path.GetFileName(resolvedTarget);

        if (!resolvedTarget.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
            !directoryName.StartsWith(TemporaryDirectoryPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to delete unexpected PostgreSQL test directory '{resolvedTarget}'.");
        }

        for (var attempt = 0; attempt < 10 && Directory.Exists(resolvedTarget); attempt++)
        {
            try
            {
                Directory.Delete(resolvedTarget, recursive: true);
            }
            catch (IOException) when (attempt < 9)
            {
                await Task.Delay(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                await Task.Delay(100);
            }
        }

        if (Directory.Exists(resolvedTarget))
        {
            throw new IOException(
                $"PostgreSQL test directory '{resolvedTarget}' still exists after cleanup retries.");
        }
    }

    private static string? FindPostgreSqlBinDirectory()
    {
        var candidates = new List<string>();
        var configuredDirectory = Environment.GetEnvironmentVariable("GENSW_TEST_POSTGRES_BIN");

        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            candidates.Add(configuredDirectory);
        }

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var postgreSqlRoot = Path.Combine(programFiles, "PostgreSQL");

            if (Directory.Exists(postgreSqlRoot))
            {
                candidates.AddRange(
                    Directory.GetDirectories(postgreSqlRoot)
                        .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                        .Select(path => Path.Combine(path, "bin")));
            }
        }

        var pathDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        candidates.AddRange(pathDirectories);

        return candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(ContainsRequiredExecutables);
    }

    private static bool ContainsRequiredExecutables(string directory)
    {
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        return File.Exists(Path.Combine(directory, $"initdb{extension}")) &&
            File.Exists(Path.Combine(directory, $"pg_ctl{extension}")) &&
            File.Exists(Path.Combine(directory, $"createdb{extension}")) &&
            File.Exists(Path.Combine(directory, $"pg_dump{extension}")) &&
            File.Exists(Path.Combine(directory, $"pg_restore{extension}"));
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string LimitOutput(string output)
    {
        const int maximumLength = 4_000;
        return output.Length <= maximumLength ? output : output[^maximumLength..];
    }
}
