using System.Text.Json;
using OpenQA.Selenium;
using AiStockTradeApp.SeleniumTests.Drivers;

namespace AiStockTradeApp.SeleniumTests.Infrastructure;

public abstract class TestBase : IAsyncLifetime
{
    protected IWebDriver Driver { get; private set; } = default!;
    protected TestSettings Settings { get; private set; } = default!;

    // Static flag to ensure global setup runs only once per test session
    private static readonly object _globalSetupLock = new();
    private static bool _globalSetupCompleted = false;

    public virtual async Task InitializeAsync()
    {
        Settings = LoadSettings();
        
        // Ensure global setup runs once per test session
        await EnsureGlobalSetupAsync();
        
        Driver = WebDriverFactory.Create(Settings);
    }

    public virtual Task DisposeAsync()
    {
        Driver.Quit();
        Driver.Dispose();
        return Task.CompletedTask;
    }

    private async Task EnsureGlobalSetupAsync()
    {
        if (_globalSetupCompleted)
            return;

        lock (_globalSetupLock)
        {
            if (_globalSetupCompleted)
                return;

            // This will be set to true after successful setup
        }

        try
        {
            // Auto-start application and API if not already running
            await TestSetupHelper.WaitForApplicationStartupAsync(Settings.BaseUrl, timeoutSeconds: 30);
            
            lock (_globalSetupLock)
            {
                _globalSetupCompleted = true;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start application for Selenium tests. " +
                $"Please ensure the application can start on {Settings.BaseUrl}. " +
                $"Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Cleanup method that should be called at the end of test session.
    /// This is designed to be called by a test runner or through a static finalizer.
    /// </summary>
    public static void GlobalTeardown()
    {
        TestSetupHelper.StopIfStartedByTests();
    }

    private static TestSettings LoadSettings()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestSettings.json"));
        var settings = JsonSerializer.Deserialize<TestSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        settings ??= new TestSettings();
        
        // Environment overlay for CI/CD and local development
        var baseUrl = Environment.GetEnvironmentVariable("SELENIUM_BASE_URL");
        if (!string.IsNullOrWhiteSpace(baseUrl)) settings.BaseUrl = baseUrl;

        var headless = Environment.GetEnvironmentVariable("SELENIUM_HEADLESS");
        if (bool.TryParse(headless, out var hl)) settings.Headless = hl;

        var culture = Environment.GetEnvironmentVariable("SELENIUM_CULTURE");
        if (!string.IsNullOrWhiteSpace(culture)) settings.Culture = culture;

        var username = Environment.GetEnvironmentVariable("SELENIUM_USERNAME");
        var password = Environment.GetEnvironmentVariable("SELENIUM_PASSWORD");
        if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(password))
        {
            settings.Credentials = new Credentials(username ?? settings.Credentials.Username, password ?? settings.Credentials.Password);
        }

        return settings;
    }
}
