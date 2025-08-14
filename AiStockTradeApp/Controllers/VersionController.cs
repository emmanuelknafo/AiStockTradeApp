using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ai_stock_trade_app.Controllers;

[ApiController]
[Route("version")] // /version
public class VersionController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public VersionController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var file = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "unknown";
        var product = asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var appVersion = _configuration["APP_VERSION"];
        var result = new
        {
            version = info,
            fileVersion = file,
            product,
            appVersion = string.IsNullOrWhiteSpace(appVersion) ? null : appVersion,
            timestampUtc = DateTime.UtcNow,
        };
        return Ok(result);
    }
}
