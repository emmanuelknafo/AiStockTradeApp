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
        return RedirectToAction("Dashboard", "Stock");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // Debug action to test localization
    public IActionResult TestLocalization()
    {
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

            return Json(testResults);
        }
        catch (Exception ex)
        {
            return Json(new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(culture)) culture = "en";
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
        );
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Dashboard", "Stock");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
