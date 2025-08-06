using ai_stock_trade_app.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ai_stock_trade_app.Tests.Controllers
{
    public class HomeControllerTests
    {
        private readonly Mock<ILogger<HomeController>> _mockLogger;
        private readonly HomeController _controller;
        private readonly Mock<HttpContext> _mockHttpContext;

        public HomeControllerTests()
        {
            _mockLogger = new Mock<ILogger<HomeController>>();
            _controller = new HomeController(_mockLogger.Object);
            
            // Setup HttpContext for controller
            _mockHttpContext = new Mock<HttpContext>();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = _mockHttpContext.Object
            };
        }

        [Fact]
        public void Index_ShouldRedirectToStockDashboard()
        {
            // Act
            var result = _controller.Index();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirectResult = result as RedirectToActionResult;
            redirectResult!.ActionName.Should().Be("Dashboard");
            redirectResult.ControllerName.Should().Be("Stock");
        }

        [Fact]
        public void Privacy_ShouldReturnView()
        {
            // Act
            var result = _controller.Privacy();

            // Assert
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public void Error_ShouldReturnErrorView()
        {
            // Arrange
            _mockHttpContext.Setup(x => x.TraceIdentifier).Returns("test-trace-id");

            // Act
            var result = _controller.Error();

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.Model.Should().NotBeNull();
            viewResult.Model.Should().BeOfType<ai_stock_trade_app.Models.ErrorViewModel>();
            
            var errorModel = viewResult.Model as ai_stock_trade_app.Models.ErrorViewModel;
            errorModel!.RequestId.Should().Be("test-trace-id");
        }

        [Fact]
        public void Error_WithoutTraceIdentifier_ShouldHandleNullRequestId()
        {
            // Arrange
            _mockHttpContext.Setup(x => x.TraceIdentifier).Returns((string?)null);

            // Act
            var result = _controller.Error();

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.Model.Should().NotBeNull();
            viewResult.Model.Should().BeOfType<ai_stock_trade_app.Models.ErrorViewModel>();
            
            var errorModel = viewResult.Model as ai_stock_trade_app.Models.ErrorViewModel;
            // The RequestId can be null when both Activity.Current?.Id and TraceIdentifier are null
            // This is acceptable behavior for the error view model
            errorModel.Should().NotBeNull();
        }
    }
}
