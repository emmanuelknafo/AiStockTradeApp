using Microsoft.AspNetCore.Mvc;
using AiStockTradeApp.Controllers;

#pragma warning disable CS0618 // Suppress obsolete StockController usage warnings – intentional redirect coverage

namespace AiStockTradeApp.Tests.Controllers;

// The legacy StockController is now a thin redirect layer. These tests verify
// that public actions return RedirectToActionResult targeting UserStockController.
public class StockControllerTests
{
    private readonly StockController _controller = new();

    [Fact]
    public void Dashboard_ShouldRedirect_ToUserStockDashboard()
    {
        var result = _controller.Dashboard();
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ControllerName.Should().Be("UserStock");
    }

    [Fact]
    public void AddStock_ShouldRedirect()
    {
        var result = _controller.AddStock();
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("AddStock");
    }

    [Fact]
    public void RemoveStock_ShouldRedirect()
    {
        var result = _controller.RemoveStock();
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("RemoveStock");
    }

    [Fact]
    public void ClearWatchlist_ShouldRedirect()
    {
        var result = _controller.ClearWatchlist();
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("ClearWatchlist");
    }
}

#pragma warning restore CS0618
