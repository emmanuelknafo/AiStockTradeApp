using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Microsoft.Playwright;

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
        page.SetDefaultTimeout(30000);

        // Navigate and click the Download button
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Nasdaq page uses a button/link that contains text like "Download historical data"
        // We wait for the visible button and trigger the download
        var button = page.GetByRole(AriaRole.Button, new() { Name = "Download historical data" });
        if (!await button.IsVisibleAsync())
        {
            // Fallbacks: anchor link or text locator
            var link = page.GetByRole(AriaRole.Link, new() { Name = "Download historical data" });
            if (await link.IsVisibleAsync())
            {
                button = link;
            }
            else
            {
                var textBtn = page.GetByText("Download historical data", new() { Exact = false });
                if (await textBtn.IsVisibleAsync())
                {
                    button = textBtn;
                }
            }
        }

        if (!await button.IsVisibleAsync())
        {
            AnsiConsole.MarkupLine("[red]Could not find the 'Download historical data' control. The page layout may have changed.[/]");
            AnsiConsole.MarkupLine("[yellow]Tip:[/] Try running with --headful to inspect the page, or update the selector.");
            await browser.CloseAsync();
            return 2;
        }

        await button.ScrollIntoViewIfNeededAsync();

        // Start waiting for a download event before clicking
    var waitForDownload = page.WaitForDownloadAsync();
    await button.ClickAsync(new() { Force = true });
    var download = await waitForDownload;

        // Save the file to destination
        await download.SaveAsAsync(destPath);
        AnsiConsole.MarkupLine("[green]Download completed.[/]");

    await browser.CloseAsync();
        return 0;
    }
}
