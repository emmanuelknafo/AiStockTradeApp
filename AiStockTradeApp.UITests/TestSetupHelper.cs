using Microsoft.Playwright;
using NUnit.Framework;
using System.Net.Http;
using System.Diagnostics;

namespace ai_stock_trade_app.UITests;

/// <summary>
/// Helper class to ensure the application is running before tests execute
/// </summary>
public static class TestSetupHelper
{
    private static Process? _appProcess;
    private static readonly object _lock = new();
    private static bool _startedByTests = false;

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
    /// Attempts to start the application if it is not already running. Respects the DISABLE_UI_TEST_AUTOSTART env var.
    /// </summary>
    private static async Task EnsureApplicationStartedAsync(string baseUrl)
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
            var projectPath = Path.Combine(solutionDir, "ai-stock-trade-app", "ai-stock-trade-app.csproj");
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
                            var line = await _appProcess.StandardOutput.ReadLineAsync();
                            if (line == null) break;
                            if (line.Contains("Now listening on"))
                            {
                                // quick early signal; we still perform HTTP probe below
                            }
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
    /// Public entry for tests: ensures the app is running (auto-start if needed) or fails.
    /// </summary>
    public static async Task WaitForApplicationStartup(string baseUrl, int timeoutSeconds = 30)
    {
        await EnsureApplicationStartedAsync(baseUrl);
        var isRunning = await IsApplicationRunningAsync(baseUrl, timeoutSeconds);
        if (!isRunning)
        {
            Assert.Fail($"Application is not running at {baseUrl} after auto-start attempt.");
        }
    }

    /// <summary>
    /// Stops the application if it was started by the UI tests.
    /// </summary>
    public static void StopIfStartedByTests()
    {
        lock (_lock)
        {
        if (_startedByTests && _appProcess is { HasExited: false })
            {
                try
                {
            _appProcess.Kill(entireProcessTree: true);
                }
                catch { /* ignore */ }
                finally
                {
            _appProcess.Dispose();
            _appProcess = null;
                }
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