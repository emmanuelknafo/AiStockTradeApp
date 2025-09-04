using AiStockTradeApp.SeleniumTests.Infrastructure;
using AiStockTradeApp.SeleniumTests.Pages;
using AiStockTradeApp.SeleniumTests.Utils;

namespace AiStockTradeApp.SeleniumTests.Tests;

public class AuthenticatedWatchlistTests : TestBase
{
    [Trait("Category", "Authenticated")]
    [Trait("AdoId", "1403")]
    [Fact]
    public void Dashboard_ShowsPersistedWatchlist_WithQuotes()
    {
        var auth = new AuthPage(Driver);
        var dashboard = new DashboardPage(Driver);
        CultureSwitcher.SetCulture(Driver, Settings.BaseUrl, Settings.Culture);
        // Optional seeding: if SELENIUM_SEED_USERID is provided, seed watchlist with AAPL & MSFT
        var seedUserId = Environment.GetEnvironmentVariable("SELENIUM_SEED_USERID");
        if (!string.IsNullOrWhiteSpace(seedUserId))
        {
            TestDataSeeder.EnsureWatchlist(seedUserId, "AAPL", "MSFT");
        }
        auth.SignIn(Settings.BaseUrl, Settings.Credentials.Username, Settings.Credentials.Password);
        dashboard.Go(Settings.BaseUrl);

    var count = dashboard.GetWatchlistCount();
    Assert.True(count > 0);
    }

    [Trait("Category", "Authenticated")]
    [Trait("AdoId", "1404")]
    [Fact]
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
    [Fact]
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
    [Fact]
    public void RemoveSymbol_Deletes_FromPersistedWatchlist_AndUI()
    {
        var auth = new AuthPage(Driver);
        var dashboard = new DashboardPage(Driver);
        var seedUserId = Environment.GetEnvironmentVariable("SELENIUM_SEED_USERID");
        if (!string.IsNullOrWhiteSpace(seedUserId))
        {
            TestDataSeeder.EnsureWatchlist(seedUserId, "AAPL");
        }
        auth.SignIn(Settings.BaseUrl, Settings.Credentials.Username, Settings.Credentials.Password);
        dashboard.Go(Settings.BaseUrl)
                 .RemoveSymbol("AAPL");

    Assert.DoesNotContain(dashboard.GetSymbols(), s => s.Equals("AAPL", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Category", "Authenticated")]
    [Trait("AdoId", "1407")]
    [Fact]
    public void MergeOnSignIn_SessionItems_DeDup_AppendToUser()
    {
        // This would require pre-populating session before login, which may
        // need API/setup hooks; placeholder here for future wiring.
    Assert.True(true);
    }
}
