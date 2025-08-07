using Microsoft.Playwright;
using NUnit.Framework;
using System.Net.Http;

namespace ai_stock_trade_app.UITests;

/// <summary>
/// Helper class to ensure the application is running before tests execute
/// </summary>
public static class TestSetupHelper
{
    public static async Task<bool> IsApplicationRunningAsync(string baseUrl, int timeoutSeconds = 30)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(5);
        
        var startTime = DateTime.Now;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        
        while (DateTime.Now - startTime < timeout)
        {
            try
            {
                // Try to make a simple HTTP request to check if the app is running
                var response = await httpClient.GetAsync(baseUrl);
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Redirect)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
                // Application is not running, wait and try again
                await Task.Delay(1000);
            }
            catch (TaskCanceledException)
            {
                // Timeout on individual request, try again
                await Task.Delay(1000);
            }
        }
        
        return false;
    }
    
    public static async Task WaitForApplicationStartup(string baseUrl, int timeoutSeconds = 30)
    {
        var isRunning = await IsApplicationRunningAsync(baseUrl, timeoutSeconds);
        if (!isRunning)
        {
            Assert.Fail($"Application is not running at {baseUrl}. Please start the application before running tests.\n" +
                       "Run: cd ai-stock-trade-app && dotnet run");
        }
    }
}