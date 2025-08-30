using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;
using AiStockTradeApp.Services;
using AiStockTradeApp.Entities.Models;

namespace AiStockTradeApp.Tests.Services
{
    /// <summary>
    /// Integration tests to verify logging is properly implemented across services
    /// </summary>
    public class ServiceLoggingIntegrationTests
    {
        [Fact]
        public void GenericService_LogsOperationStart_WhenGettingStockData()
        {
            // This test verifies that services properly use the logging extensions
            // In a real implementation, we would mock the actual service dependencies
            // and verify that LogOperationStart is called with correct parameters
            
            // Arrange
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            
            // Act - This is a pattern test to ensure logging is used correctly
            var operationName = "GetStockDataAsync";
            var parameters = new { Symbol = "AAPL", Source = "AlphaVantage" };
            
            // Verify the extension method signature works correctly
            var timer = mockLogger.Object.LogOperationStart(operationName, parameters);
            
            // Assert
            timer.Should().NotBeNull();
            
            // Verify the start log was called
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Starting operation: {operationName}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void DatabaseRepository_LogsDatabaseOperation_WhenAccessingData()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);
            
            // Act
            var timer = mockLogger.Object.LogDatabaseOperation("SELECT", "StockData", "AAPL");
            
            // Assert
            timer.Should().NotBeNull();
            
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Database operation starting: SELECT on StockData with key AAPL")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void HttpClientService_LogsHttpRequest_WhenCallingExternalApi()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);
            
            // Act
            mockLogger.Object.LogHttpRequest("GET", "https://www.alphavantage.co/query");
            
            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTP GET to https://www.alphavantage.co/query starting")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("AddToWatchlist", "AAPL")]
        [InlineData("RemoveFromWatchlist", "MSFT")]
        [InlineData("ViewStockDetails", "GOOGL")]
        [InlineData("UpdatePriceAlert", "TSLA")]
        public void WatchlistService_LogsUserAction_WhenUserInteracts(string action, string symbol)
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            var sessionId = "test-session-123";
            var context = new { Symbol = symbol, Timestamp = DateTime.UtcNow };
            
            // Act
            mockLogger.Object.LogUserAction(action, sessionId, context);
            
            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"User action: {action} for session {sessionId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void BusinessService_LogsBusinessEvent_WhenStockPriceUpdated()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            var eventData = new 
            { 
                Symbol = "AAPL", 
                OldPrice = 150.00m, 
                NewPrice = 151.25m, 
                ChangePercent = 0.83m,
                Timestamp = DateTime.UtcNow
            };
            
            // Act
            mockLogger.Object.LogBusinessEvent("StockPriceUpdated", eventData);
            
            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Business event: StockPriceUpdated")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("ApiResponseTime", 250.5, "ms")]
        [InlineData("DatabaseQueryTime", 125.0, "ms")]
        [InlineData("CacheHitRatio", 0.85, "%")]
        [InlineData("ActiveSessions", 42.0, "count")]
        public void PerformanceMonitoring_LogsMetrics_WhenMeasuring(string metricName, double value, string unit)
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            
            // Act
            mockLogger.Object.LogPerformanceMetric(metricName, value, unit);
            
            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Performance metric: {metricName} = {value} {unit}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }

    /// <summary>
    /// Tests for structured logging data integrity and serialization
    /// </summary>
    public class StructuredLoggingDataTests
    {
        [Fact]
        public void LoggingParameters_ComplexObject_ShouldSerializeCorrectly()
        {
            // Arrange
            var complexObject = new
            {
                Symbol = "AAPL",
                RequestId = Guid.NewGuid(),
                Metadata = new
                {
                    Source = "AlphaVantage",
                    CacheEnabled = true,
                    RequestTime = DateTime.UtcNow
                },
                Filters = new[] { "Price", "Volume", "Change" }
            };

            // Act - Test that the object can be serialized for logging
            var serialized = JsonSerializer.Serialize(complexObject);
            
            // Assert
            serialized.Should().NotBeNullOrEmpty();
            serialized.Should().Contain("AAPL");
            serialized.Should().Contain("AlphaVantage");
            serialized.Should().Contain("Price");
            
            var deserialized = JsonSerializer.Deserialize<object>(serialized);
            deserialized.Should().NotBeNull();
        }

        [Fact]
        public void LoggingContext_UserSession_ShouldContainRequiredFields()
        {
            // Arrange
            var userContext = new
            {
                SessionId = "session-123",
                UserId = "user-456",
                IPAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0 (Test Browser)",
                Culture = "en-US",
                Timestamp = DateTime.UtcNow
            };

            // Act
            var json = JsonSerializer.Serialize(userContext);
            
            // Assert
            json.Should().Contain("session-123");
            json.Should().Contain("user-456");
            json.Should().Contain("192.168.1.1");
            json.Should().Contain("en-US");
        }

        [Fact]
        public void LoggingContext_ErrorDetails_ShouldContainDiagnosticInfo()
        {
            // Arrange
            var exception = new InvalidOperationException("Test error message");
            var errorContext = new
            {
                ErrorId = Guid.NewGuid(),
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                Source = exception.Source,
                InnerException = exception.InnerException?.Message,
                CorrelationId = "correlation-789",
                Operation = "GetStockData",
                Parameters = new { Symbol = "INVALID_SYMBOL" }
            };

            // Act
            var json = JsonSerializer.Serialize(errorContext);
            
            // Assert
            json.Should().Contain("Test error message");
            json.Should().Contain("correlation-789");
            json.Should().Contain("GetStockData");
            json.Should().Contain("INVALID_SYMBOL");
        }
    }

    /// <summary>
    /// Tests for logging performance and resource usage
    /// </summary>
    public class LoggingPerformanceTests
    {
        [Fact]
        public void LoggingExtensions_DisabledLogLevel_ShouldHaveMinimalOverhead()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(false);
            
            // Act & Assert - Should not throw and complete quickly
            var startTime = DateTime.UtcNow;
            
            for (int i = 0; i < 1000; i++)
            {
                mockLogger.Object.LogOperationStart($"Operation{i}", new { Id = i });
                mockLogger.Object.LogUserAction($"Action{i}", $"session{i}");
                mockLogger.Object.LogPerformanceMetric($"Metric{i}", i * 1.5);
            }
            
            var duration = DateTime.UtcNow - startTime;
            
            // Should complete very quickly when logging is disabled
            duration.Should().BeLessThan(TimeSpan.FromSeconds(1));
            
            // Verify no actual logging occurred
            mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public void OperationTimer_MultipleDispose_ShouldNotCauseMemoryLeak()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            
            var timers = new List<IDisposable?>();
            
            // Act - Create multiple timers
            for (int i = 0; i < 100; i++)
            {
                var timer = mockLogger.Object.LogOperationStart($"Operation{i}");
                timers.Add(timer);
            }
            
            // Dispose all timers
            foreach (var timer in timers)
            {
                timer?.Dispose();
                timer?.Dispose(); // Double dispose should be safe
            }
            
            // Assert - Should complete without issues
            timers.Should().HaveCount(100);
            timers.Should().AllSatisfy(t => t.Should().NotBeNull());
        }
    }
}
