using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace AiStockTradeApp.SeleniumTests.Pages;

public class DashboardPage
{
    private readonly IWebDriver _driver;

    public DashboardPage(IWebDriver driver)
    {
        _driver = driver;
    }

    // Locators — prefer data-testid from UI guidelines
    // Updated selectors to match current dashboard markup
    private By StockInput => By.CssSelector("#ticker-input, [data-testid='ticker-input']");
    private By AddStockButton => By.CssSelector("#add-button, [data-testid='add-button']");
    private By WatchlistCards => By.CssSelector("[data-testid^='stock-card-']");
    private By EmptyState => By.CssSelector("[data-testid='empty-watchlist']");
    private By RemoveButton(string symbol) => By.CssSelector($"[data-testid='remove-stock-{symbol}']");
    private By StockCard(string symbol) => By.CssSelector($"[data-testid='stock-card-{symbol.ToUpper()}']");

    public DashboardPage Go(string baseUrl)
    {
    // New dashboard route under UserStock controller
    _driver.Navigate().GoToUrl(baseUrl.TrimEnd('/') + "/UserStock/Dashboard");
        return this;
    }

    public int GetWatchlistCount() => _driver.FindElements(WatchlistCards).Count;
    public bool IsEmptyStateVisible() => _driver.FindElements(EmptyState).Any();

    public DashboardPage AddSymbol(string symbol)
    {
        var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));

        // Ensure input is present & interactable
        var input = wait.Until(drv =>
        {
            var el = drv.FindElements(StockInput).FirstOrDefault();
            return (el != null && el.Displayed && el.Enabled) ? el : null;
        });

        input.Clear();
        input.SendKeys(symbol);
        _driver.FindElement(AddStockButton).Click();

        // Wait for symbol card to appear (handles async AJAX add)
        try
        {
            wait.Until(drv => drv.FindElements(StockCard(symbol)).Any());
        }
        catch (WebDriverTimeoutException)
        {
            // Swallow to allow test to assert; capture diagnostic attribute if needed later
        }
        return this;
    }

    public DashboardPage RemoveSymbol(string symbol)
    {
        var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200));
        // Ensure the card exists first
        wait.Until(drv => drv.FindElements(RemoveButton(symbol)).Any());
        _driver.FindElement(RemoveButton(symbol)).Click();
        // Wait for card to disappear
        wait.Until(drv => !drv.FindElements(RemoveButton(symbol)).Any());
        return this;
    }

    public IReadOnlyCollection<string> GetSymbols()
    {
        // Re-query to avoid stale element references; ignore transient stales
        var cards = _driver.FindElements(WatchlistCards);
        var symbols = new List<string>();
        foreach (var c in cards)
        {
            try
            {
                var dt = c.GetAttribute("data-testid");
                if (!string.IsNullOrEmpty(dt) && dt.StartsWith("stock-card-"))
                {
                    symbols.Add(dt.Replace("stock-card-", ""));
                }
            }
            catch (StaleElementReferenceException)
            {
                // Skip stale; next call will re-evaluate
            }
        }
        return symbols;
    }
}
