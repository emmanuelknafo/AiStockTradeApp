using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AiStockTradeApp.Services
{
    /// <summary>
    /// Extension methods for structured logging with performance tracking
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Logs the start of an operation and returns a disposable that logs completion time
        /// </summary>
        public static IDisposable? LogOperationStart(this ILogger logger, string operationName, object? parameters = null)
        {
            if (!logger.IsEnabled(LogLevel.Information))
                return null;

            logger.LogInformation("Starting operation: {OperationName} with parameters: {Parameters}", 
                operationName, parameters);
            
            return new OperationTimer(logger, operationName);
        }

        /// <summary>
        /// Logs the start of a database operation
        /// </summary>
        public static IDisposable? LogDatabaseOperation(this ILogger logger, string operation, string? entityType = null, object? key = null)
        {
            if (!logger.IsEnabled(LogLevel.Debug))
                return null;

            logger.LogDebug("Database operation starting: {Operation} on {EntityType} with key {Key}", 
                operation, entityType, key);
            
            return new OperationTimer(logger, $"Database.{operation}");
        }

        /// <summary>
        /// Logs an HTTP client request
        /// </summary>
        public static void LogHttpRequest(this ILogger logger, string method, string url, TimeSpan? duration = null)
        {
            if (duration.HasValue)
            {
                logger.LogInformation("HTTP {Method} to {Url} completed in {Duration}ms", 
                    method, url, duration.Value.TotalMilliseconds);
            }
            else
            {
                logger.LogDebug("HTTP {Method} to {Url} starting", method, url);
            }
        }

        /// <summary>
        /// Logs user action with session context
        /// </summary>
        public static void LogUserAction(this ILogger logger, string action, string? sessionId = null, object? context = null)
        {
            logger.LogInformation("User action: {Action} for session {SessionId} with context {Context}", 
                action, sessionId, context);
        }

        /// <summary>
        /// Logs business logic events
        /// </summary>
        public static void LogBusinessEvent(this ILogger logger, string eventName, object data)
        {
            logger.LogInformation("Business event: {EventName} with data {Data}", eventName, data);
        }

        /// <summary>
        /// Logs performance metrics
        /// </summary>
        public static void LogPerformanceMetric(this ILogger logger, string metricName, double value, string? unit = null)
        {
            logger.LogInformation("Performance metric: {MetricName} = {Value} {Unit}", metricName, value, unit);
        }

        /// <summary>
        /// Logs authentication events with structured data
        /// </summary>
        public static void LogAuthenticationEvent(this ILogger logger, string eventType, string userIdentifier, 
            bool success, string? errorMessage = null, object? additionalData = null)
        {
            if (success)
            {
                logger.LogInformation("Authentication event: {EventType} succeeded for user {UserIdentifier} with data {AdditionalData}", 
                    eventType, userIdentifier, additionalData);
            }
            else
            {
                logger.LogWarning("Authentication event: {EventType} failed for user {UserIdentifier} - {ErrorMessage} with data {AdditionalData}", 
                    eventType, userIdentifier, errorMessage, additionalData);
            }
        }

        /// <summary>
        /// Logs user registration attempts with detailed information
        /// </summary>
        public static void LogRegistrationAttempt(this ILogger logger, string email, bool success, 
            IEnumerable<string>? errors = null, object? userContext = null)
        {
            if (success)
            {
                logger.LogInformation("User registration succeeded for {Email} with context {UserContext}", 
                    email, userContext);
            }
            else
            {
                var errorList = errors?.ToList() ?? new List<string>();
                logger.LogWarning("User registration failed for {Email} with errors: {Errors} and context {UserContext}", 
                    email, errorList, userContext);
            }
        }

        /// <summary>
        /// Logs login attempts with IP address and user agent tracking
        /// </summary>
        public static void LogLoginAttempt(this ILogger logger, string email, bool success, 
            string? ipAddress = null, string? userAgent = null, string? lockoutReason = null)
        {
            var context = new { IpAddress = ipAddress, UserAgent = userAgent, LockoutReason = lockoutReason };
            
            if (success)
            {
                logger.LogInformation("Login successful for {Email} from {IpAddress} using {UserAgent}", 
                    email, ipAddress, userAgent);
            }
            else
            {
                logger.LogWarning("Login failed for {Email} from {IpAddress} using {UserAgent} - Reason: {LockoutReason}", 
                    email, ipAddress, userAgent, lockoutReason ?? "Invalid credentials");
            }
        }

        /// <summary>
        /// Logs password change events
        /// </summary>
        public static void LogPasswordChangeEvent(this ILogger logger, string userIdentifier, bool success, 
            string? reason = null)
        {
            if (success)
            {
                logger.LogInformation("Password change succeeded for user {UserIdentifier}", userIdentifier);
            }
            else
            {
                logger.LogWarning("Password change failed for user {UserIdentifier} - {Reason}", 
                    userIdentifier, reason);
            }
        }

        /// <summary>
        /// Logs account lockout events
        /// </summary>
        public static void LogAccountLockout(this ILogger logger, string userIdentifier, DateTime lockoutEnd, 
            int failedAttempts, string? triggeredBy = null)
        {
            logger.LogWarning("Account lockout triggered for user {UserIdentifier} until {LockoutEnd} after {FailedAttempts} failed attempts. Triggered by: {TriggeredBy}", 
                userIdentifier, lockoutEnd, failedAttempts, triggeredBy);
        }

        /// <summary>
        /// Logs security events like suspicious activities
        /// </summary>
        public static void LogSecurityEvent(this ILogger logger, string eventType, string userIdentifier, 
            string description, object? securityContext = null)
        {
            logger.LogWarning("Security event: {EventType} for user {UserIdentifier} - {Description} with context {SecurityContext}", 
                eventType, userIdentifier, description, securityContext);
        }
    }

    /// <summary>
    /// Disposable timer for tracking operation duration
    /// </summary>
    internal class OperationTimer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public OperationTimer(ILogger logger, string operationName)
        {
            _logger = logger;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _stopwatch.Stop();
            _logger.LogInformation("Operation completed: {OperationName} in {Duration}ms", 
                _operationName, _stopwatch.ElapsedMilliseconds);
            
            // Log warning for long-running operations
            if (_stopwatch.ElapsedMilliseconds > 5000) // 5 seconds
            {
                _logger.LogWarning("Long-running operation detected: {OperationName} took {Duration}ms", 
                    _operationName, _stopwatch.ElapsedMilliseconds);
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Static class for logging constants and templates
    /// </summary>
    public static class LoggingConstants
    {
        // Event categories
        public const string UserAction = "UserAction";
        public const string BusinessLogic = "BusinessLogic";
        public const string DataAccess = "DataAccess";
        public const string ExternalApi = "ExternalApi";
        public const string Performance = "Performance";
        public const string Security = "Security";
        public const string Authentication = "Authentication";
        public const string Authorization = "Authorization";

        // Authentication event types
        public const string UserRegistration = "UserRegistration";
        public const string UserLogin = "UserLogin";
        public const string UserLogout = "UserLogout";
        public const string PasswordChange = "PasswordChange";
        public const string PasswordReset = "PasswordReset";
        public const string AccountLockout = "AccountLockout";
        public const string EmailConfirmation = "EmailConfirmation";
        public const string TwoFactorAuthentication = "TwoFactorAuthentication";

        // Common template messages
        public const string StockDataRequested = "Stock data requested for symbol {Symbol}";
        public const string StockDataRetrieved = "Stock data retrieved for symbol {Symbol} in {Duration}ms";
        public const string WatchlistUpdated = "Watchlist updated for session {SessionId}: {Action} symbol {Symbol}";
        public const string ApiCallFailed = "External API call failed: {ApiName} for {Resource} - {ErrorMessage}";
        public const string CacheHit = "Cache hit for key {CacheKey}";
        public const string CacheMiss = "Cache miss for key {CacheKey}";
        
        // Authentication template messages
        public const string UserRegistrationAttempt = "User registration attempt for {Email} from {IpAddress}";
        public const string UserRegistrationSuccess = "User registration successful for {Email}";
        public const string UserRegistrationFailed = "User registration failed for {Email}: {Errors}";
        public const string UserLoginAttempt = "User login attempt for {Email} from {IpAddress}";
        public const string UserLoginSuccess = "User login successful for {Email}";
        public const string UserLoginFailed = "User login failed for {Email}: {Reason}";
        public const string UserLogoutEvent = "User logout for {UserId}";
        public const string AccountLockedOut = "Account locked out for {Email} until {LockoutEnd}";
        public const string SuspiciousActivity = "Suspicious activity detected for {UserIdentifier}: {Description}";
    }
}
