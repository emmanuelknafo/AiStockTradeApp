namespace AiStockTradeApp.SeleniumTests.Drivers;

public record Credentials(string Username, string Password);

public class TestSettings
{
    public string BaseUrl { get; init; } = "https://localhost:5001";
    public string Browser { get; init; } = "Chrome";
    public bool Headless { get; init; } = true;
    public int ImplicitWaitMs { get; init; } = 2000;
    public int PageLoadTimeoutSec { get; init; } = 60;
    public Credentials Credentials { get; init; } = new("", "");
    public string Culture { get; init; } = "en";
}
