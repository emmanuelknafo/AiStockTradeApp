using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace AiStockTradeApp.Services.Implementations;

public static class HistoricalDataFetcher
{
    public static async Task<string?> TryDownloadHistoricalCsvAsync(string symbol, int timeoutSec = 60)
    {
        symbol = symbol.Trim().ToLowerInvariant();
        var url = $"https://www.nasdaq.com/market-activity/stocks/{symbol}/historical?page=1&rows_per_page=10&timeline=y10";
        using var pw = await Playwright.CreateAsync();

        // Prefer Firefox per request
        await using var browser = await pw.Firefox.LaunchAsync(new() { Headless = true });
        await using var ctx = await browser.NewContextAsync(new()
        {
            AcceptDownloads = true,
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36",
            ViewportSize = new() { Width = 1280, Height = 900 }
        });
        var page = await ctx.NewPageAsync();
        page.SetDefaultTimeout(Math.Max(10, timeoutSec) * 1000);

        try
        {
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        catch
        {
            // best effort
        }

        await TryAcceptCookiesAsync(page, 10000);

    var loc = await FindDownloadControlAsync(page, Math.Max(10, timeoutSec) * 1000);
        if (loc is null)
        {
            return null;
        }

        await loc.ScrollIntoViewIfNeededAsync();
        try { await loc.HoverAsync(new() { Timeout = 1500 }); } catch { }

        try
        {
            var dl = await page.RunAndWaitForDownloadAsync(async () =>
            {
                await loc.ClickAsync(new() { Force = true, Timeout = 8000 });
            });
            var stream = await dl.CreateReadStreamAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            // Fallback: capture network response that looks like CSV
            try
            {
                await loc.ClickAsync(new() { Force = true, Timeout = 8000 });
                var response = await page.WaitForResponseAsync(r =>
                {
                    try
                    {
                        var u = r.Url ?? string.Empty;
                        var ct = r.Headers.TryGetValue("content-type", out var v) ? v : string.Empty;
                        return u.Contains(".csv", StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(ct) && ct.Contains("text/csv", StringComparison.OrdinalIgnoreCase));
                    }
                    catch { return false; }
                }, new() { Timeout = Math.Max(10, timeoutSec) * 500 });
                await response.FinishedAsync();
                var body = await response.BodyAsync();
                return System.Text.Encoding.UTF8.GetString(body);
            }
            catch
            {
                return null;
            }
        }
    }

    private static async Task<ILocator?> FindDownloadControlAsync(IPage page, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var factory in BuildCandidates())
            {
                var loc = factory(page).First;
                try
                {
                    await loc.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 1200 });
                    if (await loc.IsVisibleAsync()) return loc;
                }
                catch { }
            }

            try
            {
                for (int i = 0; i < 4; i++)
                {
                    await page.EvaluateAsync("window.scrollBy(0, 600)");
                    await page.WaitForTimeoutAsync(200);
                }
                await page.EvaluateAsync("window.scrollTo(0, 0)");
            }
            catch { }

            await page.WaitForTimeoutAsync(400);
        }
        return null;
    }

    private static IEnumerable<Func<IPage, ILocator>> BuildCandidates()
    {
        yield return p => p.GetByRole(AriaRole.Button, new() { Name = "Download historical data" });
        yield return p => p.GetByRole(AriaRole.Link, new() { Name = "Download historical data" });
        yield return p => p.GetByRole(AriaRole.Button, new() { NameRegex = new("(?i)download.*(historical|csv)") });
        yield return p => p.GetByRole(AriaRole.Link, new() { NameRegex = new("(?i)download.*(historical|csv)") });
        yield return p => p.GetByText(new System.Text.RegularExpressions.Regex("Download\\s+(historical|csv)", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        yield return p => p.Locator(":has-text('Download historical data')");
        yield return p => p.Locator(":has-text('Download CSV')");
        yield return p => p.Locator("a[download], button[download]");
        yield return p => p.Locator("[aria-label*='Download' i]");
        yield return p => p.Locator("[data-testid*='download' i]");
        yield return p => p.Locator("a[href*='.csv'], a[href*='download']");
    }

    private static async Task TryAcceptCookiesAsync(IPage page, int acceptTimeoutMs = 7000)
    {
        try
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, acceptTimeoutMs));
            try { await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)"); } catch { }
            while (DateTime.UtcNow < deadline)
            {
                foreach (var frame in page.Frames)
                {
                    try
                    {
                        var loc = frame.Locator("#onetrust-accept-btn-handler");
                        await loc.ScrollIntoViewIfNeededAsync();
                        await loc.ClickAsync(new() { Timeout = 800, Force = true });
                        await page.WaitForTimeoutAsync(300);
                        return;
                    }
                    catch { }
                }
                await page.WaitForTimeoutAsync(400);
            }
        }
        catch { }
    }
}
