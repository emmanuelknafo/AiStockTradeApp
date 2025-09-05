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
    private By NotificationToast => By.CssSelector(".notification.success, .notification.error");

    public DashboardPage Go(string baseUrl)
    {
    // New dashboard route under UserStock controller
    _driver.Navigate().GoToUrl(baseUrl.TrimEnd('/') + "/UserStock/Dashboard");
        return this;
    }

    public int GetWatchlistCount() => _driver.FindElements(WatchlistCards).Count;
    public int GetWatchlistCount(TimeSpan timeout)
    {
        var end = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < end)
        {
            var count = _driver.FindElements(WatchlistCards).Count;
            if (count > 0) return count;
            Thread.Sleep(250);
        }
        return _driver.FindElements(WatchlistCards).Count;
    }

    public DashboardPage WaitForAtLeast(int minCount, TimeSpan? timeout = null)
    {
        var wait = timeout ?? TimeSpan.FromSeconds(20);
        var end = DateTime.UtcNow + wait;
        while (DateTime.UtcNow < end)
        {
            var count = _driver.FindElements(WatchlistCards).Count;
            if (count >= minCount) break;
            Thread.Sleep(300);
        }
        return this;
    }
    public bool IsEmptyStateVisible() => _driver.FindElements(EmptyState).Any();

    public DashboardPage AddSymbol(string symbol)
    {
        var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(250));
        var js = (IJavaScriptExecutor)_driver;

        // Record current URL to detect reload
        var startUrl = _driver.Url;

        // Ensure input is present & interactable (handle possible previous reload in progress)
        IWebElement input = wait.Until(drv =>
        {
            try
            {
                var el = drv.FindElements(StockInput).FirstOrDefault();
                if (el != null && el.Displayed && el.Enabled)
                {
                    var ready = (js.ExecuteScript("return document.readyState") as string ?? string.Empty) == "complete";
                    return ready ? el : null;
                }
            }
            catch (StaleElementReferenceException) { /* retry */ }
            return null;
        });

        input.Clear();
        input.SendKeys(symbol);
        _driver.FindElement(AddStockButton).Click();

        // The app triggers a setTimeout(... window.location.reload()) after a successful add.
        // Strategy: wait for either (a) card appears without reload or (b) URL change / document ready completes then card appears.
        bool cardAppeared = false;
        try
        {
            wait.Until(drv =>
            {
                // If card already present, done
                if (drv.FindElements(StockCard(symbol)).Any()) { cardAppeared = true; return true; }

                // Detect a reload (URL change OR document becomes loading then complete again)
                var currentUrl = drv.Url;
                var readyState = js.ExecuteScript("return document.readyState") as string ?? string.Empty;
                if (currentUrl != startUrl && readyState == "complete")
                {
                    // After reload check again
                    if (drv.FindElements(StockCard(symbol)).Any()) { cardAppeared = true; return true; }
                }
                return false; // continue waiting
            });
        }
        catch (WebDriverTimeoutException)
        {
            // Fall back: last attempt to locate card after timeout
            cardAppeared = _driver.FindElements(StockCard(symbol)).Any();
        }

        // As a secondary signal, wait briefly for notification toast to disappear (success or error) to reduce race on follow-up calls
        try
        {
            var shortWait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200));
            shortWait.Until(drv => !drv.FindElements(NotificationToast).Any());
        }
        catch (WebDriverTimeoutException) { /* ignore */ }

        return this;
    }

    public DashboardPage RemoveSymbol(string symbol)
    {
        var overallTimeout = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        var attempt = 0;
        while (DateTime.UtcNow < overallTimeout)
        {
            attempt++;
            try
            {
                var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(8), TimeSpan.FromMilliseconds(250));
                var js = (IJavaScriptExecutor)_driver;

                // Wait for document ready & presence of remove button
                var button = wait.Until(drv =>
                {
                    try
                    {
                        var ready = (js.ExecuteScript("return document.readyState") as string ?? string.Empty) == "complete";
                        if (!ready) return null;
                        return drv.FindElements(RemoveButton(symbol)).FirstOrDefault();
                    }
                    catch (StaleElementReferenceException) { return null; }
                });

                if (button == null)
                {
                    // Fallback: hard refresh dashboard once if early attempts fail
                    if (attempt <= 2)
                    {
                        // Attempt a soft reload via location
                        _driver.Navigate().Refresh();
                        continue;
                    }
                }
                else
                {
                    button.Click();
                    // Wait for disappearance
                    wait.Until(drv => !drv.FindElements(RemoveButton(symbol)).Any());
                    return this;
                }
            }
            catch (WebDriverTimeoutException)
            {
                // Navigate again to dashboard and retry (may have caught during reload)
                try
                {
                    _driver.Navigate().GoToUrl(_driver.Url.Split('#')[0]);
                }
                catch { /* ignore */ }
            }
        }
        // Final attempt did not succeed; let test assert state.
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
