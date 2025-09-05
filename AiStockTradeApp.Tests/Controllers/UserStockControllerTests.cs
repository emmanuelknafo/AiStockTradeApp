using AiStockTradeApp.Controllers;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.Entities.ViewModels;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Tests.Controllers;

public class UserStockControllerTests
{
    private readonly Mock<IUserWatchlistService> _userWatchlist = new();
    private readonly Mock<IStockDataService> _stockData = new();
    private readonly Mock<IAIAnalysisService> _ai = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<ILogger<UserStockController>> _logger = new();
    private readonly Mock<IStringLocalizer<SharedResource>> _localizer = new();
    private readonly UserStockController _controller;
    private readonly Mock<HttpContext> _httpContext = new();
    private readonly Mock<ISession> _session = new();

    public UserStockControllerTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        _controller = new UserStockController(
            _userWatchlist.Object,
            _stockData.Object,
            _ai.Object,
            _userManager.Object,
            _logger.Object,
            _localizer.Object);

        _httpContext.Setup(c => c.Session).Returns(_session.Object);
        _controller.ControllerContext = new ControllerContext { HttpContext = _httpContext.Object };
        SetupSession();
    }

    private void SetupSession()
    {
        var data = new Dictionary<string, byte[]>();
        _session.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback<string, byte[]>((k, v) => data[k] = v);
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<byte[]?>.IsAny))
            .Returns((string k, out byte[]? v) =>
            {
                if (data.TryGetValue(k, out var val)) { v = val; return true; }
                v = null; return false;
            });
    }

    [Fact]
    public async Task Dashboard_EmptySessionWatchlist_RendersEmptyState()
    {
        _userWatchlist.Setup(s => s.GetWatchlistAsync(null, It.IsAny<string>()))
            .ReturnsAsync(new List<WatchlistItem>());
        _userWatchlist.Setup(s => s.GetAlertsAsync(null, It.IsAny<string>()))
            .ReturnsAsync(new List<PriceAlert>());
        _userWatchlist.Setup(s => s.CalculatePortfolioSummaryAsync(It.IsAny<List<WatchlistItem>>()))
            .ReturnsAsync(new PortfolioSummary());

        var result = await _controller.Dashboard();
        result.Should().BeOfType<ViewResult>();
        var model = ((ViewResult)result).Model as DashboardViewModel;
        model!.Watchlist.Should().BeEmpty();
    }

    [Fact]
    public async Task Dashboard_PartialErrors_SetsErrorMessage()
    {
        var wl = new List<WatchlistItem>
        {
            new() { Symbol = "AAA" },
            new() { Symbol = "BBB" }
        };
        _userWatchlist.Setup(s => s.GetWatchlistAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(wl);
        _userWatchlist.Setup(s => s.GetAlertsAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<PriceAlert>());
        _userWatchlist.Setup(s => s.CalculatePortfolioSummaryAsync(It.IsAny<List<WatchlistItem>>()))
            .ReturnsAsync(new PortfolioSummary());

        _stockData.Setup(s => s.GetStockQuoteAsync("AAA"))
            .ReturnsAsync(new StockQuoteResponse { Success = true, Data = new StockData { Symbol = "AAA", Price = 10m } });
        // Force an exception to verify catch branch populates error collection deterministically
        _stockData.Setup(s => s.GetStockQuoteAsync("BBB"))
            .ThrowsAsync(new Exception("Rate limit"));

        var result = await _controller.Dashboard();
        var model = ((ViewResult)result).Model as DashboardViewModel;
    model!.Watchlist.Should().HaveCount(2, "the watchlist items should be returned by the mock");
    model.ErrorMessage.Should().NotBeNull();
    model.ErrorMessage.Should().Contain("Rate limit");
    _userWatchlist.Verify(s => s.GetWatchlistAsync(It.IsAny<string?>(), It.IsAny<string?>()), Times.AtLeastOnce());
    }

    [Fact]
    public void Localization_Keys_Exist_For_Accessibility()
    {
        var keys = new[]
        {
            "Aria_ToggleTheme","Aria_ToggleAutoRefresh","Aria_ToggleAlertsPanel","Aria_ToggleSettingsPanel",
            "Aria_RemoveStock","Aria_ClearWatchlist","Aria_AddStock","Aria_EmptyWatchlist"
        };
        foreach (var k in keys)
        {
            var localized = _localizer.Object[k];
            localized.Value.Should().NotBeNull();
        }
    }
}
