using Microsoft.Playwright;
using NUnit.Framework;
using System;
using System.IO;

namespace AiStockTradeApp.UITests.Helpers;

public static class StockAdditionHelper
{
    public record StockAddResult(bool Success, bool Inconclusive, string? NotificationText, bool CardVisible, string? ScreenshotPath);

    public static async Task<StockAddResult> WaitForNotificationOrCardAsync(IPage page, string symbol, int timeoutMs = 10000)
    {
        var cardSelector = $"#card-{symbol}";
        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        string? notificationText = null;
        bool success = false;
        bool cardVisible = false;

        while (DateTime.UtcNow < end)
        {
            // Check for notification
            var notifList = await page.Locator(".notification").AllAsync();
            if (notifList.Count > 0)
            {
                notificationText = (await notifList[0].TextContentAsync())?.Trim();
                if (!string.IsNullOrEmpty(notificationText) &&
                    (notificationText.Contains("Added", StringComparison.OrdinalIgnoreCase) || notificationText.Contains("Success", StringComparison.OrdinalIgnoreCase)))
                {
                    success = true;
                }
                break; // Exit after first notification encountered
            }

            // Check for card
            if (await page.Locator(cardSelector).IsVisibleAsync())
            {
                cardVisible = true;
                success = true; // Treat visible card as success
                break;
            }

            await Task.Delay(300);
        }

        if (!success)
        {
            // Attempt final card check after a small grace period
            try
            {
                await Task.Delay(500);
                if (await page.Locator(cardSelector).IsVisibleAsync())
                {
                    cardVisible = true;
                    success = true;
                }
            }
            catch { }
        }

        bool inconclusive = !success && notificationText == null && !cardVisible;
        string? screenshotPath = null;
        if (inconclusive)
        {
            screenshotPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"inconclusive-{TestContext.CurrentContext.Test.Name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                TestContext.WriteLine($"Captured inconclusive screenshot: {screenshotPath}");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Failed to capture screenshot: {ex.Message}");
            }
        }

        return new StockAddResult(success, inconclusive, notificationText, cardVisible, screenshotPath);
    }
}
