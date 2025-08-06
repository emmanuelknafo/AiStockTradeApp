using ai_stock_trade_app.UITests.PageObjects;
using FluentAssertions;
using NUnit.Framework;

namespace ai_stock_trade_app.UITests.Tests;

[TestFixture]
public class StockDashboardPageObjectTests : BaseUITest
{
    private StockDashboardPage _dashboardPage = null!;

    [SetUp]
    public async Task SetUpPage()
    {
        await base.Setup();
        _dashboardPage = new StockDashboardPage(Page, BaseUrl);
        await _dashboardPage.NavigateTo();
    }

    [Test]
    public async Task PageLoad_ShouldDisplayAllRequiredElements()
    {
        // Verify page loaded correctly
        await Expect(_dashboardPage.Header).ToBeVisibleAsync();
        await Expect(_dashboardPage.Header).ToHaveTextAsync("AI-Powered Stock Tracker");

        // Verify main controls
        await Expect(_dashboardPage.TickerInput).ToBeVisibleAsync();
        await Expect(_dashboardPage.AddButton).ToBeVisibleAsync();
        await Expect(_dashboardPage.ClearAllButton).ToBeVisibleAsync();

        // Verify toggle buttons
        await Expect(_dashboardPage.ThemeToggle).ToBeVisibleAsync();
        await Expect(_dashboardPage.SettingsToggle).ToBeVisibleAsync();
        await Expect(_dashboardPage.AlertsToggle).ToBeVisibleAsync();
        await Expect(_dashboardPage.AutoRefreshToggle).ToBeVisibleAsync();

        // Verify main sections
        await Expect(_dashboardPage.Watchlist).ToBeVisibleAsync();
        await Expect(_dashboardPage.PortfolioSummary).ToBeVisibleAsync();
    }

    [Test]
    public async Task AddStock_UsingPageObject_ShouldWork()
    {
        // Add a stock using page object methods
        await _dashboardPage.AddStock("AAPL");

        // Verify stock was added
        var isInWatchlist = await _dashboardPage.IsStockInWatchlist("AAPL");
        isInWatchlist.Should().BeTrue();

        // Wait for stock data to load and verify price is displayed
        await _dashboardPage.WaitForStockToLoad("AAPL");
        var price = await _dashboardPage.GetStockPrice("AAPL");
        price.Should().NotBeNullOrEmpty();
        price.Should().NotContain("Loading");
    }

    [Test]
    public async Task RemoveStock_UsingPageObject_ShouldWork()
    {
        // First add a stock
        await _dashboardPage.AddStock("MSFT");
        var isAdded = await _dashboardPage.IsStockInWatchlist("MSFT");
        isAdded.Should().BeTrue();

        // Remove the stock
        await _dashboardPage.RemoveStock("MSFT");

        // Verify stock was removed
        var isRemoved = await _dashboardPage.IsStockInWatchlist("MSFT");
        isRemoved.Should().BeFalse();
    }

    [Test]
    public async Task SettingsPanel_UsingPageObject_ShouldToggle()
    {
        // Initially should be hidden
        var initiallyVisible = await _dashboardPage.IsSettingsPanelVisible();
        initiallyVisible.Should().BeFalse();

        // Toggle to show
        await _dashboardPage.ToggleSettings();
        var nowVisible = await _dashboardPage.IsSettingsPanelVisible();
        nowVisible.Should().BeTrue();

        // Toggle to hide
        await _dashboardPage.ToggleSettings();
        var hiddenAgain = await _dashboardPage.IsSettingsPanelVisible();
        hiddenAgain.Should().BeFalse();
    }

    [Test]
    public async Task AlertsPanel_UsingPageObject_ShouldToggle()
    {
        // Initially should be hidden
        var initiallyVisible = await _dashboardPage.IsAlertsPanelVisible();
        initiallyVisible.Should().BeFalse();

        // Toggle to show
        await _dashboardPage.ToggleAlerts();
        var nowVisible = await _dashboardPage.IsAlertsPanelVisible();
        nowVisible.Should().BeTrue();

        // Toggle to hide
        await _dashboardPage.ToggleAlerts();
        var hiddenAgain = await _dashboardPage.IsAlertsPanelVisible();
        hiddenAgain.Should().BeFalse();
    }

    [Test]
    public async Task SearchSuggestions_UsingPageObject_ShouldAppear()
    {
        // Type in search to trigger suggestions
        await _dashboardPage.TypeInSearch("A");

        // Verify suggestions appear
        var suggestionsVisible = await _dashboardPage.AreSuggestionsVisible();
        suggestionsVisible.Should().BeTrue();
    }

    [Test]
    public async Task PortfolioStats_UsingPageObject_ShouldDisplay()
    {
        // Get initial portfolio stats
        var totalValue = await _dashboardPage.GetTotalPortfolioValue();
        var stockCount = await _dashboardPage.GetStockCount();

        totalValue.Should().NotBeNullOrEmpty();
        stockCount.Should().NotBeNullOrEmpty();

        // Add a stock and verify count changes
        await _dashboardPage.AddStock("GOOGL");
        await _dashboardPage.WaitForStockToLoad("GOOGL");

        var newStockCount = await _dashboardPage.GetStockCount();
        var newStockCountInt = int.Parse(newStockCount);
        var originalStockCountInt = int.Parse(stockCount);

        newStockCountInt.Should().BeGreaterThan(originalStockCountInt);
    }

    [Test]
    public async Task ClearAllStocks_UsingPageObject_ShouldWork()
    {
        // Add some stocks first
        await _dashboardPage.AddStock("AAPL");
        await _dashboardPage.AddStock("TSLA");

        // Verify stocks were added
        var countAfterAdding = await _dashboardPage.GetStockCardCount();
        countAfterAdding.Should().BeGreaterThan(0);

        // Clear all stocks
        await _dashboardPage.ClearAllStocks();

        // Verify all stocks were removed
        var countAfterClearing = await _dashboardPage.GetStockCardCount();
        countAfterClearing.Should().Be(0);
    }

    [Test]
    public async Task ThemeToggle_UsingPageObject_ShouldWork()
    {
        // Get initial body class
        var body = Page.Locator("body");
        var initialClass = await body.GetAttributeAsync("class") ?? "";

        // Toggle theme
        await _dashboardPage.ToggleTheme();

        // Verify theme changed
        var newClass = await body.GetAttributeAsync("class") ?? "";
        newClass.Should().NotBe(initialClass);
    }

    [Test]
    public async Task ExportFunctionality_UsingPageObject_ShouldBeAccessible()
    {
        // Add a stock first to have data to export
        await _dashboardPage.AddStock("AMZN");
        await _dashboardPage.WaitForStockToLoad("AMZN");

        // Test export buttons are clickable (we won't verify download in UI tests)
        await _dashboardPage.ClickExportCsv();
        await Page.WaitForTimeoutAsync(1000);

        await _dashboardPage.ClickExportJson();
        await Page.WaitForTimeoutAsync(1000);

        // If we reach here without errors, export functionality is accessible
        var isPageResponsive = await Page.Locator("body").IsVisibleAsync();
        isPageResponsive.Should().BeTrue();
    }
}