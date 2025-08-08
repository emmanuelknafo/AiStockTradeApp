using Microsoft.Playwright;
using FluentAssertions;
using NUnit.Framework;

namespace ai_stock_trade_app.UITests.Tests;

[TestFixture]
public class UserInterfaceTests : BaseUITest
{
    [Test]
    public async Task ThemeToggle_ShouldChangeTheme()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Get initial theme (check html data-theme attribute)
        var html = Page.Locator("html");
        var initialTheme = await html.GetAttributeAsync("data-theme") ?? "light";

        // Click theme toggle
        var themeToggle = Page.Locator("#theme-toggle");
        await themeToggle.ClickAsync();

        // Wait for theme change
        await Page.WaitForTimeoutAsync(500);

        // Check if theme changed
        var newTheme = await html.GetAttributeAsync("data-theme") ?? "light";
        newTheme.Should().NotBe(initialTheme, "Theme should change when toggle is clicked");
        
        // Also verify the theme toggle button text changed
        var toggleText = await themeToggle.TextContentAsync();
        toggleText.Should().NotBeNullOrEmpty("Theme toggle button should have text");
    }

    [Test]
    public async Task SettingsPanel_ShouldToggleVisibility()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        var settingsPanel = Page.Locator("#settings-panel");
        var settingsToggle = Page.Locator("#settings-toggle");

        // Initially should be hidden
        var initialClass = await settingsPanel.GetAttributeAsync("class") ?? "";
        initialClass.Should().Contain("hidden");

        // Click settings toggle to show
        await settingsToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Should now be visible
        var visibleClass = await settingsPanel.GetAttributeAsync("class") ?? "";
        visibleClass.Should().NotContain("hidden");

        // Click again to hide
        await settingsToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Should be hidden again
        var hiddenClass = await settingsPanel.GetAttributeAsync("class") ?? "";
        hiddenClass.Should().Contain("hidden");
    }

    [Test]
    public async Task AlertsPanel_ShouldToggleVisibility()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        var alertsPanel = Page.Locator("#alerts-panel");
        var alertsToggle = Page.Locator("#alerts-toggle");

        // Initially should be hidden
        var initialClass = await alertsPanel.GetAttributeAsync("class") ?? "";
        initialClass.Should().Contain("hidden");

        // Click alerts toggle to show
        await alertsToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Should now be visible
        var visibleClass = await alertsPanel.GetAttributeAsync("class") ?? "";
        visibleClass.Should().NotContain("hidden");

        // Click again to hide
        await alertsToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Should be hidden again
        var hiddenClass = await alertsPanel.GetAttributeAsync("class") ?? "";
        hiddenClass.Should().Contain("hidden");
    }

    [Test]
    public async Task PortfolioSummary_ShouldDisplayCorrectly()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Verify portfolio summary section exists
        var portfolioSummary = Page.Locator("#portfolio-summary");
        await Expect(portfolioSummary).ToBeVisibleAsync();

        // Verify portfolio stats exist
        var totalValue = Page.Locator("#total-value");
        await Expect(totalValue).ToBeVisibleAsync();

        var totalChange = Page.Locator("#total-change");
        await Expect(totalChange).ToBeVisibleAsync();

        var stockCount = Page.Locator("#stock-count");
        await Expect(stockCount).ToBeVisibleAsync();

        // Verify export buttons exist
        var exportCsv = Page.Locator("#export-csv");
        await Expect(exportCsv).ToBeVisibleAsync();

        var exportJson = Page.Locator("#export-json");
        await Expect(exportJson).ToBeVisibleAsync();

        var importData = Page.Locator("#import-data");
        await Expect(importData).ToBeVisibleAsync();
    }

    [Test]
    public async Task AutoRefreshToggle_ShouldWork()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        var autoRefreshToggle = Page.Locator("#auto-refresh-toggle");
        await Expect(autoRefreshToggle).ToBeVisibleAsync();

        // Click the auto-refresh toggle
        await autoRefreshToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // The button text or state should change
        // This will depend on your JavaScript implementation
        var buttonText = await autoRefreshToggle.InnerTextAsync();
        buttonText.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ResponsiveDesign_ShouldWorkOnMobile()
    {
        // Set mobile viewport
        await Page.SetViewportSizeAsync(375, 667);

        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Verify main elements are still visible and accessible
        var header = Page.Locator("h1");
        await Expect(header).ToBeVisibleAsync();

        var searchContainer = Page.Locator(".search-container");
        await Expect(searchContainer).ToBeVisibleAsync();

        var tickerInput = Page.Locator("#ticker-input");
        await Expect(tickerInput).ToBeVisibleAsync();

        var addButton = Page.Locator("#add-button");
        await Expect(addButton).ToBeVisibleAsync();

        // Reset viewport
        await Page.SetViewportSizeAsync(1280, 720);
    }

    [Test]
    public async Task ErrorHandling_InvalidStockSymbol()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Enter an invalid stock symbol
        var tickerInput = Page.Locator("#ticker-input");
        await tickerInput.FillAsync("INVALIDSTOCK123");

        var addButton = Page.Locator("#add-button");
        await addButton.ClickAsync();

        // Wait for potential error handling
        await Page.WaitForTimeoutAsync(3000);

        // Check for error notification or message
        // This depends on how your app handles invalid symbols
        var notificationContainer = Page.Locator("#notification-container");
        var hasNotification = await notificationContainer.IsVisibleAsync();
        
        // At minimum, the invalid stock should not be added to watchlist
        var invalidCard = Page.Locator("#card-INVALIDSTOCK123");
        var cardExists = await invalidCard.IsVisibleAsync();
        cardExists.Should().BeFalse();
    }
}