using OpenQA.Selenium;

namespace AiStockTradeApp.SeleniumTests.Pages;

public class AuthPage
{
    private readonly IWebDriver _driver;

    public AuthPage(IWebDriver driver) => _driver = driver;

    // Updated selectors to reflect current custom Account/Login page (no Identity scaffolding prefix)
    private By UsernameInput => By.CssSelector("input[name='Email'], #Email, [data-testid='login-email']");
    private By PasswordInput => By.CssSelector("input[name='Password'], #Password, [data-testid='login-password']");
    private By LoginButton => By.CssSelector("button[type='submit'], [data-testid='login-submit']");
    private By SignOutButton => By.CssSelector("[data-testid='signout-btn'], a[href*='/Account/Logout']");

    public void SignIn(string baseUrl, string username, string password)
    {
    // Navigate to new login route (custom Account controller)
    _driver.Navigate().GoToUrl(baseUrl.TrimEnd('/') + "/Account/Login");
        _driver.FindElement(UsernameInput).SendKeys(username);
        _driver.FindElement(PasswordInput).SendKeys(password);
        _driver.FindElement(LoginButton).Click();
    }

    public void SignOut(string baseUrl)
    {
        _driver.Navigate().GoToUrl(baseUrl.TrimEnd('/') + "/");
        var signOut = _driver.FindElements(SignOutButton).FirstOrDefault();
        signOut?.Click();
    }
}
