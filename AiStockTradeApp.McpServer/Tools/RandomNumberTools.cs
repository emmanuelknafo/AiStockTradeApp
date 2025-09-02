using System.ComponentModel;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using System.Diagnostics;

/// <summary>
/// Sample MCP tools for demonstration purposes.
/// These tools can be invoked by MCP clients to perform various operations.
/// </summary>
internal class RandomNumberTools
{
    private readonly ILogger<RandomNumberTools> _logger;
    private readonly TelemetryClient _telemetryClient;

    public RandomNumberTools(ILogger<RandomNumberTools> logger, TelemetryClient telemetryClient)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
        
        _logger.LogInformation("RandomNumberTools initialized");
    }
    [McpServerTool]
    [Description("Generates a random number between the specified minimum and maximum values.")]
    public int GetRandomNumber(
        [Description("Minimum value (inclusive)")] int min = 0,
        [Description("Maximum value (exclusive)")] int max = 109)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetRandomNumber");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("GetRandomNumber started with min: {Min}, max: {Max}", min, max);
            
            activity?.SetTag("random.min", min);
            activity?.SetTag("random.max", max);
            
            var result = Random.Shared.Next(min, max);
            
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;
            
            _logger.LogInformation("GetRandomNumber completed - Result: {Result}, Range: [{Min}, {Max}), Duration: {Duration}ms", 
                result, min, max, duration);

            _telemetryClient.TrackEvent("RandomNumber.Generated", new Dictionary<string, string>
            {
                ["Min"] = min.ToString(),
                ["Max"] = max.ToString(),
                ["Result"] = result.ToString(),
                ["Range"] = (max - min).ToString()
            });

            _telemetryClient.TrackMetric("RandomNumber.GenerationTime", duration);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;
            
            _logger.LogError(ex, "GetRandomNumber exception with min: {Min}, max: {Max} - Duration: {Duration}ms", min, max, duration);
            
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Method"] = "GetRandomNumber",
                ["Min"] = min.ToString(),
                ["Max"] = max.ToString(),
                ["Duration"] = duration.ToString()
            });
            
            throw;
        }
    }

    [McpServerTool]
    [Description("Generates a random list of numbers with specified count, minimum, and maximum values.")]
    public List<int> GetRandomNumberList(
        [Description("Number of random numbers to generate (default: 10, max: 1000)")] int count = 10,
        [Description("Minimum value for each number (inclusive)")] int min = 0,
        [Description("Maximum value for each number (exclusive)")] int max = 100,
        [Description("Whether to allow duplicate numbers (default: true)")] bool allowDuplicates = true)
    {
        // Validate inputs
        count = Math.Max(1, Math.Min(1000, count)); // Clamp between 1 and 1000
        
        if (min >= max)
        {
            throw new ArgumentException("Minimum value must be less than maximum value");
        }

        var numbers = new List<int>();
        var random = Random.Shared;

        if (!allowDuplicates)
        {
            // Check if it's possible to generate unique numbers
            var range = max - min;
            if (count > range)
            {
                throw new ArgumentException($"Cannot generate {count} unique numbers in range [{min}, {max}). Maximum unique numbers possible: {range}");
            }

            // Generate unique numbers
            var availableNumbers = Enumerable.Range(min, range).ToList();
            
            for (int i = 0; i < count; i++)
            {
                var index = random.Next(availableNumbers.Count);
                numbers.Add(availableNumbers[index]);
                availableNumbers.RemoveAt(index);
            }
        }
        else
        {
            // Generate numbers with possible duplicates
            for (int i = 0; i < count; i++)
            {
                numbers.Add(random.Next(min, max));
            }
        }

        return numbers;
    }
}
