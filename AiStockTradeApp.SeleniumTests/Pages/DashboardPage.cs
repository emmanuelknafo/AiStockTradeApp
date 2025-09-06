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
    // Extended overall timeout to better tolerate slower local reloads / network
    var overallTimeout = DateTime.UtcNow + TimeSpan.FromSeconds(40);
        var js = (IJavaScriptExecutor)_driver;
        var attempt = 0;
    // Allow longer initial element wait in CI where cold start can be slower
    var isCi = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI"));
        var initialInputWaitSeconds = isCi ? 20 : 12;
        var overrideWait = Environment.GetEnvironmentVariable("SELENIUM_INPUT_WAIT_SECONDS");
        if (int.TryParse(overrideWait, out var parsed) && parsed > initialInputWaitSeconds && parsed <= 120)
        {
            initialInputWaitSeconds = parsed;
        }

        while (DateTime.UtcNow < overallTimeout)
        {
            attempt++;
            try
            {
                // Wait for input to be interactable (allow readyState 'interactive' or 'complete')
                var waitForInput = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(initialInputWaitSeconds), TimeSpan.FromMilliseconds(300));
                var input = waitForInput.Until(drv =>
                {
                    try
                    {
                        var el = drv.FindElements(StockInput).FirstOrDefault();
                        if (el == null) return null;
                        if (!el.Displayed || !el.Enabled) return null;
                        var rs = (js.ExecuteScript("return document.readyState") as string ?? string.Empty);
                        return (rs == "complete" || rs == "interactive") ? el : null;
                    }
                    catch (StaleElementReferenceException) { return null; }
                });

                if (input == null)
                {
                    // Soft refresh once if early attempts cannot find input
                    if (attempt <= 2) { _driver.Navigate().Refresh(); continue; }
                    throw new WebDriverTimeoutException("Ticker input not found / interactable");
                }

                // If card already exists (previous attempt succeeded during reload), exit early
                if (_driver.FindElements(StockCard(symbol)).Any())
                    return this;

                input.Clear();
                input.SendKeys(symbol);
                _driver.FindElement(AddStockButton).Click();

                // After clicking Add the frontend schedules a reload (setTimeout(...,1000)).
                // We loop waiting for either: (a) card appears pre-reload, (b) page reload completes and card appears, (c) notification indicates success then card.
                var postClickDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
                var sawReload = false;
                while (DateTime.UtcNow < postClickDeadline)
                {
                    // Card present
                    if (_driver.FindElements(StockCard(symbol)).Any())
                        return this;

                    // Detect a reload completion (readyState==complete after a transition)
                    try
                    {
                        var rs = (js.ExecuteScript("return document.readyState") as string ?? string.Empty);
                        if (rs == "complete")
                        {
                            if (sawReload && _driver.FindElements(StockCard(symbol)).Any()) return this;
                        }
                        else
                        {
                            sawReload = true; // we entered a non-complete state
                        }
                    }
                    catch (InvalidOperationException) { /* ignore transient navigation */ }

                    // Check for toast success to optionally extend wait slightly
                    var toast = _driver.FindElements(NotificationToast).FirstOrDefault();
                    if (toast != null && toast.Text.Contains("added", StringComparison.OrdinalIgnoreCase))
                    {
                        // Give DOM a brief chance to update
                        Thread.Sleep(400);
                    }
                    Thread.Sleep(250);
                }

                // If we exhausted inner wait but still within overall, perform a controlled refresh and retry
                if (DateTime.UtcNow < overallTimeout)
                {
                    _driver.Navigate().Refresh();
                    Thread.Sleep(500);
                    continue;
                }
            }
            catch (WebDriverTimeoutException)
            {
                // Last resort: refresh and retry while time remains
                if (DateTime.UtcNow < overallTimeout)
                {
                    _driver.Navigate().Refresh();
                    Thread.Sleep(700);
                    continue;
                }
                throw;
            }
        }

        return this; // Return even if not found; test can assert absence
    }

    /// <summary>
    /// Wait until dashboard considered "loaded": document ready & either stock input or empty state present.
    /// Returns immediately if condition already satisfied. No exception on timeout—test can still proceed.
    /// </summary>
    public DashboardPage WaitUntilLoaded(TimeSpan? timeout = null)
    {
        var js = (IJavaScriptExecutor)_driver;
        var end = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (DateTime.UtcNow < end)
        {
            try
            {
                var rs = (js.ExecuteScript("return document.readyState") as string ?? string.Empty);
                var hasInput = _driver.FindElements(StockInput).Any();
                var hasEmpty = _driver.FindElements(EmptyState).Any();
                if ((rs == "complete" || rs == "interactive") && (hasInput || hasEmpty)) return this;
            }
            catch { /* ignore transient */ }
            Thread.Sleep(250);
        }
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

    /// <summary>
    /// Removes all symbols from the watchlist via UI interactions. Retries a few times
    /// to account for async reloads between removals. Safe to call when already empty.
    /// </summary>
    public DashboardPage RemoveAllSymbols(int maxLoops = 4)
    {
        for (var i = 0; i < maxLoops; i++)
        {
            var symbols = GetSymbols().ToList();
            if (symbols.Count == 0) return this;
            foreach (var s in symbols)
            {
                try { RemoveSymbol(s); } catch { /* swallow; retry next loop */ }
            }
            // Small pause to allow DOM to settle after removals / potential reloads
            Thread.Sleep(300);
        }
        return this;
    }

    /// <summary>
    /// Waits for the empty state to become visible (or timeout). No exception thrown on timeout.
    /// </summary>
    public DashboardPage WaitForEmptyState(TimeSpan? timeout = null)
    {
        var end = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < end)
        {
            if (IsEmptyStateVisible()) break;
            Thread.Sleep(150);
        }
        return this;
    }
}
