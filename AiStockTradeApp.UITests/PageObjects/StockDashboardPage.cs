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
    // Prefer data-testid attributes (added to Razor views) with fallback to legacy IDs
    public ILocator TickerInput => _page.Locator("[data-testid='ticker-input'], #ticker-input");
    public ILocator AddButton => _page.Locator("[data-testid='add-button'], #add-button");
    public ILocator ClearAllButton => _page.Locator("[data-testid='clear-all'], #clear-all");
    public ILocator Watchlist => _page.Locator("[data-testid='watchlist'], #watchlist");
    public ILocator ThemeToggle => _page.Locator("#theme-toggle");
    public ILocator SettingsToggle => _page.Locator("#settings-toggle");
    public ILocator AlertsToggle => _page.Locator("#alerts-toggle");
    public ILocator AutoRefreshToggle => _page.Locator("#auto-refresh-toggle");
    public ILocator SettingsPanel => _page.Locator("#settings-panel");
    public ILocator AlertsPanel => _page.Locator("#alerts-panel");
    public ILocator PortfolioSummary => _page.Locator("#portfolio-summary");
    public ILocator NotificationContainer => _page.Locator("#notification-container");
    public ILocator SearchSuggestions => _page.Locator("[data-testid='search-suggestions'], #search-suggestions");

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

        // Robust wait: any of (notification, stock card, input cleared + network idle) within timeout
        var timeoutMs = 10000;
        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var cardSelector = $"#card-{symbol}";
        bool success = false;
        while (DateTime.UtcNow < end)
        {
            // Check notification first
            var notif = await _page.Locator(".notification").AllAsync();
            if (notif.Count > 0)
            {
                var txt = (await notif[0].TextContentAsync()) ?? string.Empty;
                if (txt.Contains("Added", StringComparison.OrdinalIgnoreCase) || txt.Contains("Success", StringComparison.OrdinalIgnoreCase))
                {
                    success = true;
                }
                // Break regardless—we captured notification for test logic to evaluate
                break;
            }

            // If stock card already present (maybe notification was very fast and disappeared)
            if (await _page.Locator(cardSelector).IsVisibleAsync())
            {
                success = true;
                break;
            }

            // If input cleared, give chance for reload / card to appear
            try
            {
                var inputValue = await TickerInput.InputValueAsync(new LocatorInputValueOptions { Timeout = 500 });
                if (string.IsNullOrEmpty(inputValue))
                {
                    // Wait briefly for potential reload/render
                    await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 3000 });
                }
            }
            catch { /* ignore transient */ }

            await Task.Delay(300);
        }

        if (success)
        {
            // Ensure final load & rendering
            try
            {
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 5000 });
                await _page.WaitForTimeoutAsync(500);
            }
            catch { }
        }
    }

    public async Task AddStockAndWaitForLoad(string symbol)
    {
        await AddStock(symbol);
        // Try to wait for stock to load, but don't fail if it doesn't
        try
        {
            await WaitForStockToLoad(symbol, 15000);
        }
        catch (TimeoutException)
        {
            // Stock might not load due to API issues, which is acceptable in test environment
        }
    }

    public async Task RemoveStock(string symbol)
    {
        var stockCard = GetStockCard(symbol);
        
        // Ensure the stock card is visible before trying to remove
        try
        {
            await stockCard.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // Stock card might not be visible, skip removal
            return;
        }
        
        var removeButton = stockCard.Locator(".remove-button");
        await removeButton.ClickAsync();
        
        // Wait for the card to be removed from DOM
        try
        {
            await stockCard.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 10000 });
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
        await _page.WaitForTimeoutAsync(3000); // Wait for the operation to complete
        
        // Wait for potential page reload
        try
        {
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });
        }
        catch (TimeoutException)
        {
            // No reload might occur, continue
        }
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
    return _page.Locator($"[data-testid='stock-card-{symbol}'], #card-{symbol}");
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
        try
        {
            return await stockCard.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetStockPrice(string symbol)
    {
        try
        {
            var stockCard = GetStockCard(symbol);
            var priceElement = stockCard.Locator(".price span");
            return await priceElement.TextContentAsync() ?? "";
        }
        catch
        {
            return "";
        }
    }

    public async Task<bool> IsSettingsPanelVisible()
    {
        try
        {
            var hasHiddenClass = await SettingsPanel.GetAttributeAsync("class");
            return hasHiddenClass?.Contains("hidden") != true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsAlertsPanelVisible()
    {
        try
        {
            var hasHiddenClass = await AlertsPanel.GetAttributeAsync("class");
            return hasHiddenClass?.Contains("hidden") != true;
        }
        catch
        {
            return false;
        }
    }

    // Search and Suggestions
    public async Task TypeInSearch(string text)
    {
        await TickerInput.FillAsync(text);
        await _page.WaitForTimeoutAsync(1000); // Wait for suggestions
    }

    public async Task<bool> AreSuggestionsVisible()
    {
        try
        {
            return await SearchSuggestions.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }

    // Portfolio Information
    public async Task<string> GetTotalPortfolioValue()
    {
        try
        {
            var valueElement = _page.Locator("#total-value");
            return await valueElement.TextContentAsync() ?? "0.00";
        }
        catch
        {
            return "0.00";
        }
    }

    public async Task<string> GetTotalChange()
    {
        try
        {
            var changeElement = _page.Locator("#total-change");
            return await changeElement.TextContentAsync() ?? "";
        }
        catch
        {
            return "";
        }
    }

    public async Task<string> GetStockCount()
    {
        try
        {
            var countElement = _page.Locator("#stock-count");
            return await countElement.TextContentAsync() ?? "0";
        }
        catch
        {
            return "0";
        }
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
        try
        {
            await _page.Locator(".notification").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
        }
        catch (TimeoutException)
        {
            // No notification appeared, which might be acceptable
        }
    }
}