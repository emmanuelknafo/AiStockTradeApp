using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using Moq;
using FluentAssertions;
using AiStockTradeApp.Controllers;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.ViewModels;
using AiStockTradeApp.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace AiStockTradeApp.Tests.Controllers
{
    public class AccountControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly Mock<IStringLocalizer<SharedResource>> _mockLocalizer;
        private readonly Mock<ILogger<AccountController>> _mockLogger;
        private readonly AccountController _controller;

        public AccountControllerTests()
        {
            // Setup UserManager mock
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            // Setup SignInManager mock
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            var options = new Mock<IOptions<IdentityOptions>>();
            var logger = new Mock<ILogger<SignInManager<ApplicationUser>>>();
            var schemes = new Mock<IAuthenticationSchemeProvider>();
            var confirmation = new Mock<IUserConfirmation<ApplicationUser>>();

            _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
                _mockUserManager.Object,
                contextAccessor.Object,
                userPrincipalFactory.Object,
                options.Object,
                logger.Object,
                schemes.Object,
                confirmation.Object);

            // Setup other mocks
            _mockLocalizer = new Mock<IStringLocalizer<SharedResource>>();
            _mockLogger = new Mock<ILogger<AccountController>>();

            // Setup localizer to return keys as values for testing
            _mockLocalizer.Setup(x => x[It.IsAny<string>()])
                .Returns((string key) => new LocalizedString(key, key));

            _controller = new AccountController(
                _mockUserManager.Object,
                _mockSignInManager.Object,
                _mockLocalizer.Object,
                _mockLogger.Object);

            // Setup HttpContext for IP address tracking
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            context.Request.Headers.UserAgent = "Test User Agent";
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = context
            };
        }

        [Fact]
        public void Login_Get_ShouldReturnView_WhenUserNotAuthenticated()
        {
            // Arrange
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal();

            // Act
            var result = _controller.Login();

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.ViewData["Title"].Should().Be("Account_Login_Title");
        }

        [Fact]
        public void Login_Get_ShouldRedirect_WhenUserAlreadyAuthenticated()
        {
            // Arrange
            var claims = new List<Claim> { new(ClaimTypes.Name, "test@example.com") };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            // Act
            var result = _controller.Login();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
        }

        [Fact]
        public async Task Login_Post_ShouldReturnView_WhenModelStateInvalid()
        {
            // Arrange
            var model = new LoginViewModel { Email = "", Password = "" };
            _controller.ModelState.AddModelError("Email", "Email is required");

            // Act
            var result = await _controller.Login(model);

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.Model.Should().Be(model);
            
            // Verify authentication event was logged
            VerifyAuthenticationEventLogged(LoggingConstants.UserLogin, model.Email, false);
        }

        [Fact]
        public async Task Login_Post_ShouldRedirectToHome_WhenLoginSuccessful()
        {
            // Arrange
            var model = new LoginViewModel { Email = "test@example.com", Password = "Password123!" };
            var user = new ApplicationUser 
            { 
                Email = "test@example.com", 
                UserName = "test@example.com",
                Id = "user-123"
            };

            _mockSignInManager.Setup(x => x.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, true))
                .ReturnsAsync(IdentitySignInResult.Success);

            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email))
                .ReturnsAsync(user);

            _mockUserManager.Setup(x => x.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.Login(model);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirectResult = result as RedirectToActionResult;
            redirectResult!.ActionName.Should().Be("Index");
            redirectResult.ControllerName.Should().Be("Home");

            // Verify user last login was updated
            _mockUserManager.Verify(x => x.UpdateAsync(It.Is<ApplicationUser>(u => u.LastLoginAt.HasValue)), Times.Once);
            
            // Verify successful login was logged
            VerifyLoginAttemptLogged(model.Email, true);
        }

        [Fact]
        public async Task Login_Post_ShouldReturnView_WhenAccountLockedOut()
        {
            // Arrange
            var model = new LoginViewModel { Email = "test@example.com", Password = "Password123!" };
            var user = new ApplicationUser 
            { 
                Email = "test@example.com", 
                UserName = "test@example.com",
                LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5)
            };

            _mockSignInManager.Setup(x => x.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, true))
                .ReturnsAsync(IdentitySignInResult.LockedOut);

            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email))
                .ReturnsAsync(user);

            _mockUserManager.Setup(x => x.GetAccessFailedCountAsync(user))
                .ReturnsAsync(5);

            // Act
            var result = await _controller.Login(model);

            // Assert
            result.Should().BeOfType<ViewResult>();
            _controller.ModelState.Should().ContainKey(string.Empty);
            
            // Verify lockout was logged
            VerifyAccountLockoutLogged(model.Email);
            VerifyLoginAttemptLogged(model.Email, false);
        }

        [Fact]
        public async Task Login_Post_ShouldReturnView_WhenCredentialsInvalid()
        {
            // Arrange
            var model = new LoginViewModel { Email = "test@example.com", Password = "WrongPassword" };

            _mockSignInManager.Setup(x => x.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, true))
                .ReturnsAsync(IdentitySignInResult.Failed);

            // Act
            var result = await _controller.Login(model);

            // Assert
            result.Should().BeOfType<ViewResult>();
            _controller.ModelState.Should().ContainKey(string.Empty);
            
            // Verify failed login was logged
            VerifyLoginAttemptLogged(model.Email, false);
        }

        [Fact]
        public void Register_Get_ShouldReturnView_WhenUserNotAuthenticated()
        {
            // Arrange
            _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal();

            // Act
            var result = _controller.Register();

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.ViewData["Title"].Should().Be("Account_Register_Title");
        }

        [Fact]
        public async Task Register_Post_ShouldReturnView_WhenModelStateInvalid()
        {
            // Arrange
            var model = new RegisterViewModel { Email = "", Password = "", FirstName = "", LastName = "" };
            _controller.ModelState.AddModelError("Email", "Email is required");

            // Act
            var result = await _controller.Register(model);

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.Model.Should().Be(model);
            
            // Verify registration event was logged
            VerifyAuthenticationEventLogged(LoggingConstants.UserRegistration, model.Email, false);
        }

        [Fact]
        public async Task Register_Post_ShouldReturnView_WhenUserAlreadyExists()
        {
            // Arrange
            var model = new RegisterViewModel 
            { 
                Email = "existing@example.com", 
                Password = "Password123!",
                FirstName = "John",
                LastName = "Doe"
            };

            var existingUser = new ApplicationUser { Email = model.Email };
            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email))
                .ReturnsAsync(existingUser);

            // Act
            var result = await _controller.Register(model);

            // Assert
            result.Should().BeOfType<ViewResult>();
            _controller.ModelState.Should().ContainKey(string.Empty);
            
            // Verify failed registration was logged
            VerifyRegistrationAttemptLogged(model.Email, false);
        }

        [Fact]
        public async Task Register_Post_ShouldRedirectToHome_WhenRegistrationSuccessful()
        {
            // Arrange
            var model = new RegisterViewModel 
            { 
                Email = "new@example.com", 
                Password = "Password123!",
                FirstName = "John",
                LastName = "Doe"
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), model.Password))
                .ReturnsAsync(IdentityResult.Success);

            _mockSignInManager.Setup(x => x.SignInAsync(It.IsAny<ApplicationUser>(), false, null))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Register(model);

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirectResult = result as RedirectToActionResult;
            redirectResult!.ActionName.Should().Be("Index");
            redirectResult.ControllerName.Should().Be("Home");
            
            // Verify successful registration was logged
            VerifyRegistrationAttemptLogged(model.Email, true);
        }

        [Fact]
        public async Task Register_Post_ShouldReturnView_WhenRegistrationFails()
        {
            // Arrange
            var model = new RegisterViewModel 
            { 
                Email = "new@example.com", 
                Password = "weak",
                FirstName = "John",
                LastName = "Doe"
            };

            var identityErrors = new[]
            {
                new IdentityError { Code = "PasswordTooShort", Description = "Password is too short" },
                new IdentityError { Code = "PasswordRequiresDigit", Description = "Password requires a digit" }
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), model.Password))
                .ReturnsAsync(IdentityResult.Failed(identityErrors));

            // Act
            var result = await _controller.Register(model);

            // Assert
            result.Should().BeOfType<ViewResult>();
            _controller.ModelState.Should().HaveCount(2);
            
            // Verify failed registration was logged
            VerifyRegistrationAttemptLogged(model.Email, false);
        }

        [Fact]
        public async Task Logout_ShouldSignOutAndRedirectToHome()
        {
            // Arrange
            var claims = new List<Claim> 
            { 
                new(ClaimTypes.Name, "test@example.com"),
                new(ClaimTypes.NameIdentifier, "user-123")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext.HttpContext.User = principal;

            _mockSignInManager.Setup(x => x.SignOutAsync())
                .Returns(Task.CompletedTask);

            _mockUserManager.Setup(x => x.GetUserId(principal))
                .Returns("user-123");

            // Act
            var result = await _controller.Logout();

            // Assert
            result.Should().BeOfType<RedirectToActionResult>();
            var redirectResult = result as RedirectToActionResult;
            redirectResult!.ActionName.Should().Be("Index");
            redirectResult.ControllerName.Should().Be("Home");

            _mockSignInManager.Verify(x => x.SignOutAsync(), Times.Once);
            
            // Verify logout was logged
            VerifyAuthenticationEventLogged(LoggingConstants.UserLogout, "test@example.com", true);
        }

        [Fact]
        public void AccessDenied_ShouldReturnView()
        {
            // Act
            var result = _controller.AccessDenied();

            // Assert
            result.Should().BeOfType<ViewResult>();
            var viewResult = result as ViewResult;
            viewResult!.ViewData["Title"].Should().Be("Account_AccessDenied_Title");
        }

        [Fact]
        public async Task Login_Post_ShouldHandleException_AndReturnViewWithError()
        {
            // Arrange
            var model = new LoginViewModel { Email = "test@example.com", Password = "Password123!" };

            _mockSignInManager.Setup(x => x.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, true))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            // Act
            var result = await _controller.Login(model);

            // Assert
            result.Should().BeOfType<ViewResult>();
            _controller.ModelState.Should().ContainKey(string.Empty);
            
            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Register_Post_ShouldHandleException_AndReturnViewWithError()
        {
            // Arrange
            var model = new RegisterViewModel 
            { 
                Email = "new@example.com", 
                Password = "Password123!",
                FirstName = "John",
                LastName = "Doe"
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            // Act
            var result = await _controller.Register(model);

            // Assert
            result.Should().BeOfType<ViewResult>();
            _controller.ModelState.Should().ContainKey(string.Empty);
            
            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // Helper methods for verifying logging calls
        private void VerifyAuthenticationEventLogged(string eventType, string userIdentifier, bool success)
        {
            _mockLogger.Verify(
                x => x.Log(
                    success ? LogLevel.Information : LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Authentication event: {eventType}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyLoginAttemptLogged(string email, bool success)
        {
            _mockLogger.Verify(
                x => x.Log(
                    success ? LogLevel.Information : LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(success ? "Login successful" : "Login failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyRegistrationAttemptLogged(string email, bool success)
        {
            _mockLogger.Verify(
                x => x.Log(
                    success ? LogLevel.Information : LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(success ? "registration succeeded" : "registration failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyAccountLockoutLogged(string email)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Account lockout triggered")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }
}
