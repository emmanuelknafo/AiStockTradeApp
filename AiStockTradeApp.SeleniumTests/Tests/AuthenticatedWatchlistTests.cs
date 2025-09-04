using AiStockTradeApp.SeleniumTests.Infrastructure;
using AiStockTradeApp.SeleniumTests.Pages;
using AiStockTradeApp.SeleniumTests.Utils;

namespace AiStockTradeApp.SeleniumTests.Tests;

public class AuthenticatedWatchlistTests : TestBase
{
    [Trait("Category", "Authenticated")]
    [Trait("AdoId", "1403")]
    [Fact(Skip = "Needs app URL and seeded user credentials")]
    public void Dashboard_ShowsPersistedWatchlist_WithQuotes()
    {
        var auth = new AuthPage(Driver);
        var dashboard = new DashboardPage(Driver);
        CultureSwitcher.SetCulture(Driver, Settings.BaseUrl, Settings.Culture);
        auth.SignIn(Settings.BaseUrl, Settings.Credentials.Username, Settings.Credentials.Password);
        dashboard.Go(Settings.BaseUrl);

    var count = dashboard.GetWatchlistCount();
    Assert.True(count > 0);
    }

    [Trait("Category", "Authenticated")]
    [Trait("AdoId", "1404")]
    [Fact(Skip = "Needs seeded empty watchlist user")]
    public void EmptyWatchlist_ShowsLocalizedEmptyState_AndAddCta()
    {
        var auth = new AuthPage(Driver);
        var dashboard = new DashboardPage(Driver);
        CultureSwitcher.SetCulture(Driver, Settings.BaseUrl, Settings.Culture);
        auth.SignIn(Settings.BaseUrl, Settings.Credentials.Username, Settings.Credentials.Password);
        dashboard.Go(Settings.BaseUrl);

    Assert.True(dashboard.IsEmptyStateVisible());
    }

    [Trait("Category", "Authenticated")]
    [Trait("AdoId", "1405")]
    [Fact(Skip = "Requires seeded user and UI hooks")]
    public void AddSymbol_Validates_Persists_AndPreventsDuplicates()
    {
        var auth = new AuthPage(Driver);
        var dashboard = new DashboardPage(Driver);
        auth.SignIn(Settings.BaseUrl, Settings.Credentials.Username, Settings.Credentials.Password);
        dashboard.Go(Settings.BaseUrl)
                 .AddSymbol("GOOG");

    Assert.Contains(dashboard.GetSymbols(), s => s.Equals("GOOG", StringComparison.OrdinalIgnoreCase));

        dashboard.AddSymbol("GOOG");
        // Assert duplicate prevention UI message in a future implementation
    }

    [Trait("Category", "Authenticated")]
    [Trait("AdoId", "1406")]
    [Fact(Skip = "Requires seeded user with AAPL")]
    public void RemoveSymbol_Deletes_FromPersistedWatchlist_AndUI()
    {
        var auth = new AuthPage(Driver);
        var dashboard = new DashboardPage(Driver);
        auth.SignIn(Settings.BaseUrl, Settings.Credentials.Username, Settings.Credentials.Password);
        dashboard.Go(Settings.BaseUrl)
                 .RemoveSymbol("AAPL");

    Assert.DoesNotContain(dashboard.GetSymbols(), s => s.Equals("AAPL", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Category", "Authenticated")]
    [Trait("AdoId", "1407")]
    [Fact(Skip = "Requires session setup and user with AAPL")]
    public void MergeOnSignIn_SessionItems_DeDup_AppendToUser()
    {
        // This would require pre-populating session before login, which may
        // need API/setup hooks; placeholder here for future wiring.
    Assert.True(true);
    }
}
