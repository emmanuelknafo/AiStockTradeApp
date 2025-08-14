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

    // Stock Management Actions
    public async Task AddStock(string symbol)
    {
        await TickerInput.FillAsync(symbol);
        await AddButton.ClickAsync();
        await _page.WaitForTimeoutAsync(3000); // Wait longer for API call and potential page actions
        
        // Wait for either success notification or stock card to appear
        try
        {
            await _page.WaitForSelectorAsync($"#card-{symbol}", new() { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // Stock might not be added due to API issues, but that's ok for tests
        }
    }

    public async Task RemoveStock(string symbol)
    {
        var stockCard = GetStockCard(symbol);
        var removeButton = stockCard.Locator(".remove-button");
        await removeButton.ClickAsync();
        await _page.WaitForTimeoutAsync(2000); // Wait longer for removal
        
        // Wait for the card to disappear
        try
        {
            await stockCard.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 3000 });
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
        return await Watchlist.Locator(".stock-card").CountAsync();
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
    public async Task WaitForStockToLoad(string symbol, int timeoutMs = 10000)
    {
        var stockCard = GetStockCard(symbol);
        var priceElement = stockCard.Locator(".price span");
        
        // Use simple wait with retry pattern instead of WaitForFunctionAsync
        var endTime = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < endTime)
        {
            try
            {
                var text = await priceElement.TextContentAsync();
                if (!string.IsNullOrEmpty(text) && !text.Contains("Loading"))
                {
                    return; // Stock has loaded successfully
                }
            }
            catch
            {
                // Element might not be ready yet, continue waiting
            }
            
            await Task.Delay(500); // Wait 500ms before checking again
        }
        
        throw new TimeoutException($"Stock {symbol} did not load within {timeoutMs}ms");
    }

    public async Task WaitForNotification(int timeoutMs = 5000)
    {
        await NotificationContainer.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
    }
}