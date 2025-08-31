using AiStockTradeApp.Services;
using System.Diagnostics;

namespace AiStockTradeApp.Api.Middleware;

/// <summary>
/// Extension methods for enhanced API endpoint logging using structured logging patterns.
/// </summary>
public static class ApiLoggingExtensions
{
    /// <summary>
    /// Logs API endpoint execution with timing, parameters, and result information.
    /// </summary>
    public static async Task<T> LogApiOperationAsync<T>(
        this ILogger logger,
        string operationName,
        Func<Task<T>> operation,
        object? parameters = null,
        string? correlationId = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["OperationName"] = operationName,
            ["CorrelationId"] = correlationId ?? "unknown"
        });

        logger.LogInformation(
            "Starting API operation {OperationName} - OperationId: {OperationId}, Parameters: {@Parameters}",
            operationName, operationId, parameters);

        try
        {
            var result = await operation();
            stopwatch.Stop();

            logger.LogInformation(
                "Completed API operation {OperationName} - OperationId: {OperationId}, " +
                "Duration: {Duration}ms, Success: true",
                operationName, operationId, stopwatch.ElapsedMilliseconds);

            // Log performance warning for slow operations
            if (stopwatch.ElapsedMilliseconds > 5000)
            {
                logger.LogWarning(
                    "Slow API operation detected: {OperationName} took {Duration}ms - OperationId: {OperationId}",
                    operationName, stopwatch.ElapsedMilliseconds, operationId);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex,
                "Failed API operation {OperationName} - OperationId: {OperationId}, " +
                "Duration: {Duration}ms, Error: {ErrorMessage}",
                operationName, operationId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Logs API endpoint execution without return value.
    /// </summary>
    public static async Task LogApiOperationAsync(
        this ILogger logger,
        string operationName,
        Func<Task> operation,
        object? parameters = null,
        string? correlationId = null)
    {
        await logger.LogApiOperationAsync(operationName, async () =>
        {
            await operation();
            return true; // Dummy return value
        }, parameters, correlationId);
    }

    /// <summary>
    /// Logs external API calls with detailed request/response information.
    /// </summary>
    public static async Task<T> LogExternalApiCallAsync<T>(
        this ILogger logger,
        string apiName,
        string endpoint,
        Func<Task<T>> apiCall,
        object? requestData = null)
    {
        var callId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ExternalApiCall"] = apiName,
            ["ApiCallId"] = callId,
            ["Endpoint"] = endpoint
        });

        logger.LogInformation(
            "Starting external API call to {ApiName} - CallId: {CallId}, " +
            "Endpoint: {Endpoint}, Request: {@RequestData}",
            apiName, callId, endpoint, requestData);

        try
        {
            var result = await apiCall();
            stopwatch.Stop();

            logger.LogInformation(
                "Completed external API call to {ApiName} - CallId: {CallId}, " +
                "Duration: {Duration}ms, Success: true",
                apiName, callId, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            logger.LogError(ex,
                "External API call failed to {ApiName} - CallId: {CallId}, " +
                "Duration: {Duration}ms, HttpError: {ErrorMessage}",
                apiName, callId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            logger.LogWarning(ex,
                "External API call timed out to {ApiName} - CallId: {CallId}, " +
                "Duration: {Duration}ms",
                apiName, callId, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex,
                "Unexpected error in external API call to {ApiName} - CallId: {CallId}, " +
                "Duration: {Duration}ms, Error: {ErrorMessage}",
                apiName, callId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Logs database operations with entity information and performance metrics.
    /// </summary>
    public static async Task<T> LogDatabaseOperationAsync<T>(
        this ILogger logger,
        string operation,
        string entityType,
        Func<Task<T>> dbOperation,
        object? entityKey = null)
    {
        var operationId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["DatabaseOperation"] = operation,
            ["EntityType"] = entityType,
            ["OperationId"] = operationId
        });

        logger.LogDebug(
            "Starting database operation {Operation} on {EntityType} - " +
            "OperationId: {OperationId}, Key: {EntityKey}",
            operation, entityType, operationId, entityKey);

        try
        {
            var result = await dbOperation();
            stopwatch.Stop();

            logger.LogDebug(
                "Completed database operation {Operation} on {EntityType} - " +
                "OperationId: {OperationId}, Duration: {Duration}ms",
                operation, entityType, operationId, stopwatch.ElapsedMilliseconds);

            // Log performance warning for slow database operations
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                logger.LogWarning(
                    "Slow database operation: {Operation} on {EntityType} took {Duration}ms - " +
                    "OperationId: {OperationId}, Key: {EntityKey}",
                    operation, entityType, stopwatch.ElapsedMilliseconds, operationId, entityKey);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex,
                "Database operation failed: {Operation} on {EntityType} - " +
                "OperationId: {OperationId}, Duration: {Duration}ms, " +
                "Key: {EntityKey}, Error: {ErrorMessage}",
                operation, entityType, operationId, stopwatch.ElapsedMilliseconds,
                entityKey, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Logs API validation errors with structured error details.
    /// </summary>
    public static void LogValidationError(
        this ILogger logger,
        string operationName,
        string parameterName,
        object? parameterValue,
        string validationMessage,
        string? correlationId = null)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationName"] = operationName,
            ["ParameterName"] = parameterName,
            ["CorrelationId"] = correlationId ?? "unknown"
        });

        logger.LogWarning(
            "Validation error in {OperationName} - Parameter: {ParameterName}, " +
            "Value: {ParameterValue}, Message: {ValidationMessage}, " +
            "CorrelationId: {CorrelationId}",
            operationName, parameterName, parameterValue, validationMessage, correlationId);
    }

    /// <summary>
    /// Logs business events specific to stock trading operations.
    /// </summary>
    public static void LogStockDataEvent(
        this ILogger logger,
        string eventType,
        string symbol,
        object eventData,
        string? correlationId = null)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["EventType"] = eventType,
            ["Symbol"] = symbol,
            ["CorrelationId"] = correlationId ?? "unknown"
        });

        logger.LogInformation(
            "Stock data event: {EventType} for {Symbol} - " +
            "CorrelationId: {CorrelationId}, Data: {@EventData}",
            eventType, symbol, correlationId, eventData);
    }

    /// <summary>
    /// Logs import job progress and status updates.
    /// </summary>
    public static void LogImportJobProgress(
        this ILogger logger,
        Guid jobId,
        string jobType,
        string status,
        int totalItems = 0,
        int processedItems = 0,
        string? errorMessage = null)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = jobId,
            ["JobType"] = jobType,
            ["Status"] = status
        });

        if (!string.IsNullOrEmpty(errorMessage))
        {
            logger.LogError(
                "Import job {JobId} ({JobType}) failed - Status: {Status}, " +
                "Processed: {ProcessedItems}/{TotalItems}, Error: {ErrorMessage}",
                jobId, jobType, status, processedItems, totalItems, errorMessage);
        }
        else if (totalItems > 0)
        {
            var progressPercent = totalItems > 0 ? (processedItems * 100.0 / totalItems) : 0;
            logger.LogInformation(
                "Import job {JobId} ({JobType}) progress - Status: {Status}, " +
                "Progress: {ProcessedItems}/{TotalItems} ({ProgressPercent:F1}%)",
                jobId, jobType, status, processedItems, totalItems, progressPercent);
        }
        else
        {
            logger.LogInformation(
                "Import job {JobId} ({JobType}) status - Status: {Status}",
                jobId, jobType, status);
        }
    }
}
