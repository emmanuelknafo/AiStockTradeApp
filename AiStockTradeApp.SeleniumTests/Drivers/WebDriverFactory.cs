using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace AiStockTradeApp.SeleniumTests.Drivers;

public static class WebDriverFactory
{
    public static IWebDriver Create(TestSettings settings)
    {
        var options = new ChromeOptions();
        if (settings.Headless)
        {
            options.AddArgument("--headless=new");
            options.AddArgument("--disable-gpu");
        }
        options.AddArgument("--window-size=1600,1200");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");

        var driver = new ChromeDriver(options);
        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(settings.ImplicitWaitMs);
        driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(settings.PageLoadTimeoutSec);
        return driver;
    }
}
