using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace AiStockTradeApp.Controllers
{
    /// <summary>
    /// Account controller for user authentication and account management
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IStringLocalizer<SharedResource> localizer,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _localizer = localizer;
            _logger = logger;
        }

        /// <summary>
        /// Display login page
        /// </summary>
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Title"] = _localizer["Account_Login_Title"];
            return View();
        }

        /// <summary>
        /// Process login form submission
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Title"] = _localizer["Account_Login_Title"];

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Try to sign in the user
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, 
                    model.Password, 
                    model.RememberMe, 
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    // Update last login time
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    if (user != null)
                    {
                        user.LastLoginAt = DateTime.UtcNow;
                        await _userManager.UpdateAsync(user);
                    }

                    _logger.LogInformation("User {Email} logged in successfully", model.Email);
                    return RedirectToLocal(returnUrl);
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User {Email} account locked out", model.Email);
                    ModelState.AddModelError(string.Empty, _localizer["Account_Login_LockedOut"]);
                }
                else
                {
                    _logger.LogWarning("Invalid login attempt for {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, _localizer["Account_Login_InvalidCredentials"]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, _localizer["Account_Login_Error"]);
            }

            return View(model);
        }

        /// <summary>
        /// Display registration page
        /// </summary>
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Title"] = _localizer["Account_Register_Title"];
            return View();
        }

        /// <summary>
        /// Process registration form submission
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["Title"] = _localizer["Account_Register_Title"];

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PreferredCulture = GetCurrentCulture(),
                    CreatedAt = DateTime.UtcNow,
                    EnablePriceAlerts = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {Email} created successfully", model.Email);

                    // Automatically sign in the user after registration
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    
                    return RedirectToLocal(returnUrl);
                }

                // Add any registration errors to model state
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, _localizer["Account_Register_Error"]);
            }

            return View(model);
        }

        /// <summary>
        /// Logout the current user
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
                _logger.LogInformation("User logged out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
            }

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Display access denied page
        /// </summary>
        [HttpGet]
        public IActionResult AccessDenied()
        {
            ViewData["Title"] = _localizer["Account_AccessDenied_Title"];
            return View();
        }

        /// <summary>
        /// Display user profile page
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var model = new UserProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email!,
                PreferredCulture = user.PreferredCulture,
                EnablePriceAlerts = user.EnablePriceAlerts,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };

            ViewData["Title"] = _localizer["Account_Profile_Title"];
            return View(model);
        }

        /// <summary>
        /// Update user profile
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserProfileViewModel model)
        {
            ViewData["Title"] = _localizer["Account_Profile_Title"];

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login");
                }

                user.FirstName = model.FirstName?.Trim();
                user.LastName = model.LastName?.Trim();
                user.PreferredCulture = model.PreferredCulture;
                user.EnablePriceAlerts = model.EnablePriceAlerts;

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    ViewBag.Message = _localizer["Account_Profile_UpdateSuccess"];
                    _logger.LogInformation("User {Email} updated profile successfully", user.Email);
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", User.Identity?.Name);
                ModelState.AddModelError(string.Empty, _localizer["Account_Profile_UpdateError"]);
            }

            return View(model);
        }

        /// <summary>
        /// Helper method to redirect to local URL or home page
        /// </summary>
        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Get current culture from localization context
        /// </summary>
        private string GetCurrentCulture()
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }
    }
}
