using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.ViewModels;
using AiStockTradeApp.Services;
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
                _logger.LogAuthenticationEvent(LoggingConstants.UserLogin, model.Email, false, 
                    "Invalid model state", new { ModelErrors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
                return View(model);
            }

            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
                
                _logger.LogAuthenticationEvent(LoggingConstants.UserLogin, model.Email, true, 
                    null, new { IpAddress = ipAddress, UserAgent = userAgent, ReturnUrl = returnUrl });

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

                    _logger.LogLoginAttempt(model.Email, true, ipAddress, userAgent);
                    _logger.LogInformation("User {Email} logged in successfully from {IpAddress}", model.Email, ipAddress);
                    return RedirectToLocal(returnUrl);
                }

                if (result.IsLockedOut)
                {
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    var lockoutEnd = user?.LockoutEnd?.DateTime ?? DateTime.UtcNow.AddMinutes(5);
                    var failedCount = await _userManager.GetAccessFailedCountAsync(user!);
                    
                    _logger.LogAccountLockout(model.Email, lockoutEnd, failedCount, 
                        $"Login attempt from {ipAddress}");
                    _logger.LogLoginAttempt(model.Email, false, ipAddress, userAgent, "Account locked out");
                    ModelState.AddModelError(string.Empty, _localizer["Account_Login_LockedOut"]);
                }
                else if (result.IsNotAllowed)
                {
                    _logger.LogLoginAttempt(model.Email, false, ipAddress, userAgent, "Account not allowed (email not confirmed)");
                    ModelState.AddModelError(string.Empty, _localizer["Account_Login_NotAllowed"]);
                }
                else if (result.RequiresTwoFactor)
                {
                    _logger.LogAuthenticationEvent(LoggingConstants.TwoFactorAuthentication, model.Email, true, 
                        null, new { IpAddress = ipAddress });
                    // Handle two-factor authentication if implemented
                    ModelState.AddModelError(string.Empty, "Two-factor authentication required");
                }
                else
                {
                    _logger.LogLoginAttempt(model.Email, false, ipAddress, userAgent, "Invalid credentials");
                    ModelState.AddModelError(string.Empty, _localizer["Account_Login_InvalidCredentials"]);
                }
            }
            catch (Exception ex)
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                _logger.LogError(ex, "Error during login for {Email} from {IpAddress}", model.Email, ipAddress);
                _logger.LogAuthenticationEvent(LoggingConstants.UserLogin, model.Email, false, 
                    ex.Message, new { IpAddress = ipAddress, Exception = ex.GetType().Name });
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
                _logger.LogAuthenticationEvent(LoggingConstants.UserRegistration, model.Email, false, 
                    "Invalid model state", new { ModelErrors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
                return View(model);
            }

            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
                
                _logger.LogAuthenticationEvent(LoggingConstants.UserRegistration, model.Email, true, 
                    null, new { IpAddress = ipAddress, UserAgent = userAgent, FirstName = model.FirstName, LastName = model.LastName });

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    _logger.LogRegistrationAttempt(model.Email, false, 
                        new[] { "User with this email already exists" }, 
                        new { IpAddress = ipAddress, AttemptedFirstName = model.FirstName, AttemptedLastName = model.LastName });
                    ModelState.AddModelError(string.Empty, "User with this email already exists");
                    return View(model);
                }

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
                    _logger.LogRegistrationAttempt(model.Email, true, null, 
                        new { IpAddress = ipAddress, UserId = user.Id, UserName = user.UserName });
                    _logger.LogInformation("User {Email} created successfully with ID {UserId} from {IpAddress}", 
                        model.Email, user.Id, ipAddress);

                    // Automatically sign in the user after registration
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    
                    _logger.LogLoginAttempt(model.Email, true, ipAddress, userAgent);
                    return RedirectToLocal(returnUrl);
                }

                // Add any registration errors to model state
                var errors = result.Errors.Select(e => e.Description).ToList();
                _logger.LogRegistrationAttempt(model.Email, false, errors, 
                    new { IpAddress = ipAddress, AttemptedFirstName = model.FirstName, AttemptedLastName = model.LastName });

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    _logger.LogWarning("Registration validation error for {Email}: {Code} - {Description}", 
                        model.Email, error.Code, error.Description);
                }
            }
            catch (Exception ex)
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                _logger.LogError(ex, "Error during registration for {Email} from {IpAddress}", model.Email, ipAddress);
                _logger.LogAuthenticationEvent(LoggingConstants.UserRegistration, model.Email, false, 
                    ex.Message, new { IpAddress = ipAddress, Exception = ex.GetType().Name, StackTrace = ex.StackTrace });
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
                var userEmail = User.Identity?.Name;
                var userId = _userManager.GetUserId(User);
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                
                await _signInManager.SignOutAsync();
                
                _logger.LogAuthenticationEvent(LoggingConstants.UserLogout, userEmail ?? userId ?? "Unknown", true, 
                    null, new { IpAddress = ipAddress, UserId = userId });
                _logger.LogInformation("User {UserEmail} (ID: {UserId}) logged out from {IpAddress}", 
                    userEmail, userId, ipAddress);
            }
            catch (Exception ex)
            {
                var userEmail = User.Identity?.Name;
                var userId = _userManager.GetUserId(User);
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                
                _logger.LogError(ex, "Error during logout for user {UserEmail} (ID: {UserId}) from {IpAddress}", 
                    userEmail, userId, ipAddress);
                _logger.LogAuthenticationEvent(LoggingConstants.UserLogout, userEmail ?? userId ?? "Unknown", false, 
                    ex.Message, new { IpAddress = ipAddress, Exception = ex.GetType().Name });
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
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
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
