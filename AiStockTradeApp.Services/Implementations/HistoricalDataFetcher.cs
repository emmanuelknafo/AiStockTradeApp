using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Text.RegularExpressions;

namespace AiStockTradeApp.Services.Implementations;

public static class HistoricalDataFetcher
{
    private static int _installOnce;

    public static async Task<string?> TryDownloadHistoricalCsvAsync(string symbol, int timeoutSec = 60, ILogger? logger = null, TelemetryClient? telemetry = null)
    {
        symbol = symbol.Trim().ToLowerInvariant();
        var url = $"https://www.nasdaq.com/market-activity/stocks/{symbol}/historical?page=1&rows_per_page=10&timeline=y10";

        await EnsurePlaywrightInstalledAsync();

        using var pw = await Playwright.CreateAsync();

        var isLinux = OperatingSystem.IsLinux();
        var launchOptions = new BrowserTypeLaunchOptions { Headless = true };
        if (isLinux)
        {
            launchOptions.Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--no-first-run",
                "--no-default-browser-check",
                "--disable-background-timer-throttling",
                "--disable-backgrounding-occluded-windows",
                "--disable-renderer-backgrounding",
                "--disable-web-security",
                "--disable-features=TranslateUI",
                "--disable-ipc-flooding-protection",
                "--disable-blink-features=AutomationControlled"
            };
        }

        await using var browser = isLinux
            ? await pw.Chromium.LaunchAsync(launchOptions)
            : await pw.Firefox.LaunchAsync(new() { Headless = true });

        await using var ctx = await browser.NewContextAsync(new()
        {
            AcceptDownloads = true,
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36",
            ViewportSize = new() { Width = 1280, Height = 900 }
        });

        var page = await ctx.NewPageAsync();
        page.SetDefaultTimeout(Math.Max(10, timeoutSec) * 1000);

        logger?.LogInformation("HistoricalDataFetcher starting for {Symbol} on {OS}", symbol, isLinux ? "Linux" : "Non-Linux");
        telemetry?.TrackEvent(new EventTelemetry("HistoricalDataFetcher.Start")
        {
            Properties = { { "symbol", symbol }, { "os", isLinux ? "linux" : "other" } }
        });

        try
        {
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "HistoricalDataFetcher navigation failed for {Symbol}", symbol);
            telemetry?.TrackException(ex, new Dictionary<string, string>
            {
                { "stage", "goto" },
                { "symbol", symbol },
                { "url", url }
            });
        }

        await TryAcceptCookiesAsync(page, 10000);

        var loc = await FindDownloadControlAsync(page, Math.Max(10, timeoutSec) * 1000);
        if (loc is null)
        {
            logger?.LogWarning("HistoricalDataFetcher could not find download control for {Symbol}", symbol);
            telemetry?.TrackEvent(new EventTelemetry("HistoricalDataFetcher.NoControl")
            {
                Properties = { { "symbol", symbol } }
            });
            return null;
        }

        await loc.ScrollIntoViewIfNeededAsync();
        try { await loc.HoverAsync(new() { Timeout = 1500 }); } catch { }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var dl = await page.RunAndWaitForDownloadAsync(async () =>
            {
                await loc.ClickAsync(new() { Force = true, Timeout = 8000 });
            });
            var stream = await dl.CreateReadStreamAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var csv = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            stopwatch.Stop();
            logger?.LogInformation("HistoricalDataFetcher succeeded via download API for {Symbol} in {ElapsedMs}ms, bytes={Length}", symbol, stopwatch.Elapsed.TotalMilliseconds, csv?.Length ?? 0);
            telemetry?.TrackEvent(new EventTelemetry("HistoricalDataFetcher.Success")
            {
                Metrics = { { "elapsedMs", stopwatch.Elapsed.TotalMilliseconds }, { "bytes", csv?.Length ?? 0 } },
                Properties = { { "symbol", symbol }, { "path", "download" }, { "browser", isLinux ? "chromium" : "firefox" } }
            });
            return csv;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "HistoricalDataFetcher download API failed for {Symbol}, trying response sniffing", symbol);
            telemetry?.TrackException(ex, new Dictionary<string, string>
            {
                { "stage", "download-api" },
                { "symbol", symbol }
            });

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
                var csv = System.Text.Encoding.UTF8.GetString(body);
                stopwatch.Stop();
                logger?.LogInformation("HistoricalDataFetcher succeeded via response sniff for {Symbol} in {ElapsedMs}ms, bytes={Length}", symbol, stopwatch.Elapsed.TotalMilliseconds, csv?.Length ?? 0);
                telemetry?.TrackEvent(new EventTelemetry("HistoricalDataFetcher.Success")
                {
                    Metrics = { { "elapsedMs", stopwatch.Elapsed.TotalMilliseconds }, { "bytes", csv?.Length ?? 0 } },
                    Properties = { { "symbol", symbol }, { "path", "response" }, { "browser", isLinux ? "chromium" : "firefox" } }
                });
                return csv;
            }
            catch (Exception ex2)
            {
                stopwatch.Stop();
                logger?.LogError(ex2, "HistoricalDataFetcher failed completely for {Symbol}", symbol);
                telemetry?.TrackException(ex2, new Dictionary<string, string>
                {
                    { "stage", "response-sniff" },
                    { "symbol", symbol }
                });
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
        yield return p => p.GetByText(new Regex("Download\\s+(historical|csv)", RegexOptions.IgnoreCase));
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

    private static Task EnsurePlaywrightInstalledAsync()
    {
        if (Interlocked.Exchange(ref _installOnce, 1) == 1)
            return Task.CompletedTask;

        try
        {
            Microsoft.Playwright.Program.Main(new[] { "install" });
        }
        catch
        {
            // Best-effort
        }

        return Task.CompletedTask;
    }
}
