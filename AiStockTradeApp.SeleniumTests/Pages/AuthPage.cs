using OpenQA.Selenium;

namespace AiStockTradeApp.SeleniumTests.Pages;

public class AuthPage
{
    private readonly IWebDriver _driver;

    public AuthPage(IWebDriver driver) => _driver = driver;

    private By UsernameInput => By.CssSelector("input[name='Input.Email'], #Input_Email, [data-testid='login-username']");
    private By PasswordInput => By.CssSelector("input[name='Input.Password'], #Input_Password, [data-testid='login-password']");
    private By LoginButton => By.CssSelector("button[type='submit'], [data-testid='login-submit']");
    private By SignOutButton => By.CssSelector("[data-testid='signout-btn'], a[href*='/Account/Logout']");

    public void SignIn(string baseUrl, string username, string password)
    {
        _driver.Navigate().GoToUrl(baseUrl.TrimEnd('/') + "/Identity/Account/Login");
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
