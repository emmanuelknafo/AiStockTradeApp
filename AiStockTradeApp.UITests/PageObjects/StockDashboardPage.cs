using Microsoft.Playwright;

namespace AiStockTradeApp.UITests.PageObjects;

public class StockDashboardPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public StockDashboardPage(IPage page, string baseUrl = "https://localhost:7043")
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    // Locators
    public ILocator Header => _page.Locator("h1");
    public ILocator TickerInput => _page.Locator("#ticker-input");
    public ILocator AddButton => _page.Locator("#add-button");
    public ILocator ClearAllButton => _page.Locator("#clear-all");
    public ILocator Watchlist => _page.Locator("#watchlist");
    public ILocator ThemeToggle => _page.Locator("#theme-toggle");
    public ILocator SettingsToggle => _page.Locator("#settings-toggle");
    public ILocator AlertsToggle => _page.Locator("#alerts-toggle");
    public ILocator AutoRefreshToggle => _page.Locator("#auto-refresh-toggle");
    public ILocator SettingsPanel => _page.Locator("#settings-panel");
    public ILocator AlertsPanel => _page.Locator("#alerts-panel");
    public ILocator PortfolioSummary => _page.Locator("#portfolio-summary");
    public ILocator NotificationContainer => _page.Locator("#notification-container");
    public ILocator SearchSuggestions => _page.Locator("#search-suggestions");

    // Navigation
    public async Task NavigateTo()
    {
        await _page.GotoAsync($"{_baseUrl}/Stock/Dashboard");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task WaitForPageReady()
    {
        // Wait for essential elements to be available (but not necessarily visible since watchlist can be empty)
        await Watchlist.WaitForAsync(new() { Timeout = 10000, State = WaitForSelectorState.Attached });
        await TickerInput.WaitForAsync(new() { Timeout = 10000 });
        await AddButton.WaitForAsync(new() { Timeout = 10000 });
        // Small delay to ensure all JavaScript has initialized
        await _page.WaitForTimeoutAsync(500);
    }

    // Stock Management Actions
    public async Task AddStock(string symbol)
    {
        await TickerInput.FillAsync(symbol);
        await AddButton.ClickAsync();
        
        // Wait for success notification to appear
        try
        {
            await _page.WaitForSelectorAsync(".notification.success", new() { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // Might have failed to add stock, but continue anyway
        }
        
        // Wait for page reload to complete (JavaScript triggers reload after 1 second)
        // We need to wait for network idle to ensure the page has fully reloaded
        try
        {
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            // Page might not reload if there was an error, continue anyway
        }
        
        // Additional wait to ensure stock card rendering is complete
        await _page.WaitForTimeoutAsync(1000);
    }

    public async Task AddStockAndWaitForLoad(string symbol)
    {
        await AddStock(symbol);
        await WaitForStockToLoad(symbol);
    }

    public async Task RemoveStock(string symbol)
    {
        var stockCard = GetStockCard(symbol);
        
        // Ensure the stock card is visible before trying to remove
        await stockCard.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        
        var removeButton = stockCard.Locator(".remove-button");
        await removeButton.ClickAsync();
        
        // Wait for success notification to appear
        try
        {
            await _page.WaitForSelectorAsync(".notification.success", new() { Timeout = 3000 });
        }
        catch (TimeoutException)
        {
            // Notification might not appear if there's an error, but continue
        }
        
        // Wait for the card to be removed from DOM
        try
        {
            await stockCard.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // Card might still be visible if removal failed, but test should continue
        }
    }

    public async Task ClearAllStocks()
    {
        // Handle the confirmation dialog by setting up a dialog handler
        _page.Dialog += async (_, dialog) =>
        {
            await dialog.AcceptAsync();
        };
        
        await ClearAllButton.ClickAsync();
        await _page.WaitForTimeoutAsync(2000); // Wait for the operation to complete
    }

    // UI Interactions
    public async Task ToggleSettings()
    {
        await SettingsToggle.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    public async Task ToggleAlerts()
    {
        await AlertsToggle.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    public async Task ToggleTheme()
    {
        await ThemeToggle.ClickAsync();
        await _page.WaitForTimeoutAsync(500);
    }

    public async Task ToggleAutoRefresh()
    {
        await AutoRefreshToggle.ClickAsync();
        await _page.WaitForTimeoutAsync(300);
    }

    // Getters
    public ILocator GetStockCard(string symbol)
    {
        return _page.Locator($"#card-{symbol}");
    }

    public async Task<int> GetStockCardCount()
    {
        try
        {
            // Simple approach: just count the stock cards directly without waiting for watchlist visibility
            await _page.WaitForTimeoutAsync(500); // Brief wait for stability
            return await _page.Locator("#watchlist .stock-card").CountAsync();
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Execution context was destroyed"))
        {
            // If context was destroyed, try once more after a short wait
            await _page.WaitForTimeoutAsync(1000);
            return await _page.Locator("#watchlist .stock-card").CountAsync();
        }
    }

    public async Task<bool> IsStockInWatchlist(string symbol)
    {
        var stockCard = GetStockCard(symbol);
        return await stockCard.IsVisibleAsync();
    }

    public async Task<string> GetStockPrice(string symbol)
    {
        var stockCard = GetStockCard(symbol);
        var priceElement = stockCard.Locator(".price span");
        return await priceElement.TextContentAsync() ?? "";
    }

    public async Task<bool> IsSettingsPanelVisible()
    {
        var hasHiddenClass = await SettingsPanel.GetAttributeAsync("class");
        return hasHiddenClass?.Contains("hidden") != true;
    }

    public async Task<bool> IsAlertsPanelVisible()
    {
        var hasHiddenClass = await AlertsPanel.GetAttributeAsync("class");
        return hasHiddenClass?.Contains("hidden") != true;
    }

    // Search and Suggestions
    public async Task TypeInSearch(string text)
    {
        await TickerInput.FillAsync(text);
        await _page.WaitForTimeoutAsync(1000); // Wait for suggestions
    }

    public async Task<bool> AreSuggestionsVisible()
    {
        return await SearchSuggestions.IsVisibleAsync();
    }

    // Portfolio Information
    public async Task<string> GetTotalPortfolioValue()
    {
        var valueElement = _page.Locator("#total-value");
        return await valueElement.TextContentAsync() ?? "0.00";
    }

    public async Task<string> GetTotalChange()
    {
        var changeElement = _page.Locator("#total-change");
        return await changeElement.TextContentAsync() ?? "";
    }

    public async Task<string> GetStockCount()
    {
        var countElement = _page.Locator("#stock-count");
        return await countElement.TextContentAsync() ?? "0";
    }

    // Wait for portfolio stock count to increase beyond a known value
    public async Task WaitForPortfolioCountIncrease(int previousCount, int timeoutMs = 20000)
    {
        var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var countLocator = _page.Locator("#stock-count");
        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var text = await countLocator.TextContentAsync();
                if (int.TryParse(text, out var current) && current > previousCount)
                {
                    return;
                }
            }
            catch
            {
                // Ignore transient DOM issues and keep polling
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Portfolio stock count did not increase within {timeoutMs}ms");
    }

    // Export Actions
    public async Task ClickExportCsv()
    {
        var exportCsvLink = _page.Locator("#export-csv");
        await exportCsvLink.ClickAsync();
    }

    public async Task ClickExportJson()
    {
        var exportJsonLink = _page.Locator("#export-json");
        await exportJsonLink.ClickAsync();
    }

    // Validation Helpers
    public async Task WaitForStockToLoad(string symbol, int timeoutMs = 20000)
    {
        // First wait for the stock card to be present after potential page reload
        var stockCard = GetStockCard(symbol);
        
        // Wait for stock card to exist in DOM first (handles page reload scenario)
        try
        {
            await stockCard.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = timeoutMs / 2 });
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"Stock card for {symbol} did not appear within {timeoutMs / 2}ms");
        }
        
        var priceElement = stockCard.Locator(".price span");
        
        // Use simple wait with retry pattern for price data to load
        var endTime = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < endTime)
        {
            try
            {
                // Wait for price element to be visible first
                await priceElement.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 1000 });
                
                var text = await priceElement.TextContentAsync();
                if (!string.IsNullOrEmpty(text) && !text.Contains("Loading"))
                {
                    return; // Stock has loaded successfully
                }
            }
            catch
            {
                // Element might not be ready yet or still loading, continue waiting
            }
            
            await Task.Delay(500); // Wait 500ms before checking again
        }
        
        throw new TimeoutException($"Stock {symbol} did not load within {timeoutMs}ms");
    }

    public async Task<bool> TryWaitForStockToLoad(string symbol, int timeoutMs = 20000)
    {
        try
        {
            await WaitForStockToLoad(symbol, timeoutMs);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task WaitForStockToLoadOrTimeout(string symbol, int timeoutMs = 10000)
    {
        try
        {
            await WaitForStockToLoad(symbol, timeoutMs);
        }
        catch (TimeoutException)
        {
            // Ignore timeout - this is for tests that want to continue even if stock doesn't load
        }
    }

    public async Task<bool> WaitForStockToBeRemoved(string symbol, int timeoutMs = 5000)
    {
        var stockCard = GetStockCard(symbol);
        var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        
        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var isVisible = await stockCard.IsVisibleAsync();
                if (!isVisible)
                {
                    return true; // Stock was removed
                }
            }
            catch
            {
                // Element might be detached/removed, which is what we want
                return true;
            }
            
            await Task.Delay(500);
        }
        
        return false; // Stock was not removed within timeout
    }

    public async Task WaitForNotification(int timeoutMs = 5000)
    {
        await NotificationContainer.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
    }
}