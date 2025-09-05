using Microsoft.AspNetCore.Mvc;
using AiStockTradeApp.Controllers;

#pragma warning disable CS0618 // Suppress obsolete StockController usage warnings – intentional redirect coverage

namespace AiStockTradeApp.Tests.Controllers;

/// <summary>
/// Tests for the deprecated StockController which now only provides redirects to UserStockController.
/// These tests verify that the legacy routes still work for backward compatibility.
/// </summary>
public class StockControllerTests
{
    private readonly StockController _controller = new();

    [Fact]
    public void Dashboard_ShouldRedirect_ToUserStockDashboard()
    {
        // Act
        var result = _controller.Dashboard();
        
        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirectResult = result as RedirectToActionResult;
        redirectResult!.ActionName.Should().Be("Dashboard");
        redirectResult.ControllerName.Should().Be("UserStock");
    }

    [Fact]
    public void AddStock_ShouldRedirect_ToUserStockAddStock()
    {
        // Act
        var result = _controller.AddStock();
        
        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirectResult = result as RedirectToActionResult;
        redirectResult!.ActionName.Should().Be("AddStock");
        redirectResult.ControllerName.Should().Be("UserStock");
    }

    [Fact]
    public void RemoveStock_ShouldRedirect_ToUserStockRemoveStock()
    {
        // Act
        var result = _controller.RemoveStock();
        
        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirectResult = result as RedirectToActionResult;
        redirectResult!.ActionName.Should().Be("RemoveStock");
        redirectResult.ControllerName.Should().Be("UserStock");
    }

    [Fact]
    public void ClearWatchlist_ShouldRedirect_ToUserStockClearWatchlist()
    {
        // Act
        var result = _controller.ClearWatchlist();
        
        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        var redirectResult = result as RedirectToActionResult;
        redirectResult!.ActionName.Should().Be("ClearWatchlist");
        redirectResult.ControllerName.Should().Be("UserStock");
    }
}

#pragma warning restore CS0618
