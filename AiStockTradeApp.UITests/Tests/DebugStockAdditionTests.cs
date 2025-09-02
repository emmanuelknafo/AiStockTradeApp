using Microsoft.Playwright;
using FluentAssertions;
using NUnit.Framework;
using AiStockTradeApp.UITests.PageObjects;

namespace AiStockTradeApp.UITests.Tests;

[TestFixture]
public class DebugStockAdditionTests : BaseUITest
{
    private StockDashboardPage _dashboardPage;

    [SetUp]
    public async Task Setup()
    {
        _dashboardPage = new StockDashboardPage(Page, BaseUrl);
        await NavigateToStockDashboard();
        await _dashboardPage.WaitForPageReady();
    }

    [Test]
    public async Task DebugAddStock_ShouldShowWhatHappens()
    {
        // Take a screenshot before adding stock
        await Page.ScreenshotAsync(new() { Path = "before-add-stock.png" });
        
        // Fill the ticker input
        await _dashboardPage.TickerInput.FillAsync("AAPL");
        await Page.WaitForTimeoutAsync(500);
        
        // Take screenshot after filling input
        await Page.ScreenshotAsync(new() { Path = "after-fill-input.png" });
        
        // Click the add button
        await _dashboardPage.AddButton.ClickAsync();
        
        // Wait a bit and take another screenshot
        await Page.WaitForTimeoutAsync(2000);
        await Page.ScreenshotAsync(new() { Path = "after-click-add.png" });
        
        // Check for any notifications
        var notifications = await Page.Locator(".notification").AllAsync();
        Console.WriteLine($"Found {notifications.Count} notifications");
        
        foreach (var notification in notifications)
        {
            var text = await notification.TextContentAsync();
            var className = await notification.GetAttributeAsync("class");
            Console.WriteLine($"Notification: '{text}' with class '{className}'");
        }
        
        // Check if input was cleared
        var inputValue = await _dashboardPage.TickerInput.InputValueAsync();
        Console.WriteLine($"Input value after add: '{inputValue}'");
        
        // Check if any stock cards exist
        var stockCards = await Page.Locator(".stock-card").AllAsync();
        Console.WriteLine($"Found {stockCards.Count} stock cards");
        
        // Wait longer and check again
        await Page.WaitForTimeoutAsync(5000);
        await Page.ScreenshotAsync(new() { Path = "after-wait.png" });
        
        var stockCardsAfterWait = await Page.Locator(".stock-card").AllAsync();
        Console.WriteLine($"Found {stockCardsAfterWait.Count} stock cards after wait");
        
        // Check the page URL to see if we're still on the same page
        var currentUrl = Page.Url;
        Console.WriteLine($"Current URL: {currentUrl}");
    }
}
