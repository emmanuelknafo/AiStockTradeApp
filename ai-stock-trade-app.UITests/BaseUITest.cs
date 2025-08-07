using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace ai_stock_trade_app.UITests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class BaseUITest : PageTest
{
    protected string BaseUrl = "http://localhost:5259"; // Updated to match launchSettings.json
    
    [OneTimeSetUp]
    public void GlobalSetup()
    {
        // Note: Run 'playwright install' as a separate step in CI/CD or locally
        // Program.Main(new[] { "install" }); // This line is not needed
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
        await Page.GotoAsync(BaseUrl);
    }

    protected async Task NavigateToStockDashboard()
    {
        await Page.GotoAsync($"{BaseUrl}/Stock/Dashboard");
    }

    protected async Task WaitForPageLoad()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}