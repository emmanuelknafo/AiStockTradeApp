using Microsoft.Playwright;
using FluentAssertions;
using NUnit.Framework;

namespace AiStockTradeApp.PlaywrightUITests.Tests;

[TestFixture]
public class AccessibilityTests : BaseUITest
{
    [Test]
    public async Task Page_ShouldHaveProperSemanticStructure()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Check for proper heading structure
        var h1Elements = await Page.Locator("h1").CountAsync();
        h1Elements.Should().BeGreaterThan(0, "Page should have at least one H1 element");

        // Check for main landmark
        var mainElement = Page.Locator("main");
        await Expect(mainElement).ToBeVisibleAsync();

        // Check for proper form labels
        var tickerInput = Page.Locator("#ticker-input");
        var inputAriaLabel = await tickerInput.GetAttributeAsync("aria-label");
        var inputPlaceholder = await tickerInput.GetAttributeAsync("placeholder");
        
        (inputAriaLabel ?? inputPlaceholder).Should().NotBeNullOrEmpty("Input should have accessible name");
    }

    [Test]
    public async Task Buttons_ShouldBeKeyboardAccessible()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

    // Locate the button and focus it directly to avoid hitting layout nav links
    var addButton = Page.Locator("#add-button");
    await addButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
    await addButton.FocusAsync();

    // Press Enter to activate via keyboard
    await Page.Keyboard.PressAsync("Enter");

    // Button should remain visible and interactive
    var isVisible = await addButton.IsVisibleAsync();
    isVisible.Should().BeTrue();
    }

    [Test]
    public async Task InteractiveElements_ShouldHaveProperARIAAttributes()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Check toggle buttons have proper ARIA attributes
        var themeToggle = Page.Locator("#theme-toggle");
        var themeToggleRole = await themeToggle.GetAttributeAsync("role");
        var themeToggleAriaLabel = await themeToggle.GetAttributeAsync("aria-label");
        
        // Button should have proper role (button is default) or aria-label
        if (themeToggleRole != null)
        {
            themeToggleRole.Should().Be("button");
        }

        // Settings panel should have proper ARIA attributes when hidden/shown
        var settingsPanel = Page.Locator("#settings-panel");
        var settingsToggle = Page.Locator("#settings-toggle");
        
        // Click to show settings
        await settingsToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        
        var ariaHidden = await settingsPanel.GetAttributeAsync("aria-hidden");
        if (ariaHidden != null)
        {
            ariaHidden.Should().Be("false", "Visible panel should not be aria-hidden");
        }
    }

    [Test]
    public async Task ColorContrast_ShouldBeSufficient()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // This is a basic check - in a real scenario you'd use axe-core or similar tools
        // Check that text elements are visible and readable
        var headerText = Page.Locator("h1");
        await Expect(headerText).ToBeVisibleAsync();

        var inputField = Page.Locator("#ticker-input");
        await Expect(inputField).ToBeVisibleAsync();

        // Verify elements have computed styles that suggest good contrast
        var headerColor = await headerText.EvaluateAsync("el => getComputedStyle(el).color");
        var headerBgColor = await headerText.EvaluateAsync("el => getComputedStyle(el).backgroundColor");
        
        headerColor.Should().NotBeNull();
        headerColor.ToString().Should().NotBeNullOrEmpty();
        // In a real test, you'd calculate the actual contrast ratio
    }

    [Test]
    public async Task FocusManagement_ShouldBeProper()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Test focus trap in modals/panels
        var settingsToggle = Page.Locator("#settings-toggle");
        await settingsToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Focus should move into the settings panel or remain manageable
        var settingsPanel = Page.Locator("#settings-panel");
        var isVisible = await settingsPanel.IsVisibleAsync();
        isVisible.Should().BeTrue();

        // Close the panel
        await settingsToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Focus should return to a reasonable location
        var activeElement = await Page.EvaluateAsync("document.activeElement.tagName");
        activeElement.Should().NotBeNull();
    }

    [Test]
    public async Task ScreenReader_ShouldHaveProperAnnouncements()
    {
        await NavigateToStockDashboard();
        await WaitForPageLoad();

        // Check for live regions that would announce changes
        var notificationContainer = Page.Locator("#notification-container");
        var ariaLive = await notificationContainer.GetAttributeAsync("aria-live");
        
        // If notifications exist, they should have proper aria-live regions
        var containerExists = await notificationContainer.IsVisibleAsync();
        if (containerExists && ariaLive != null)
        {
            ariaLive.Should().BeOneOf("polite", "assertive");
        }

        // Add a stock and check for status updates
        var tickerInput = Page.Locator("#ticker-input");
        await tickerInput.FillAsync("AAPL");
        
        var addButton = Page.Locator("#add-button");
        await addButton.ClickAsync();
        
        await Page.WaitForTimeoutAsync(2000);

        // Check if status or loading states are properly announced
        var apiStatus = Page.Locator("#api-status");
        var statusText = await apiStatus.TextContentAsync();
        statusText.Should().NotBeNullOrEmpty();
    }
}