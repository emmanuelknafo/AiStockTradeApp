using AiStockTradeApp.SeleniumTests.Infrastructure;
using AiStockTradeApp.SeleniumTests.Pages;
using AiStockTradeApp.SeleniumTests.Utils;

namespace AiStockTradeApp.SeleniumTests.Tests;

public class AnonymousWatchlistTests : TestBase
{
    [Trait("Category", "Anonymous")]
    [Trait("AdoId", "1408")]
    [Fact(Skip = "Needs app running")]
    public void Dashboard_ShowsSessionWatchlist_WithQuotes()
    {
        var dashboard = new DashboardPage(Driver);
        CultureSwitcher.SetCulture(Driver, Settings.BaseUrl, Settings.Culture);
        dashboard.Go(Settings.BaseUrl)
                 .AddSymbol("TSLA")
                 .AddSymbol("AMZN");

    var symbols = dashboard.GetSymbols();
    Assert.Contains(symbols, s => s.Equals("TSLA", StringComparison.OrdinalIgnoreCase));
    Assert.Contains(symbols, s => s.Equals("AMZN", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Category", "Anonymous")]
    [Trait("AdoId", "1409")]
    [Fact(Skip = "Needs app running")]
    public void EmptySession_ShowsLocalizedEmptyState_AndAddCta()
    {
        var dashboard = new DashboardPage(Driver);
        dashboard.Go(Settings.BaseUrl);
    Assert.True(dashboard.IsEmptyStateVisible());
    }

    [Trait("Category", "Anonymous")]
    [Trait("AdoId", "1410")]
    [Fact(Skip = "Needs app running")]
    public void AddRemoveSymbol_AffectsSessionOnly_AndPreventsDuplicates()
    {
        var dashboard = new DashboardPage(Driver);
        dashboard.Go(Settings.BaseUrl)
                 .AddSymbol("AAPL")
                 .AddSymbol("AAPL");

    Assert.Equal(1, dashboard.GetSymbols().Count(s => s.Equals("AAPL", StringComparison.OrdinalIgnoreCase)));

        dashboard.RemoveSymbol("AAPL");
    Assert.DoesNotContain(dashboard.GetSymbols(), s => s.Equals("AAPL", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Category", "Anonymous")]
    [Trait("AdoId", "1411")]
    [Fact(Skip = "Needs credentials and seeded data")]
    public void ContextSwitch_OnSignOut_UsesSessionList()
    {
        var auth = new AuthPage(Driver);
        var dashboard = new DashboardPage(Driver);

        dashboard.Go(Settings.BaseUrl)
                 .AddSymbol("MSFT");

        auth.SignIn(Settings.BaseUrl, Settings.Credentials.Username, Settings.Credentials.Password);
        dashboard.Go(Settings.BaseUrl);

        auth.SignOut(Settings.BaseUrl);
        dashboard.Go(Settings.BaseUrl);

    Assert.Contains(dashboard.GetSymbols(), s => s.Equals("MSFT", StringComparison.OrdinalIgnoreCase));
    }
}
