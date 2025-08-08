using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace ai_stock_trade_app.UITests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class BaseUITest : PageTest
{
    protected string BaseUrl;
    
    public BaseUITest()
    {
        // Use environment variable for base URL, fallback to local HTTPS development URL
        BaseUrl = Environment.GetEnvironmentVariable("PLAYWRIGHT_BASE_URL") ?? "https://localhost:7043";
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
    
    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        // Check if the application is running before starting tests
        await TestSetupHelper.WaitForApplicationStartup(BaseUrl, timeoutSeconds: 10);
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
                       "Start the application with: cd ai-stock-trade-app && dotnet run\n" +
                       "Then run the tests again.");
        }
    }

    protected async Task NavigateToStockDashboard()
    {
        try
        {
            await Page.GotoAsync($"{BaseUrl}/Stock/Dashboard", new PageGotoOptions { Timeout = 10000 });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("ERR_CONNECTION_REFUSED"))
        {
            Assert.Fail($"Cannot connect to application at {BaseUrl}/Stock/Dashboard. Please ensure the application is running.\n" +
                       "Start the application with: cd ai-stock-trade-app && dotnet run\n" +
                       "Then run the tests again.");
        }
    }

    protected async Task WaitForPageLoad()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });
    }
}