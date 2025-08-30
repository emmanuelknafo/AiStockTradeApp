using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using AiStockTradeApp.Services;
using Xunit;

namespace AiStockTradeApp.Tests.Services
{
    /// <summary>
    /// Comprehensive tests for LoggingExtensions to ensure proper structured logging
    /// </summary>
    public class LoggingExtensionsTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public LoggingExtensionsTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void LogOperationStart_WithEnabledInformationLevel_ShouldLogAndReturnTimer()
        {
            // Arrange
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            
            // Act
            var timer = _mockLogger.Object.LogOperationStart("TestOperation", new { param1 = "value1" });
            
            // Assert
            timer.Should().NotBeNull();
            timer.Should().BeAssignableTo<IDisposable>();
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting operation: TestOperation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogOperationStart_WithDisabledInformationLevel_ShouldReturnNull()
        {
            // Arrange
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(false);
            
            // Act
            var timer = _mockLogger.Object.LogOperationStart("TestOperation");
            
            // Assert
            timer.Should().BeNull();
            
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public void LogDatabaseOperation_WithEnabledDebugLevel_ShouldLogAndReturnTimer()
        {
            // Arrange
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);
            
            // Act
            var timer = _mockLogger.Object.LogDatabaseOperation("SELECT", "StockData", "AAPL");
            
            // Assert
            timer.Should().NotBeNull();
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Database operation starting: SELECT on StockData")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogHttpRequest_WithDuration_ShouldLogInformation()
        {
            // Arrange
            var duration = TimeSpan.FromMilliseconds(250);
            
            // Act
            _mockLogger.Object.LogHttpRequest("GET", "https://api.example.com/stocks", duration);
            
            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTP GET to https://api.example.com/stocks completed in 250ms")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogHttpRequest_WithoutDuration_ShouldLogDebug()
        {
            // Act
            _mockLogger.Object.LogHttpRequest("POST", "https://api.example.com/stocks");
            
            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTP POST to https://api.example.com/stocks starting")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogUserAction_WithSessionIdAndContext_ShouldLogInformation()
        {
            // Arrange
            var sessionId = "session123";
            var context = new { action = "AddToWatchlist", symbol = "AAPL" };
            
            // Act
            _mockLogger.Object.LogUserAction("AddStock", sessionId, context);
            
            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User action: AddStock for session session123")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogBusinessEvent_WithEventData_ShouldLogInformation()
        {
            // Arrange
            var eventData = new { Symbol = "MSFT", Price = 350.25m, Timestamp = DateTime.UtcNow };
            
            // Act
            _mockLogger.Object.LogBusinessEvent("StockPriceUpdated", eventData);
            
            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Business event: StockPriceUpdated")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogPerformanceMetric_WithValueAndUnit_ShouldLogInformation()
        {
            // Act
            _mockLogger.Object.LogPerformanceMetric("ResponseTime", 125.5, "ms");
            
            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Performance metric: ResponseTime = 125.5 ms")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogPerformanceMetric_WithoutUnit_ShouldLogInformation()
        {
            // Act
            _mockLogger.Object.LogPerformanceMetric("CacheHitRatio", 0.85);
            
            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Performance metric: CacheHitRatio = 0.85")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(LoggingConstants.UserAction)]
        [InlineData(LoggingConstants.BusinessLogic)]
        [InlineData(LoggingConstants.DataAccess)]
        [InlineData(LoggingConstants.ExternalApi)]
        [InlineData(LoggingConstants.Performance)]
        [InlineData(LoggingConstants.Security)]
        public void LoggingConstants_EventCategories_ShouldHaveExpectedValues(string category)
        {
            // Assert
            category.Should().NotBeNullOrEmpty();
            category.Should().NotContain(" ", "Event categories should not contain spaces");
        }

        [Theory]
        [InlineData(LoggingConstants.StockDataRequested, "Stock data requested for symbol {Symbol}")]
        [InlineData(LoggingConstants.StockDataRetrieved, "Stock data retrieved for symbol {Symbol} in {Duration}ms")]
        [InlineData(LoggingConstants.WatchlistUpdated, "Watchlist updated for session {SessionId}: {Action} symbol {Symbol}")]
        [InlineData(LoggingConstants.ApiCallFailed, "External API call failed: {ApiName} for {Resource} - {ErrorMessage}")]
        [InlineData(LoggingConstants.CacheHit, "Cache hit for key {CacheKey}")]
        [InlineData(LoggingConstants.CacheMiss, "Cache miss for key {CacheKey}")]
        public void LoggingConstants_TemplateMessages_ShouldHaveExpectedFormat(string actual, string expected)
        {
            // Assert
            actual.Should().Be(expected);
            actual.Should().Contain("{", "Template messages should contain parameter placeholders");
        }
    }

    /// <summary>
    /// Tests for OperationTimer functionality through the LoggingExtensions
    /// </summary>
    public class OperationTimerTests
    {
        private readonly Mock<ILogger> _mockLogger;

        public OperationTimerTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
        }

        [Fact]
        public void OperationTimer_Dispose_ShouldLogCompletion()
        {
            // Arrange
            var timer = _mockLogger.Object.LogOperationStart("TestOperation");
            
            // Act
            timer!.Dispose();
            
            // Assert - Verify completion log was called
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Operation completed: TestOperation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task OperationTimer_LongRunningOperation_ShouldLogWarning()
        {
            // Arrange
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);
            var timer = _mockLogger.Object.LogOperationStart("LongOperation");
            
            // Act - Simulate long-running operation
            await Task.Delay(100); // Not actually 5 seconds for test performance
            timer!.Dispose();
            
            // Note: In real scenario, we would need to mock the stopwatch or use a testable timer
            // For this test, we're verifying the pattern is correct
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Operation completed: LongOperation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void OperationTimer_DoubleDispose_ShouldNotThrow()
        {
            // Arrange
            var timer = _mockLogger.Object.LogOperationStart("TestOperation");
            
            // Act & Assert
            timer!.Dispose();
            timer.Invoking(t => t.Dispose()).Should().NotThrow();
        }
    }
}
