using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using AiStockTradeApp.Controllers;
using System.Reflection;

namespace AiStockTradeApp.Tests.Controllers
{
    public class VersionControllerTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly VersionController _controller;

        public VersionControllerTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _controller = new VersionController(_mockConfiguration.Object);
        }

        [Fact]
        public void Get_ShouldReturnVersionInformation()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["APP_VERSION"]).Returns("1.0.0");

            // Act
            var result = _controller.Get();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.Value.Should().NotBeNull();

            // Verify the returned object has the expected properties
            var value = okResult.Value;
            var valueType = value!.GetType();
            
            valueType.GetProperty("version").Should().NotBeNull();
            valueType.GetProperty("fileVersion").Should().NotBeNull();
            valueType.GetProperty("product").Should().NotBeNull();
            valueType.GetProperty("appVersion").Should().NotBeNull();
        }

        [Fact]
        public void Get_WithNullAppVersion_ShouldReturnNullAppVersion()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["APP_VERSION"]).Returns((string?)null);

            // Act
            var result = _controller.Get();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            var value = okResult!.Value;
            value.Should().NotBeNull();
            
            // Use reflection to check the appVersion property
            var appVersionProperty = value!.GetType().GetProperty("appVersion");
            appVersionProperty.Should().NotBeNull();
            var appVersionValue = appVersionProperty!.GetValue(value);
            appVersionValue.Should().BeNull();
        }

        [Fact]
        public void Get_WithEmptyAppVersion_ShouldReturnNullAppVersion()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["APP_VERSION"]).Returns("");

            // Act
            var result = _controller.Get();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            var value = okResult!.Value;
            value.Should().NotBeNull();
            
            // Use reflection to check the appVersion property
            var appVersionProperty = value!.GetType().GetProperty("appVersion");
            appVersionProperty.Should().NotBeNull();
            var appVersionValue = appVersionProperty!.GetValue(value);
            appVersionValue.Should().BeNull();
        }

        [Fact]
        public void Get_WithWhitespaceAppVersion_ShouldReturnNullAppVersion()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["APP_VERSION"]).Returns("   ");

            // Act
            var result = _controller.Get();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            var value = okResult!.Value;
            value.Should().NotBeNull();
            
            // Use reflection to check the appVersion property
            var appVersionProperty = value!.GetType().GetProperty("appVersion");
            appVersionProperty.Should().NotBeNull();
            var appVersionValue = appVersionProperty!.GetValue(value);
            appVersionValue.Should().BeNull();
        }

        [Fact]
        public void Get_WithValidAppVersion_ShouldReturnAppVersion()
        {
            // Arrange
            var expectedAppVersion = "2.1.0-beta";
            _mockConfiguration.Setup(x => x["APP_VERSION"]).Returns(expectedAppVersion);

            // Act
            var result = _controller.Get();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            var value = okResult!.Value;
            value.Should().NotBeNull();
            
            // Use reflection to check the appVersion property
            var appVersionProperty = value!.GetType().GetProperty("appVersion");
            appVersionProperty.Should().NotBeNull();
            var appVersionValue = appVersionProperty!.GetValue(value);
            appVersionValue.Should().Be(expectedAppVersion);
        }

        [Fact]
        public void Get_ShouldReturnAssemblyInformation()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["APP_VERSION"]).Returns("1.0.0");

            // Act
            var result = _controller.Get();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            
            var value = okResult!.Value;
            value.Should().NotBeNull();
            
            // Check that version information is not "unknown" (should be real assembly info)
            var versionProperty = value!.GetType().GetProperty("version");
            versionProperty.Should().NotBeNull();
            var versionValue = versionProperty!.GetValue(value) as string;
            versionValue.Should().NotBeNullOrEmpty();
            
            var fileVersionProperty = value.GetType().GetProperty("fileVersion");
            fileVersionProperty.Should().NotBeNull();
            var fileVersionValue = fileVersionProperty!.GetValue(value) as string;
            fileVersionValue.Should().NotBeNullOrEmpty();
        }
    }
}
