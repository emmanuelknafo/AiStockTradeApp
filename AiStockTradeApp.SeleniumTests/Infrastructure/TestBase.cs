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
        return settings ?? new TestSettings();
    }
}
