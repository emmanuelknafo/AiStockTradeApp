using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace AiStockTradeApp.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(cfg =>
        {
            cfg.SetApplicationName("aistock-cli");
            cfg.AddCommand<DownloadHistoricalCommand>("download-historical")
               .WithDescription("Download historical stock data CSV from nasdaq.com")
               .WithExample(new[] { "download-historical", "--symbol", "GOOG", "--dest", "C:/tmp/goog.csv" })
               .WithExample(new[] { "download-historical", "-s", "MSFT", "-d", "./msft.csv" });
        });
        return await app.RunAsync(args);
    }
}

public sealed class DownloadHistoricalCommandSettings : CommandSettings
{
    [CommandOption("-s|--symbol <SYMBOL>")]
    [Description("Stock ticker symbol (e.g., GOOG)")]
    public string Symbol { get; init; } = string.Empty;

    [CommandOption("-d|--dest <FILEPATH>")]
    [Description("Destination CSV file path")]
    public string Destination { get; init; } = string.Empty;

    [CommandOption("--headful")] 
    [Description("Run browser non-headless for debugging")] 
    public bool Headful { get; init; }

    [CommandOption("--timeoutSec <SECONDS>")]
    [Description("Overall operation timeout in seconds (default 60)")]
    [DefaultValue(60)]
    public int TimeoutSec { get; init; } = 60;

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Symbol))
            return ValidationResult.Error("--symbol is required");
        if (string.IsNullOrWhiteSpace(Destination))
            return ValidationResult.Error("--dest is required");
        return ValidationResult.Success();
    }
}

public sealed class DownloadHistoricalCommand : AsyncCommand<DownloadHistoricalCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DownloadHistoricalCommandSettings settings)
    {
        var symbol = settings.Symbol.Trim().ToLowerInvariant();
        var url = $"https://www.nasdaq.com/market-activity/stocks/{symbol}/historical?page=1&rows_per_page=10&timeline=y10";
        var destPath = Path.GetFullPath(settings.Destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        AnsiConsole.MarkupLine($"[green]Navigating:[/] {url}");
        AnsiConsole.MarkupLine($"[green]Will save to:[/] {destPath}");
    var timeoutMs = Math.Max(10, settings.TimeoutSec) * 1000;

        // Setup Playwright
        using var pw = await Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new()
        {
            Headless = !settings.Headful,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
        });
        var bctx = await browser.NewContextAsync(new()
        {
            AcceptDownloads = true
        });
        var page = await bctx.NewPageAsync();
        page.SetDefaultTimeout(timeoutMs);

        // Navigate with a more permissive load state (networkidle can hang on analytics)
        try
        {
            await page.GotoAsync(url, new() { Timeout = timeoutMs, WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        catch (TimeoutException)
        {
            // Best effort: continue; page may have loaded enough.
        }
        // Give a brief moment for late scripts
        await page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 5000 });

    // Attempt to accept cookie banner if present (common variants)
        await page.TryAcceptCookiesAsync(acceptTimeoutMs: 12000);

        // Nasdaq page uses a control like "Download historical data" or "Download CSV"
        // Build several candidates and pick the first visible one, waiting briefly for it
        var candidates = new ILocator[]
        {
            page.GetByRole(AriaRole.Button, new() { Name = "Download historical data" }),
            page.GetByRole(AriaRole.Link,   new() { Name = "Download historical data" }),
            page.GetByRole(AriaRole.Button, new() { NameRegex = new("(?i)download.*(historical|csv)") }),
            page.GetByRole(AriaRole.Link,   new() { NameRegex = new("(?i)download.*(historical|csv)") }),
            page.GetByText(new Regex("Download\\s+(historical|csv)", RegexOptions.IgnoreCase)),
            page.Locator(":has-text('Download historical data')"),
            page.Locator(":has-text('Download CSV')"),
        };
        ILocator? downloadControl = null;
        foreach (var c in candidates)
        {
            try
            {
                await c.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                if (await c.First.IsVisibleAsync()) { downloadControl = c.First; break; }
            }
            catch { /* try next */ }
        }

        if (downloadControl is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find a visible download control. The page layout may have changed.[/]");
            AnsiConsole.MarkupLine("[yellow]Tip:[/] Try running with --headful to inspect or update selectors.");
            await browser.CloseAsync();
            return 2;
        }

        await downloadControl.ScrollIntoViewIfNeededAsync();
        try { await downloadControl.HoverAsync(new() { Timeout = 1500 }); } catch { }

        // Strategy 1: Use the browser download event
        try
        {
            var dl = await page.RunAndWaitForDownloadAsync(async () =>
            {
                await downloadControl.ClickAsync(new() { Force = true, Timeout = 8000 });
            }, new() { Timeout = timeoutMs });
            await dl.SaveAsAsync(destPath);
            AnsiConsole.MarkupLine("[green]Download completed (browser event).[/]");
        }
        catch (TimeoutException)
        {
            // Strategy 2: Fallback to capturing the CSV network response
            AnsiConsole.MarkupLine("[yellow]No browser download event detected. Falling back to network capture...[/]");
            await downloadControl.ClickAsync(new() { Force = true, Timeout = 8000 });

            var response = await page.WaitForResponseAsync(r =>
            {
                try
                {
                    var url = r.Url ?? string.Empty;
                    var ct = r.Headers.TryGetValue("content-type", out var v) ? v : string.Empty;
                    return url.Contains(".csv", StringComparison.OrdinalIgnoreCase)
                           || url.Contains("download", StringComparison.OrdinalIgnoreCase)
                           || (!string.IsNullOrEmpty(ct) && ct.Contains("text/csv", StringComparison.OrdinalIgnoreCase));
                }
                catch { return false; }
            }, new() { Timeout = timeoutMs / 2 });

            await response.FinishedAsync();
            var body = await response.BodyAsync();
            await File.WriteAllBytesAsync(destPath, body);
            AnsiConsole.MarkupLine("[green]Download completed (network capture).[/]");
        }

    await browser.CloseAsync();
        return 0;
    }
}

internal static class PageExtensions
{
    public static async Task TryAcceptCookiesAsync(this IPage page, int acceptTimeoutMs = 7000)
    {
        try
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, acceptTimeoutMs));
            // Ensure banner is in view (often at bottom)
            try { await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)"); } catch { }

            static IEnumerable<Func<IFrame, ILocator>> CandidateLocators(IPage page)
            {
                // Locators built from a given frame; main page will be included in Frames enumeration too
                yield return f => f.Locator("#onetrust-accept-btn-handler");
                yield return f => f.GetByRole(AriaRole.Button, new() { NameRegex = new("(?i)accept.*cookies") });
                yield return f => f.GetByText("Accept all cookies", new() { Exact = false });
                yield return f => f.Locator("button:has-text('Accept all cookies')");
                yield return f => f.Locator("button:has-text('I Accept')");
                yield return f => f.Locator("button:has-text('Accept')");
                // Some sites use shadow containers; Playwright still surfaces buttons when accessible
            }

            while (DateTime.UtcNow < deadline)
            {
                var frames = page.Frames;
                foreach (var frame in frames)
                {
                    foreach (var make in CandidateLocators(page))
                    {
                        try
                        {
                            var loc = make(frame);
                            await loc.ScrollIntoViewIfNeededAsync();
                            await loc.ClickAsync(new() { Timeout = 800, Force = true });
                            // brief settle
                            await page.WaitForTimeoutAsync(300);
                            return; // Success
                        }
                        catch { /* try next */ }
                    }
                }
                // small delay before retrying
                await page.WaitForTimeoutAsync(400);
            }
        }
        catch
        {
            // Swallow any unexpected issues; cookie consent is best-effort.
        }
    }
}
