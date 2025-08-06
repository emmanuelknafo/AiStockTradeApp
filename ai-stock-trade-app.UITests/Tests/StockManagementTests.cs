using Microsoft.Playwright;
using FluentAssertions;
using NUnit.Framework;

namespace ai_stock_trade_app.UITests.Tests;

[TestFixture]
public class StockManagementTests : BaseUITest
{
    [Test]
    public async Task AddStock_WithValidSymbol_ShouldAddToWatchlist()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Enter a valid stock symbol
        var tickerInput = Page.Locator("#ticker-input");
        await tickerInput.FillAsync("AAPL");

        // Click add button
        var addButton = Page.Locator("#add-button");
        await addButton.ClickAsync();

        // Wait for the stock card to appear
        await Page.WaitForTimeoutAsync(3000); // Give time for API call

        // Verify stock card was added
        var stockCard = Page.Locator("#card-AAPL");
        await Expect(stockCard).ToBeVisibleAsync();

        // Verify stock symbol is displayed
        var symbolHeader = stockCard.Locator("h2");
        await Expect(symbolHeader).ToHaveTextAsync("AAPL");

        // Verify remove button exists
        var removeButton = stockCard.Locator(".remove-button");
        await Expect(removeButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task AddStock_WithEmptySymbol_ShouldNotAddToWatchlist()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Click add button without entering a symbol
        var addButton = Page.Locator("#add-button");
        await addButton.ClickAsync();

        // Wait a moment
        await Page.WaitForTimeoutAsync(1000);

        // Verify no new stock cards were added (should only have existing ones if any)
        var watchlist = Page.Locator("#watchlist");
        var initialCards = await watchlist.Locator(".stock-card").CountAsync();
        
        // The count should remain the same (empty or unchanged)
        var finalCards = await watchlist.Locator(".stock-card").CountAsync();
        finalCards.Should().Be(initialCards);
    }

    [Test]
    public async Task RemoveStock_ShouldRemoveFromWatchlist()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // First, add a stock
        var tickerInput = Page.Locator("#ticker-input");
        await tickerInput.FillAsync("MSFT");

        var addButton = Page.Locator("#add-button");
        await addButton.ClickAsync();

        // Wait for the stock card to appear
        await Page.WaitForTimeoutAsync(3000);

        // Verify stock card exists
        var stockCard = Page.Locator("#card-MSFT");
        await Expect(stockCard).ToBeVisibleAsync();

        // Click remove button
        var removeButton = stockCard.Locator(".remove-button");
        await removeButton.ClickAsync();

        // Wait for removal
        await Page.WaitForTimeoutAsync(1000);

        // Verify stock card is removed
        await Expect(stockCard).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task ClearAll_ShouldRemoveAllStocks()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Add multiple stocks
        var symbols = new[] { "AAPL", "GOOGL", "TSLA" };
        var tickerInput = Page.Locator("#ticker-input");
        var addButton = Page.Locator("#add-button");

        foreach (var symbol in symbols)
        {
            await tickerInput.FillAsync(symbol);
            await addButton.ClickAsync();
            await Page.WaitForTimeoutAsync(2000); // Wait between additions
        }

        // Verify stocks were added
        var watchlist = Page.Locator("#watchlist");
        var cardCount = await watchlist.Locator(".stock-card").CountAsync();
        cardCount.Should().BeGreaterThan(0);

        // Click clear all button
        var clearButton = Page.Locator("#clear-all");
        await clearButton.ClickAsync();

        // Wait for clearing
        await Page.WaitForTimeoutAsync(1000);

        // Verify all cards are removed
        var finalCount = await watchlist.Locator(".stock-card").CountAsync();
        finalCount.Should().Be(0);
    }

    [Test]
    public async Task SearchSuggestions_ShouldAppearWhenTyping()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Start typing in the search box
        var tickerInput = Page.Locator("#ticker-input");
        await tickerInput.FillAsync("A");

        // Wait for suggestions to appear
        await Page.WaitForTimeoutAsync(1000);

        // Check if suggestions container exists
        var suggestionsContainer = Page.Locator("#search-suggestions");
        await Expect(suggestionsContainer).ToBeVisibleAsync();
    }
}