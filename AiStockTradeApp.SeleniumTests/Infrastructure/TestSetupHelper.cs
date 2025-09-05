using System.Diagnostics;
using System.Net.Http;

namespace AiStockTradeApp.SeleniumTests.Infrastructure;

/// <summary>
/// Helper class to ensure the application (UI) and API are running before tests execute
/// </summary>
public static class TestSetupHelper
{
    private static Process? _appProcess;
    private static Process? _apiProcess;
    private static readonly object _lock = new();
    private static bool _startedByTests = false;
    private static bool _apiStartedByTests = false;

    /// <summary>
    /// Checks if the application is responding on the provided baseUrl within the timeout.
    /// </summary>
    public static async Task<bool> IsApplicationRunningAsync(string baseUrl, int timeoutSeconds = 30)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                var response = await httpClient.GetAsync(baseUrl);
                if ((int)response.StatusCode < 500) // accept any non-5xx as "up"
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
                await Task.Delay(750);
            }
            catch (TaskCanceledException)
            {
                await Task.Delay(750);
            }
        }
        return false;
    }

    /// <summary>
    /// Ensure the API is started for Selenium tests. Uses SELENIUM_API_BASE_URL or defaults to https://localhost:7032
    /// </summary>
    public static async Task EnsureApiStartedAsync()
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable("SELENIUM_API_BASE_URL") ?? "https://localhost:7032";
        var healthUrl = apiBaseUrl.TrimEnd('/') + "/health";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISABLE_SELENIUM_TEST_AUTOSTART")))
            return;

        if (await IsApplicationRunningAsync(healthUrl, 5))
            return; // API already running

        lock (_lock)
        {
            if (_apiProcess is { HasExited: false })
                return;

            var solutionDir = GetSolutionRoot();
            var projectPath = Path.Combine(solutionDir, "AiStockTradeApp.Api", "AiStockTradeApp.Api.csproj");
            if (!File.Exists(projectPath))
            {
                throw new InvalidOperationException($"Cannot auto-start API. Project file not found at {projectPath}");
            }

            var uri = new Uri(apiBaseUrl);
            var urlArg = $"--urls {uri.Scheme}://localhost:{uri.Port}";

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" {urlArg}",
                WorkingDirectory = solutionDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "true";
            psi.Environment["DOTNET_NOLOGO"] = "true";
            // Force in-memory DB for API so SQL Server is not required during Selenium tests
            psi.Environment["USE_INMEMORY_DB"] = "true";

            _apiProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

            try
            {
                if (!_apiProcess!.Start())
                {
                    throw new InvalidOperationException("Failed to start API process for Selenium tests.");
                }
                _apiStartedByTests = true;
                
                // Drain output asynchronously to prevent deadlocks
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (_apiProcess is { HasExited: false })
                        {
                            var _ = await _apiProcess.StandardOutput.ReadLineAsync();
                            if (_ == null) break;
                        }
                    }
                    catch { }
                });
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (_apiProcess is { HasExited: false })
                        {
                            var _ = await _apiProcess.StandardError.ReadLineAsync();
                            if (_ == null) break;
                        }
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Exception starting API process: {ex.Message}", ex);
            }
        }

        var started = await IsApplicationRunningAsync(healthUrl, 30);
        if (!started)
        {
            try { _apiProcess?.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException($"Auto-started API did not become responsive at {healthUrl} within timeout.");
        }
    }

    /// <summary>
    /// Attempts to start the UI application if it is not already running.
    /// </summary>
    public static async Task EnsureApplicationStartedAsync(string baseUrl)
    {
        // Allow opt-out (e.g. pipeline already starts the app)
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISABLE_SELENIUM_TEST_AUTOSTART")))
            return;

        if (await IsApplicationRunningAsync(baseUrl, 5))
            return; // already running

        lock (_lock)
        {
            if (_appProcess is { HasExited: false })
                return; // another thread already started it

            var solutionDir = GetSolutionRoot();
            var projectPath = Path.Combine(solutionDir, "AiStockTradeApp", "AiStockTradeApp.csproj");
            if (!File.Exists(projectPath))
            {
                throw new InvalidOperationException($"Cannot auto-start application. Project file not found at {projectPath}");
            }

            // Derive explicit URL from baseUrl to force Kestrel binding (improves startup detection speed)
            var uri = new Uri(baseUrl);
            var urlArg = $"--urls {uri.Scheme}://localhost:{uri.Port}";

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" {urlArg}",
                WorkingDirectory = solutionDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "true";
            psi.Environment["DOTNET_NOLOGO"] = "true";

            // Pass API base URL down to UI so it knows where to call
            var apiBaseUrl = Environment.GetEnvironmentVariable("SELENIUM_API_BASE_URL") ?? "https://localhost:7032";
            psi.Environment["StockApi__BaseUrl"] = apiBaseUrl;
            psi.Environment["StockApi__HttpBaseUrl"] = apiBaseUrl;

            _appProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            try
            {
                if (!_appProcess!.Start())
                {
                    throw new InvalidOperationException("Failed to start application process for Selenium tests.");
                }
                _startedByTests = true;
                
                // Drain output asynchronously to prevent deadlocks
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (_appProcess is { HasExited: false })
                        {
                            var _ = await _appProcess.StandardOutput.ReadLineAsync();
                            if (_ == null) break;
                        }
                    }
                    catch { /* ignore */ }
                });
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (_appProcess is { HasExited: false })
                        {
                            var _ = await _appProcess.StandardError.ReadLineAsync();
                            if (_ == null) break;
                        }
                    }
                    catch { /* ignore */ }
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Exception starting application process: {ex.Message}", ex);
            }
        }

        // Wait up to 30s for the app to respond.
        var started = await IsApplicationRunningAsync(baseUrl, 30);
        if (!started)
        {
            try { _appProcess?.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException($"Auto-started application did not become responsive at {baseUrl} within timeout.");
        }
    }

    /// <summary>
    /// Public entry for tests: ensures the app and API are running (auto-start if needed) or fails.
    /// </summary>
    public static async Task WaitForApplicationStartupAsync(string baseUrl, int timeoutSeconds = 30)
    {
        await EnsureApiStartedAsync();
        await EnsureApplicationStartedAsync(baseUrl);
        var isRunning = await IsApplicationRunningAsync(baseUrl, timeoutSeconds);
        if (!isRunning)
        {
            throw new InvalidOperationException($"Application is not running at {baseUrl} after auto-start attempt.");
        }
    }

    /// <summary>
    /// Stops the application and API if they were started by the Selenium tests.
    /// </summary>
    public static void StopIfStartedByTests()
    {
        lock (_lock)
        {
            if (_startedByTests && _appProcess is { HasExited: false })
            {
                try { _appProcess.Kill(entireProcessTree: true); } catch { }
                finally { _appProcess.Dispose(); _appProcess = null; }
            }
            if (_apiStartedByTests && _apiProcess is { HasExited: false })
            {
                try { _apiProcess.Kill(entireProcessTree: true); } catch { }
                finally { _apiProcess.Dispose(); _apiProcess = null; }
            }
        }
    }

    private static string GetSolutionRoot()
    {
        // Walk up from current directory until .sln is found (fallback to current)
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6; i++)
        {
            if (Directory.GetFiles(dir, "*.sln").Any())
                return dir;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null) break;
            dir = parent;
        }
        return Directory.GetCurrentDirectory();
    }
}