using AiStockTradeApp.Controllers;
using AiStockTradeApp.Entities.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using Moq;
using FluentAssertions;
using Xunit;

namespace AiStockTradeApp.Tests.Controllers
{
    public class HomeControllerTests
    {
        private readonly Mock<ILogger<HomeController>> _mockLogger;
        private readonly Mock<IStringLocalizerFactory> _mockLocalizerFactory;
        private readonly Mock<IStringLocalizer<SharedResource>> _mockDirectLocalizer;
        private readonly HomeController _controller;
        private readonly Mock<HttpContext> _mockHttpContext;

        public HomeControllerTests()
        {
            _mockLogger = new Mock<ILogger<HomeController>>();
            _mockLocalizerFactory = new Mock<IStringLocalizerFactory>();
            _mockDirectLocalizer = new Mock<IStringLocalizer<SharedResource>>();
            _controller = new HomeController(_mockLogger.Object, _mockLocalizerFactory.Object, _mockDirectLocalizer.Object);
            
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
            viewResult.Model.Should().BeOfType<ErrorViewModel>();
            
            var errorModel = viewResult.Model as ErrorViewModel;
            errorModel!.RequestId.Should().Be("test-trace-id");
        }

        [Fact]
        public void Error_WithoutTraceIdentifier_ShouldHandleNullRequestId()
        {
            // Arrange
            _mockHttpContext.Setup(x => x.TraceIdentifier).Returns(string.Empty);

            // Act
            var result = _controller.Error();

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.Model.Should().NotBeNull();
            viewResult.Model.Should().BeOfType<ErrorViewModel>();
            
            var errorModel = viewResult.Model as ErrorViewModel;
            // The RequestId will use Activity.Current?.Id if TraceIdentifier is empty
            errorModel.Should().NotBeNull();
        }
    }
}
