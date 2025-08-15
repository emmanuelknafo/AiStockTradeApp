using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace AiStockTradeApp.UITests;

[Parallelizable(ParallelScope.None)] // Disable parallelization to prevent Playwright concurrency issues
[TestFixture]
[CancelAfter(120000)] // Set 2-minute default timeout for all UI tests
public class BaseUITest : PageTest
{
    protected string BaseUrl;

    public BaseUITest()
    {
        // Use environment variable for base URL, fallback to standard HTTP dev port (matches auto-start logic)
        BaseUrl = Environment.GetEnvironmentVariable("PLAYWRIGHT_BASE_URL") ?? "http://localhost:5000";
    }

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions()
        {
            // Ignore SSL certificate errors for localhost development (both HTTP and HTTPS)
            IgnoreHTTPSErrors = true,
            // Set viewport size
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
            // Additional options for better test stability
            Locale = "en-US",
            TimezoneId = "America/New_York"
        };
    }

    // Remove the 'override' modifier from the LaunchOptions method declaration
    public BrowserTypeLaunchOptions LaunchOptions()
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[]
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
            }
        };

        // Additional options for CI environments
        var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_PIPELINES"));

        if (isCI)
        {
            Console.WriteLine("Running in CI environment - applying CI-specific browser options");
            launchOptions.Timeout = 60000; // 60 seconds timeout
        }

        return launchOptions;
    }

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        // Check if the application is running before starting tests
        await TestSetupHelper.WaitForApplicationStartup(BaseUrl, timeoutSeconds: 10);
    }

    [OneTimeTearDown]
    public void GlobalTeardown()
    {
        TestSetupHelper.StopIfStartedByTests();
    }

    [SetUp]
    public async Task Setup()
    {
        // Configure test settings
        await Context.Tracing.StartAsync(new()
        {
            Title = TestContext.CurrentContext.Test.Name,
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        // Set sensible defaults for CI where machines may be slower
        try
        {
            Page.SetDefaultTimeout(15000);
            Page.SetDefaultNavigationTimeout(20000);
        }
        catch { /* Page might not be ready in rare cases; safe to ignore */ }
    }

    [TearDown]
    public async Task TearDown()
    {
        // Save traces for failed tests
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            var tracePath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "playwright-traces",
                $"{TestContext.CurrentContext.Test.Name}-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(tracePath)!);
            await Context.Tracing.StopAsync(new() { Path = tracePath });
        }
        else
        {
            await Context.Tracing.StopAsync();
        }
    }

    protected async Task<IPage> GetNewPageAsync()
    {
        var page = await Context.NewPageAsync();
        return page;
    }

    protected async Task NavigateToHomePage()
    {
        try
        {
            await Page.GotoAsync(BaseUrl, new PageGotoOptions { Timeout = 10000 });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("ERR_CONNECTION_REFUSED"))
        {
            Assert.Fail($"Cannot connect to application at {BaseUrl}. Please ensure the application is running.\n" +
                       "Start the application with: cd AiStockTradeApp && dotnet run\n" +
                       "Then run the tests again.");
        }
    }

    protected async Task NavigateToStockDashboard()
    {
        try
        {
            await Page.GotoAsync($"{BaseUrl}/Stock/Dashboard", new PageGotoOptions { Timeout = 15000 });
            await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            // Additional wait for JavaScript to initialize
            await Page.WaitForTimeoutAsync(1000);
            // Ensure network quiescence to reduce flakiness on CI
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 10000 });
            // Ensure core controls are present and (if possible) visible before tests proceed
            try
            {
                var clearAll = Page.Locator("#clear-all");
                await clearAll.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 10000 });
                // Visibility may still race with layout; try but don't fail the navigation if it isn't yet visible
                await clearAll.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            }
            catch { /* Non-fatal: element might not be on non-dashboard pages or slower to render */ }
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("ERR_CONNECTION_REFUSED"))
        {
            Assert.Fail($"Cannot connect to application at {BaseUrl}/Stock/Dashboard. Please ensure the application is running.\n" +
                       "Start the application with: cd AiStockTradeApp && dotnet run\n" +
                       "Then run the tests again.");
        }
    }

    protected async Task WaitForPageLoad()
    {
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 15000 });
        // Wait for the main elements to be visible
        await Page.Locator("h1").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        // Additional wait for JavaScript initialization
        await Page.WaitForTimeoutAsync(500);
    }

    protected async Task<ILocator> WaitForLocatorAttachedWithRetry(string selector, int attempts = 2, int perAttemptTimeoutMs = 8000)
    {
        var locator = Page.Locator(selector);
        for (int i = 1; i <= attempts; i++)
        {
            try
            {
                await locator.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = perAttemptTimeoutMs });
                return locator;
            }
            catch
            {
                if (i == attempts) break;
                // Try a lightweight refresh and wait for readiness again
                await Page.ReloadAsync(new() { Timeout = perAttemptTimeoutMs });
                await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = perAttemptTimeoutMs });
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = perAttemptTimeoutMs });
            }
        }

        try
        {
            var html = await Page.ContentAsync();
            var snapshotPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"page-snapshot-{TestContext.CurrentContext.Test.Name}.html");
            File.WriteAllText(snapshotPath, html);
            TestContext.WriteLine($"Saved page snapshot to: {snapshotPath}");
        }
        catch { /* best-effort */ }

        throw new TimeoutException($"Timed out waiting for selector '{selector}' to be attached after {attempts} attempts.");
    }
}