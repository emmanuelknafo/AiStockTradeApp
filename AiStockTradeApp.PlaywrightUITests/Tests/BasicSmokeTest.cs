using NUnit.Framework;
using Microsoft.Playwright;

namespace AiStockTradeApp.PlaywrightUITests.Tests;

[TestFixture]
public class BasicSmokeTest : BaseUITest
{
    [Test]
    public async Task Application_ShouldLoad_AndReturnValidResponse()
    {
        // Simple test that just checks if the application loads
        await Page.GotoAsync(BaseUrl);
        
        // Wait for the page to load and check title
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var title = await Page.TitleAsync();
        Assert.That(title, Is.Not.Null.And.Not.Empty);
        
        // Check if the main container is present without complex operations
        var bodyContent = await Page.TextContentAsync("body");
        Assert.That(bodyContent, Is.Not.Null);
        
        Console.WriteLine($"Page loaded successfully with title: {title}");
    }

    [Test]
    public async Task Application_ShouldHave_BasicElements()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Check for basic HTML elements using simple queries
        var hasHeader = await Page.Locator("h1").CountAsync() > 0;
        var hasInput = await Page.Locator("input").CountAsync() > 0;
        var hasButton = await Page.Locator("button").CountAsync() > 0;
        
        Assert.That(hasHeader, Is.True, "Should have at least one h1 element");
        Assert.That(hasInput, Is.True, "Should have at least one input element");
        Assert.That(hasButton, Is.True, "Should have at least one button element");
        
        Console.WriteLine("Basic elements found on page");
    }
}
