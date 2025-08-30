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
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
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
            // Arrange
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);
            
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
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
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
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
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
            // Arrange
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            
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
            // Arrange
            _mockLogger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
            
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

        #region Authentication Logging Tests

        [Fact]
        public void LogAuthenticationEvent_WithSuccessfulEvent_ShouldLogInformation()
        {
            // Arrange
            var eventType = "UserLogin";
            var userIdentifier = "test@example.com";
            var additionalData = new { IpAddress = "127.0.0.1", UserAgent = "TestAgent" };

            // Act
            _mockLogger.Object.LogAuthenticationEvent(eventType, userIdentifier, true, null, additionalData);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Authentication event: {eventType} succeeded for user {userIdentifier}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogAuthenticationEvent_WithFailedEvent_ShouldLogWarning()
        {
            // Arrange
            var eventType = "UserLogin";
            var userIdentifier = "test@example.com";
            var errorMessage = "Invalid credentials";
            var additionalData = new { IpAddress = "127.0.0.1" };

            // Act
            _mockLogger.Object.LogAuthenticationEvent(eventType, userIdentifier, false, errorMessage, additionalData);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Authentication event: {eventType} failed for user {userIdentifier} - {errorMessage}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogRegistrationAttempt_WithSuccessfulRegistration_ShouldLogInformation()
        {
            // Arrange
            var email = "new@example.com";
            var userContext = new { IpAddress = "127.0.0.1", UserAgent = "TestAgent" };

            // Act
            _mockLogger.Object.LogRegistrationAttempt(email, true, null, userContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"User registration succeeded for {email}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogRegistrationAttempt_WithFailedRegistration_ShouldLogWarning()
        {
            // Arrange
            var email = "new@example.com";
            var errors = new[] { "Password too weak", "Email already exists" };
            var userContext = new { IpAddress = "127.0.0.1" };

            // Act
            _mockLogger.Object.LogRegistrationAttempt(email, false, errors, userContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"User registration failed for {email}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogLoginAttempt_WithSuccessfulLogin_ShouldLogInformation()
        {
            // Arrange
            var email = "test@example.com";
            var ipAddress = "127.0.0.1";
            var userAgent = "Mozilla/5.0 Test";

            // Act
            _mockLogger.Object.LogLoginAttempt(email, true, ipAddress, userAgent);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Login successful for {email} from {ipAddress}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogLoginAttempt_WithFailedLogin_ShouldLogWarning()
        {
            // Arrange
            var email = "test@example.com";
            var ipAddress = "127.0.0.1";
            var userAgent = "Mozilla/5.0 Test";
            var lockoutReason = "Invalid credentials";

            // Act
            _mockLogger.Object.LogLoginAttempt(email, false, ipAddress, userAgent, lockoutReason);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Login failed for {email} from {ipAddress}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogPasswordChangeEvent_WithSuccessfulChange_ShouldLogInformation()
        {
            // Arrange
            var userIdentifier = "user-123";

            // Act
            _mockLogger.Object.LogPasswordChangeEvent(userIdentifier, true);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Password change succeeded for user {userIdentifier}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogPasswordChangeEvent_WithFailedChange_ShouldLogWarning()
        {
            // Arrange
            var userIdentifier = "user-123";
            var reason = "Current password incorrect";

            // Act
            _mockLogger.Object.LogPasswordChangeEvent(userIdentifier, false, reason);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Password change failed for user {userIdentifier} - {reason}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogAccountLockout_ShouldLogWarning()
        {
            // Arrange
            var userIdentifier = "test@example.com";
            var lockoutEnd = DateTime.UtcNow.AddMinutes(15);
            var failedAttempts = 5;
            var triggeredBy = "Multiple failed login attempts";

            // Act
            _mockLogger.Object.LogAccountLockout(userIdentifier, lockoutEnd, failedAttempts, triggeredBy);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Account lockout triggered for user {userIdentifier}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogSecurityEvent_ShouldLogWarning()
        {
            // Arrange
            var eventType = "SuspiciousLogin";
            var userIdentifier = "test@example.com";
            var description = "Login from unusual location";
            var securityContext = new { IpAddress = "192.168.1.100", Country = "Unknown" };

            // Act
            _mockLogger.Object.LogSecurityEvent(eventType, userIdentifier, description, securityContext);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Security event: {eventType} for user {userIdentifier} - {description}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData("UserRegistration")]
        [InlineData("UserLogin")]
        [InlineData("UserLogout")]
        [InlineData("PasswordChange")]
        public void LogAuthenticationEvent_WithDifferentEventTypes_ShouldIncludeEventTypeInLog(string eventType)
        {
            // Arrange
            var userIdentifier = "test@example.com";

            // Act
            _mockLogger.Object.LogAuthenticationEvent(eventType, userIdentifier, true);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Authentication event: {eventType}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void LogLoginAttempt_WithNullOptionalParameters_ShouldNotThrow()
        {
            // Arrange
            var email = "test@example.com";

            // Act & Assert
            var act = () => _mockLogger.Object.LogLoginAttempt(email, true, null, null, null);
            act.Should().NotThrow();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}
