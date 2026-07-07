using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StudyMind.App.Services;

public sealed class LocalBackendService : IDisposable
{
    private static readonly TimeSpan HealthProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

    private readonly string _bindAddress;
    private readonly Uri _baseAddress;
    private Process? _backendProcess;
    private bool _startedBackend;

    public LocalBackendService()
    {
        var bindAddress = Environment.GetEnvironmentVariable("STUDYMIND_BIND_ADDR")?.Trim();
        if (string.IsNullOrWhiteSpace(bindAddress))
        {
            bindAddress = "127.0.0.1:7878";
        }

        _bindAddress = bindAddress;
        _baseAddress = BuildBaseAddress(_bindAddress);
    }

    public string BaseAddress => _baseAddress.ToString().TrimEnd('/');

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        if (await TryGetHealthAsync(cancellationToken) is not null)
        {
            return;
        }

        var target = ResolveBackendLaunchTarget();
        _backendProcess = StartBackend(target);
        _startedBackend = true;

        var deadline = DateTimeOffset.UtcNow + StartupTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_backendProcess.HasExited)
            {
                throw new InvalidOperationException(
                    $"本地后端启动后立即退出，退出码 {_backendProcess.ExitCode}。");
            }

            if (await TryGetHealthAsync(cancellationToken) is not null)
            {
                return;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException($"本地后端未在 {StartupTimeout.TotalSeconds:0} 秒内就绪，请确认端口 {_bindAddress} 未被占用。");
    }

    public void Dispose()
    {
        if (!_startedBackend || _backendProcess is null)
        {
            return;
        }

        try
        {
            if (!_backendProcess.HasExited)
            {
                _backendProcess.Kill(entireProcessTree: true);
                _backendProcess.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _backendProcess.Dispose();
            _backendProcess = null;
            _startedBackend = false;
        }
    }

    private async Task<BackendHealthResponse?> TryGetHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = _baseAddress,
                Timeout = HealthProbeTimeout
            };
            using var response = await httpClient.GetAsync("/health", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var health = await response.Content.ReadFromJsonAsync<BackendHealthResponse>(
                cancellationToken: cancellationToken);
            return string.Equals(health?.Status, "ok", StringComparison.OrdinalIgnoreCase)
                ? health
                : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private BackendLaunchTarget ResolveBackendLaunchTarget()
    {
        var appBaseDirectory = AppContext.BaseDirectory;

        var releaseBackend = Path.GetFullPath(Path.Combine(
            appBaseDirectory,
            "..",
            "backend",
            "studymind-backend.exe"));
        if (File.Exists(releaseBackend))
        {
            var releaseRoot = Path.GetFullPath(Path.Combine(appBaseDirectory, ".."));
            var workingDirectory = Path.GetDirectoryName(releaseBackend)!;
            return new BackendLaunchTarget(
                releaseBackend,
                "",
                workingDirectory,
                ResolveDatabasePathOverride(
                    workingDirectory,
                    PrepareReleaseDatabasePath(releaseRoot)));
        }

        var repositoryRoot = FindRepositoryRoot(appBaseDirectory);
        if (repositoryRoot is not null)
        {
            var backendDirectory = Path.Combine(repositoryRoot, "backend");
            var databasePath = PrepareDevelopmentDatabasePath(backendDirectory);
            var debugBackend = Path.Combine(backendDirectory, "target", "debug", "studymind-backend.exe");
            var releaseBackendFromSource = Path.Combine(backendDirectory, "target", "release", "studymind-backend.exe");

            if (File.Exists(debugBackend))
            {
                return new BackendLaunchTarget(
                    debugBackend,
                    "",
                    backendDirectory,
                    ResolveDatabasePathOverride(backendDirectory, databasePath));
            }

            if (File.Exists(releaseBackendFromSource))
            {
                return new BackendLaunchTarget(
                    releaseBackendFromSource,
                    "",
                    backendDirectory,
                    ResolveDatabasePathOverride(backendDirectory, databasePath));
            }

            return new BackendLaunchTarget(
                "cargo",
                "run --quiet",
                backendDirectory,
                ResolveDatabasePathOverride(backendDirectory, databasePath));
        }

        throw new FileNotFoundException(
            "找不到本地后端程序。请确认发布目录包含 backend\\studymind-backend.exe，或在源码目录中运行前端。");
    }

    private Process StartBackend(BackendLaunchTarget target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target.DatabasePath)!);

        var startInfo = new ProcessStartInfo
        {
            FileName = target.FileName,
            Arguments = target.Arguments,
            WorkingDirectory = target.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["STUDYMIND_BIND_ADDR"] = _bindAddress;
        startInfo.Environment["STUDYMIND_DATABASE_PATH"] = target.DatabasePath;

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("本地后端进程启动失败。");
    }

    private static Uri BuildBaseAddress(string bindAddress)
    {
        if (bindAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            bindAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(bindAddress);
        }

        var clientAddress = bindAddress.StartsWith("0.0.0.0:", StringComparison.Ordinal)
            ? $"127.0.0.1:{bindAddress["0.0.0.0:".Length..]}"
            : bindAddress;

        return new Uri($"http://{clientAddress}");
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "Cargo.toml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string PrepareDevelopmentDatabasePath(string backendDirectory)
    {
        var dataDirectory = Path.Combine(backendDirectory, "data");
        Directory.CreateDirectory(dataDirectory);
        return Path.Combine(dataDirectory, "studymind.db");
    }

    private static string PrepareReleaseDatabasePath(string releaseRoot)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(releaseRoot, "data")
            : Path.Combine(localAppData, "StudyMind");
        Directory.CreateDirectory(dataDirectory);

        var databasePath = Path.Combine(dataDirectory, "studymind.db");
        var legacyDataDirectory = Path.Combine(releaseRoot, "data");
        var legacyDatabasePath = Path.Combine(legacyDataDirectory, "studymind.db");
        if (!File.Exists(databasePath) && File.Exists(legacyDatabasePath))
        {
            foreach (var source in Directory.GetFiles(legacyDataDirectory, "studymind.db*"))
            {
                File.Copy(source, Path.Combine(dataDirectory, Path.GetFileName(source)), overwrite: true);
            }
        }

        return databasePath;
    }

    private static string ResolveDatabasePathOverride(string workingDirectory, string fallbackPath)
    {
        var overridePath = Environment.GetEnvironmentVariable("STUDYMIND_DATABASE_PATH")?.Trim();
        if (string.IsNullOrWhiteSpace(overridePath))
        {
            return fallbackPath;
        }

        return Path.IsPathRooted(overridePath)
            ? overridePath
            : Path.GetFullPath(Path.Combine(workingDirectory, overridePath));
    }

    private sealed record BackendLaunchTarget(
        string FileName,
        string Arguments,
        string WorkingDirectory,
        string DatabasePath);

    private sealed class BackendHealthResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("database")]
        public string Database { get; set; } = "";
    }
}
