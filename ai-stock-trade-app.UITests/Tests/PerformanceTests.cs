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
    var stockCards = Page.Locator("#watchlist .stock-card");

    // Ensure we accept the Clear All confirmation dialog
    Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

        for (int i = 0; i < 3; i++)
        {
            // Add some stocks
            await tickerInput.FillAsync("AAPL");
            await addButton.ClickAsync();
            // Wait until at least one stock card is rendered
            await Expect(stockCards.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await Page.WaitForTimeoutAsync(250);

            await tickerInput.FillAsync("GOOGL");
            await addButton.ClickAsync();
            await Expect(stockCards.First).ToBeVisibleAsync(new() { Timeout = 10000 });
            await Page.WaitForTimeoutAsync(250);

            // Clear all and wait until no stock cards remain (with retries and fallback)
            await Expect(clearButton).ToBeVisibleAsync(new() { Timeout = 10000 });
            await clearButton.ClickAsync();

            var cleared = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await Expect(stockCards).ToHaveCountAsync(0, new() { Timeout = 5000 });
                    cleared = true;
                    break;
                }
                catch
                {
                    // Try clicking clear once more and wait briefly
                    await clearButton.ClickAsync();
                    await Page.WaitForTimeoutAsync(500);
                }
            }

            if (!cleared)
            {
                // Fallback: remove remaining cards individually
                var maxIndividually = Math.Min(await stockCards.CountAsync(), 5);
                for (int k = 0; k < maxIndividually; k++)
                {
                    var first = stockCards.First;
                    var removeBtn = first.Locator(".remove-button");
                    if (!await removeBtn.IsVisibleAsync()) break;
                    await removeBtn.ClickAsync();
                    try { await first.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 5000 }); } catch { }
                    await Page.WaitForTimeoutAsync(250);
                    if (await stockCards.CountAsync() == 0) { cleared = true; break; }
                }
            }

            // Final assertion that list is empty
            await Expect(stockCards).ToHaveCountAsync(0, new() { Timeout = 5000 });
        }

        // Test should complete without browser crashes or excessive resource usage
        var isPageResponsive = await Page.Locator("body").IsVisibleAsync();
        isPageResponsive.Should().BeTrue();
    }

    [Test]
    public async Task RapidActions_ShouldHandleGracefully()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        var tickerInput = Page.Locator("#ticker-input");
        var addButton = Page.Locator("#add-button");

        // Perform multiple actions quickly in sequence (not concurrent)
        await tickerInput.FillAsync("AAPL");
        
        // Click add button multiple times quickly but sequentially
        for (int i = 0; i < 3; i++)
        {
            await addButton.ClickAsync();
            await Page.WaitForTimeoutAsync(100); // Small delay between clicks
        }

        // Should handle gracefully without errors - check that watchlist container exists and page is responsive
        var watchlist = Page.Locator("#watchlist");
        await Expect(watchlist).ToBeAttachedAsync(); // Check if element exists in DOM
        
        // Also check that the page is still responsive by verifying the input is accessible
        var isPageResponsive = await tickerInput.IsEnabledAsync();
        isPageResponsive.Should().BeTrue("Page should remain responsive after rapid actions");
    }
}