namespace AiStockTradeApp.SeleniumTests.Drivers;

public record Credentials(string Username, string Password);

public class TestSettings
{
    public string BaseUrl { get; set; } = "https://localhost:5001";
    public string Browser { get; set; } = "Chrome";
    public bool Headless { get; set; } = true;
    public int ImplicitWaitMs { get; set; } = 2000;
    public int PageLoadTimeoutSec { get; set; } = 60;
    public Credentials Credentials { get; set; } = new("", "");
    public string Culture { get; set; } = "en";
}
