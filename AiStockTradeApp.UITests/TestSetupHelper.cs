using Microsoft.Playwright;
using NUnit.Framework;
using System.Net.Http;
using System.Diagnostics;

namespace AiStockTradeApp.UITests;

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
    /// Ensure the API is started for UI tests. Uses PLAYWRIGHT_API_BASE_URL or defaults to http://localhost:5256
    /// </summary>
    public static async Task EnsureApiStartedAsync()
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable("PLAYWRIGHT_API_BASE_URL") ?? "http://localhost:5256";
        var healthUrl = apiBaseUrl.TrimEnd('/') + "/health";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISABLE_UI_TEST_AUTOSTART")))
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
                Assert.Fail($"Cannot auto-start API. Project file not found at {projectPath}");
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
            // Force in-memory DB for API so SQL Server is not required during UI tests
            psi.Environment["USE_INMEMORY_DB"] = "true";

            _apiProcess = new Process { StartInfo = new ProcessStartInfo
            {
                FileName = psi.FileName,
                Arguments = psi.Arguments,
                WorkingDirectory = psi.WorkingDirectory,
                RedirectStandardOutput = psi.RedirectStandardOutput,
                RedirectStandardError = psi.RedirectStandardError,
                UseShellExecute = psi.UseShellExecute,
                CreateNoWindow = psi.CreateNoWindow
            }, EnableRaisingEvents = true };

            try
            {
                if (!_apiProcess!.Start())
                {
                    Assert.Fail("Failed to start API process for UI tests.");
                }
                _apiStartedByTests = true;
                // Drain output asynchronously
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
                Assert.Fail($"Exception starting API process: {ex.Message}\n{ex}");
            }
        }

        var started = await IsApplicationRunningAsync(healthUrl, 30);
        if (!started)
        {
            try { _apiProcess?.Kill(entireProcessTree: true); } catch { }
            Assert.Fail($"Auto-started API did not become responsive at {healthUrl} within timeout.");
        }
    }

    /// <summary>
    /// Attempts to start the UI application if it is not already running.
    /// </summary>
    public static async Task EnsureApplicationStartedAsync(string baseUrl)
    {
        // Allow opt-out (e.g. pipeline already starts the app)
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISABLE_UI_TEST_AUTOSTART")))
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
                Assert.Fail($"Cannot auto-start application. Project file not found at {projectPath}");
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
            var apiBaseUrl = Environment.GetEnvironmentVariable("PLAYWRIGHT_API_BASE_URL") ?? "http://localhost:5256";
            psi.Environment["StockApi__BaseUrl"] = apiBaseUrl;
            psi.Environment["StockApi__HttpBaseUrl"] = apiBaseUrl;

            _appProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            try
            {
                if (!_appProcess!.Start())
                {
                    Assert.Fail("Failed to start application process for UI tests.");
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
                Assert.Fail($"Exception starting application process: {ex.Message}\n{ex}");
            }
        }

        // Wait up to 30s for the app to respond.
        var started = await IsApplicationRunningAsync(baseUrl, 30);
        if (!started)
        {
            try { _appProcess?.Kill(entireProcessTree: true); } catch { }
            Assert.Fail($"Auto-started application did not become responsive at {baseUrl} within timeout.");
        }
    }

    /// <summary>
    /// Public entry for tests: ensures the app and API are running (auto-start if needed) or fails.
    /// </summary>
    public static async Task WaitForApplicationStartup(string baseUrl, int timeoutSeconds = 30)
    {
        await EnsureApiStartedAsync();
        await EnsureApplicationStartedAsync(baseUrl);
        var isRunning = await IsApplicationRunningAsync(baseUrl, timeoutSeconds);
        if (!isRunning)
        {
            Assert.Fail($"Application is not running at {baseUrl} after auto-start attempt.");
        }
    }

    /// <summary>
    /// Stops the application and API if they were started by the UI tests.
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