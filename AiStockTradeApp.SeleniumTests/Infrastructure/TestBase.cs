using System.Text.Json;
using OpenQA.Selenium;
using AiStockTradeApp.SeleniumTests.Drivers;

namespace AiStockTradeApp.SeleniumTests.Infrastructure;

public abstract class TestBase : IAsyncLifetime
{
    protected IWebDriver Driver { get; private set; } = default!;
    protected TestSettings Settings { get; private set; } = default!;

    public virtual Task InitializeAsync()
    {
        Settings = LoadSettings();
        Driver = WebDriverFactory.Create(Settings);
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        Driver.Quit();
        Driver.Dispose();
        return Task.CompletedTask;
    }

    private static TestSettings LoadSettings()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestSettings.json"));
        var settings = JsonSerializer.Deserialize<TestSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        settings ??= new TestSettings();
        // Environment overlay for CI
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
