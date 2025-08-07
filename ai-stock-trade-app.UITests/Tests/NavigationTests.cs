using Microsoft.Playwright;
using FluentAssertions;
using NUnit.Framework;

namespace ai_stock_trade_app.UITests.Tests;

[TestFixture]
public class NavigationTests : BaseUITest
{
    [Test]
    public async Task HomePage_ShouldRedirectToStockDashboard()
    {
        // Navigate to home page
        await NavigateToHomePage();
        await WaitForPageLoad();

        // Should redirect to Stock Dashboard or at least show the dashboard content
        var currentUrl = Page.Url;
        
        // Check URL contains dashboard or verify we're on the right page by checking for dashboard elements
        var isDashboardUrl = currentUrl.Contains("/Stock/Dashboard") || currentUrl.Contains("/dashboard", StringComparison.OrdinalIgnoreCase);
        var hasDashboardContent = await Page.Locator("h1").GetByText("AI-Powered Stock Tracker").IsVisibleAsync();
        
        (isDashboardUrl || hasDashboardContent).Should().BeTrue("Should be on dashboard page or redirected to dashboard");
    }

    [Test]
    public async Task StockDashboard_ShouldLoadSuccessfully()
    {
        // Navigate to Stock Dashboard
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Verify page title or content
        try
        {
            var title = await Page.TitleAsync();
            title.Should().Contain("AI-Powered Stock Tracker");
        }
        catch
        {
            // If title check fails, verify by content
            var headerElement = Page.Locator("h1");
            await Expect(headerElement).ToBeVisibleAsync();
            await Expect(headerElement).ToContainTextAsync("AI-Powered Stock Tracker");
        }

        // Verify main header is visible
        var header = Page.Locator("h1");
        await Expect(header).ToBeVisibleAsync();
        await Expect(header).ToContainTextAsync("AI-Powered Stock Tracker");
    }

    [Test]
    public async Task StockDashboard_ShouldHaveRequiredElements()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Verify search bar exists
        var tickerInput = Page.Locator("#ticker-input");
        await Expect(tickerInput).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 10000 });
        
        var placeholder = await tickerInput.GetAttributeAsync("placeholder");
        placeholder.Should().NotBeNullOrEmpty("Ticker input should have a placeholder");

        // Verify add button exists
        var addButton = Page.Locator("#add-button");
        await Expect(addButton).ToBeVisibleAsync();
        
        var addButtonText = await addButton.TextContentAsync();
        addButtonText.Should().Contain("Add", "Add button should contain 'Add' text");

        // Verify clear all button exists
        var clearButton = Page.Locator("#clear-all");
        await Expect(clearButton).ToBeVisibleAsync();
        
        var clearButtonText = await clearButton.TextContentAsync();
        clearButtonText.Should().Contain("Clear", "Clear button should contain 'Clear' text");

        // Verify control buttons exist
        var controlButtons = new[]
        {
            ("#theme-toggle", "Theme toggle"),
            ("#auto-refresh-toggle", "Auto refresh toggle"),
            ("#alerts-toggle", "Alerts toggle"),
            ("#settings-toggle", "Settings toggle")
        };

        foreach (var (selector, description) in controlButtons)
        {
            var button = Page.Locator(selector);
            await Expect(button).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
        }
    }

    [Test]
    public async Task PrivacyPage_ShouldLoadSuccessfully()
    {
        // Navigate to Privacy page
        await Page.GotoAsync($"{BaseUrl}/Home/Privacy", new PageGotoOptions { Timeout = 10000 });
        await WaitForPageLoad();

        // Verify page loads without errors
        var body = Page.Locator("body");
        await Expect(body).ToBeVisibleAsync();

        // Check that we're on the Privacy page
        var currentUrl = Page.Url;
        currentUrl.Should().Contain("/Home/Privacy", "Should be on the Privacy page");
        
        // Verify the page has content (not an error page)
        var hasContent = await body.TextContentAsync();
        hasContent.Should().NotBeNullOrEmpty("Privacy page should have content");
    }

    [Test]
    public async Task Application_ShouldHaveResponsiveLayout()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Test desktop layout
        await Page.SetViewportSizeAsync(1280, 720);
        var header = Page.Locator("h1");
        await Expect(header).ToBeVisibleAsync();

        // Test mobile layout
        await Page.SetViewportSizeAsync(375, 667);
        await Expect(header).ToBeVisibleAsync();

        // Verify main elements are still accessible on mobile
        var tickerInput = Page.Locator("#ticker-input");
        await Expect(tickerInput).ToBeVisibleAsync();

        // Reset to desktop
        await Page.SetViewportSizeAsync(1280, 720);
    }
}