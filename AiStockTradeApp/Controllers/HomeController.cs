using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AiStockTradeApp.Entities.ViewModels;
using Microsoft.AspNetCore.Localization;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace AiStockTradeApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IStringLocalizer<SharedResource> _directLocalizer;

    public HomeController(ILogger<HomeController> logger, IStringLocalizerFactory localizerFactory, IStringLocalizer<SharedResource> directLocalizer)
    {
        _logger = logger;
        _localizerFactory = localizerFactory;
        _directLocalizer = directLocalizer;
    }

    public IActionResult Index()
    {
        _logger.LogInformation("Home page accessed, redirecting to Stock Dashboard");
        return RedirectToAction("Dashboard", "Stock");
    }

    public IActionResult Privacy()
    {
        _logger.LogDebug("Privacy page accessed");
        return View();
    }

    // Debug action to test localization
    public IActionResult TestLocalization()
    {
        _logger.LogInformation("Localization test accessed. Current culture: {Culture}", CultureInfo.CurrentUICulture.Name);
        
        try
        {
            var currentCulture = CultureInfo.CurrentUICulture.Name;
            
            var testResults = new
            {
                CurrentCulture = currentCulture,
                DirectApproach = new
                {
                    HeaderTitle = _directLocalizer["Header_Title"].Value,
                    HeaderTitleResourceNotFound = _directLocalizer["Header_Title"].ResourceNotFound,
                    AppTitle = _directLocalizer["App_Title"].Value,
                    AppTitleResourceNotFound = _directLocalizer["App_Title"].ResourceNotFound,
                    AllKeysCount = _directLocalizer.GetAllStrings(true).Count(),
                    LocalizerType = _directLocalizer.GetType().Name
                },
                SampleKeys = _directLocalizer.GetAllStrings(true).Take(10).Select(x => new { Key = x.Name, Value = x.Value, ResourceNotFound = x.ResourceNotFound }).ToList()
            };

            _logger.LogDebug("Localization test completed successfully for culture {Culture}", currentCulture);
            return Json(testResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during localization test");
            return Json(new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string? returnUrl = null)
    {
        var originalCulture = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrWhiteSpace(culture)) culture = "en";
        
        _logger.LogInformation("Language change requested from {OriginalCulture} to {NewCulture} for session {SessionId}", 
            originalCulture, culture, HttpContext.Session.Id);
            
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
        );
        
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            _logger.LogDebug("Redirecting to return URL: {ReturnUrl}", returnUrl);
            return Redirect(returnUrl);
        }
        
        _logger.LogDebug("Redirecting to Stock Dashboard after language change");
        return RedirectToAction("Dashboard", "Stock");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
