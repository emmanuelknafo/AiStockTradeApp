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

        // Should redirect to Stock Dashboard
        var currentUrl = Page.Url;
        currentUrl.Should().Contain("/Stock/Dashboard");
    }

    [Test]
    public async Task StockDashboard_ShouldLoadSuccessfully()
    {
        // Navigate to Stock Dashboard
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Verify page title
        var title = await Page.TitleAsync();
        title.Should().Contain("AI-Powered Stock Tracker");

        // Verify main header is visible
        var header = Page.Locator("h1");
        await Expect(header).ToBeVisibleAsync();
        await Expect(header).ToHaveTextAsync("AI-Powered Stock Tracker");
    }

    [Test]
    public async Task StockDashboard_ShouldHaveRequiredElements()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Verify search bar exists
        var tickerInput = Page.Locator("#ticker-input");
        await Expect(tickerInput).ToBeVisibleAsync();
        await Expect(tickerInput).ToHaveAttributeAsync("placeholder", "Enter ticker symbol (e.g., AAPL)");

        // Verify add button exists
        var addButton = Page.Locator("#add-button");
        await Expect(addButton).ToBeVisibleAsync();
        await Expect(addButton).ToHaveTextAsync("Add Stock");

        // Verify clear all button exists
        var clearButton = Page.Locator("#clear-all");
        await Expect(clearButton).ToBeVisibleAsync();
        await Expect(clearButton).ToHaveTextAsync("Clear All");

        // Verify control buttons exist
        var themeToggle = Page.Locator("#theme-toggle");
        await Expect(themeToggle).ToBeVisibleAsync();

        var autoRefreshToggle = Page.Locator("#auto-refresh-toggle");
        await Expect(autoRefreshToggle).ToBeVisibleAsync();

        var alertsToggle = Page.Locator("#alerts-toggle");
        await Expect(alertsToggle).ToBeVisibleAsync();

        var settingsToggle = Page.Locator("#settings-toggle");
        await Expect(settingsToggle).ToBeVisibleAsync();
    }

    [Test]
    public async Task PrivacyPage_ShouldLoadSuccessfully()
    {
        // Navigate to Privacy page
        await Page.GotoAsync($"{BaseUrl}/Home/Privacy");
        await WaitForPageLoad();

        // Verify page loads without errors
        var response = await Page.Locator("body").IsVisibleAsync();
        response.Should().BeTrue();

        // Check that we're on the Privacy page
        var currentUrl = Page.Url;
        currentUrl.Should().Contain("/Home/Privacy");
    }
}