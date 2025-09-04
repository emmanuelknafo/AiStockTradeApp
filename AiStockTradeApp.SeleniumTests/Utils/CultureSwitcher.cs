using OpenQA.Selenium;

namespace AiStockTradeApp.SeleniumTests.Utils;

public static class CultureSwitcher
{
    public static void SetCulture(IWebDriver driver, string baseUrl, string culture)
    {
        // Assumes an endpoint like /Home/SetLanguage?culture=xx&returnUrl=/
        var url = baseUrl.TrimEnd('/') + $"/Home/SetLanguage?culture={culture}&returnUrl=/";
        driver.Navigate().GoToUrl(url);
    }
}
