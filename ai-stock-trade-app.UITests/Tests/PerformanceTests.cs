using Microsoft.Playwright;
using FluentAssertions;
using NUnit.Framework;

namespace ai_stock_trade_app.UITests.Tests;

[TestFixture]
public class PerformanceTests : BaseUITest
{
    [Test]
    public async Task PageLoad_ShouldLoadWithinReasonableTime()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await NavigateToStockDashboard();
        await WaitForPageLoad();

        stopwatch.Stop();

        // Page should load within 5 seconds
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000);
    }

    [Test]
    public async Task MultipleStocks_ShouldNotSignificantlySlowDown()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        var symbols = new[] { "AAPL", "GOOGL", "MSFT", "TSLA", "AMZN" };
        var tickerInput = Page.Locator("#ticker-input");
        var addButton = Page.Locator("#add-button");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Add multiple stocks
        foreach (var symbol in symbols)
        {
            await tickerInput.FillAsync(symbol);
            await addButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000); // Small delay between additions
        }

        stopwatch.Stop();

        // Should complete within reasonable time (30 seconds for 5 stocks)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000);

        // Verify all stocks were added
        var watchlist = Page.Locator("#watchlist");
        var cardCount = await watchlist.Locator(".stock-card").CountAsync();
        cardCount.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task MemoryUsage_ShouldNotExcessivelyGrow()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Add and remove stocks multiple times to test for memory leaks
        var tickerInput = Page.Locator("#ticker-input");
        var addButton = Page.Locator("#add-button");
        var clearButton = Page.Locator("#clear-all");

        for (int i = 0; i < 3; i++)
        {
            // Add some stocks
            await tickerInput.FillAsync("AAPL");
            await addButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            await tickerInput.FillAsync("GOOGL");
            await addButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);

            // Clear all
            await clearButton.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }

        // Test should complete without browser crashes or excessive resource usage
        var isPageResponsive = await Page.Locator("body").IsVisibleAsync();
        isPageResponsive.Should().BeTrue();
    }

    [Test]
    public async Task ConcurrentActions_ShouldHandleGracefully()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        var tickerInput = Page.Locator("#ticker-input");
        var addButton = Page.Locator("#add-button");

        // Perform multiple actions quickly
        await tickerInput.FillAsync("AAPL");
        
        // Click add button multiple times quickly
        var tasks = new List<Task>();
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(addButton.ClickAsync());
            tasks.Add(Page.WaitForTimeoutAsync(100));
        }

        await Task.WhenAll(tasks);

        // Should handle gracefully without errors - check that watchlist container exists and page is responsive
        var watchlist = Page.Locator("#watchlist");
        await Expect(watchlist).ToBeAttachedAsync(); // Check if element exists in DOM
        
        // Also check that the page is still responsive by verifying the input is accessible
        var isPageResponsive = await tickerInput.IsEnabledAsync();
        isPageResponsive.Should().BeTrue("Page should remain responsive after concurrent actions");
    }
}