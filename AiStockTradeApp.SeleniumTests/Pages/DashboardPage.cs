using OpenQA.Selenium;

namespace AiStockTradeApp.SeleniumTests.Pages;

public class DashboardPage
{
    private readonly IWebDriver _driver;

    public DashboardPage(IWebDriver driver)
    {
        _driver = driver;
    }

    // Locators — prefer data-testid from UI guidelines
    private By StockInput => By.CssSelector("[data-testid='stock-input']");
    private By AddStockButton => By.CssSelector("[data-testid='add-stock-btn']");
    private By WatchlistCards => By.CssSelector("[data-testid^='stock-card-']");
    private By EmptyState => By.CssSelector("[data-testid='empty-state']");
    private By RemoveButton(string symbol) => By.CssSelector($"[data-testid='remove-{symbol}']");

    public DashboardPage Go(string baseUrl)
    {
        _driver.Navigate().GoToUrl(baseUrl.TrimEnd('/') + "/");
        return this;
    }

    public int GetWatchlistCount() => _driver.FindElements(WatchlistCards).Count;
    public bool IsEmptyStateVisible() => _driver.FindElements(EmptyState).Any();

    public DashboardPage AddSymbol(string symbol)
    {
        var input = _driver.FindElement(StockInput);
        input.Clear();
        input.SendKeys(symbol);
        _driver.FindElement(AddStockButton).Click();
        return this;
    }

    public DashboardPage RemoveSymbol(string symbol)
    {
        var remove = _driver.FindElement(RemoveButton(symbol));
        remove.Click();
        return this;
    }

    public IReadOnlyCollection<string> GetSymbols()
    {
        var cards = _driver.FindElements(WatchlistCards);
        return cards
            .Select(c => c.GetAttribute("data-testid"))
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!.Replace("stock-card-", ""))
            .ToArray();
    }
}
