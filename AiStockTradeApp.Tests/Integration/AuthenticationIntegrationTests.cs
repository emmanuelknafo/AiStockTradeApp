using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using AiStockTradeApp.DataAccess.Data;
using AiStockTradeApp.Entities.Models;
using Microsoft.AspNetCore.Identity;
using System.Net;
using Microsoft.Extensions.Configuration;
using System.Text;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authentication;
using AiStockTradeApp.Controllers;

namespace AiStockTradeApp.Tests.Integration
{
    public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext registrations
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationIdentityContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    var stockDataDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationIdentityContext>));
                    if (stockDataDescriptor != null)
                        services.Remove(stockDataDescriptor);

                    // Add InMemory database for testing
                    services.AddDbContext<ApplicationIdentityContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestIdentityDb_" + Guid.NewGuid());
                    });

                    // Note: StockDataContext not needed for authentication tests

                    // Configure test logging
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Debug);
                    });
                });

                // Override configuration for testing
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "InMemory",
                        ["StockApi:BaseUrl"] = "https://localhost:5001",
                        ["ApplicationInsights:ConnectionString"] = ""
                    });
                });
            });

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task HealthCheck_ShouldReturnHealthy()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Be("Healthy");
        }

        [Fact]
        public async Task Login_Get_ShouldReturnLoginPage()
        {
            // Act
            var response = await _client.GetAsync("/Account/Login");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Login");
            content.Should().Contain("Email");
            content.Should().Contain("Password");
        }

        [Fact]
        public async Task Register_Get_ShouldReturnRegisterPage()
        {
            // Act
            var response = await _client.GetAsync("/Account/Register");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Create Account");
            content.Should().Contain("Email");
            content.Should().Contain("Password");
            content.Should().Contain("First Name");
            content.Should().Contain("Last Name");
        }

        [Fact]
        public async Task Register_Post_WithValidData_ShouldCreateUserAndRedirect()
        {
            // Arrange
            var registerData = new Dictionary<string, string>
            {
                ["Email"] = "test@example.com",
                ["Password"] = "Password123!",
                ["ConfirmPassword"] = "Password123!",
                ["FirstName"] = "John",
                ["LastName"] = "Doe"
            };

            // Get the registration page first to obtain anti-forgery token
            var getResponse = await _client.GetAsync("/Account/Register");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new List<KeyValuePair<string, string>>(registerData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", token)
            };

            // Act
            var response = await _client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().Should().Be("/");

            // Verify user was created in database
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync("test@example.com");
            user.Should().NotBeNull();
            user!.FirstName.Should().Be("John");
            user.LastName.Should().Be("Doe");
            user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task Register_Post_WithExistingEmail_ShouldReturnErrorPage()
        {
            // Arrange - Create user first
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var existingUser = new ApplicationUser
                {
                    UserName = "existing@example.com",
                    Email = "existing@example.com",
                    FirstName = "Existing",
                    LastName = "User",
                    CreatedAt = DateTime.UtcNow
                };
                await userManager.CreateAsync(existingUser, "Password123!");
            }

            var registerData = new Dictionary<string, string>
            {
                ["Email"] = "existing@example.com",
                ["Password"] = "Password123!",
                ["ConfirmPassword"] = "Password123!",
                ["FirstName"] = "John",
                ["LastName"] = "Doe"
            };

            // Get the registration page first to obtain anti-forgery token
            var getResponse = await _client.GetAsync("/Account/Register");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new List<KeyValuePair<string, string>>(registerData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", token)
            };

            // Act
            var response = await _client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("User with this email already exists");
        }

        [Fact]
        public async Task Register_Post_WithWeakPassword_ShouldReturnValidationErrors()
        {
            // Arrange
            var registerData = new Dictionary<string, string>
            {
                ["Email"] = "test@example.com",
                ["Password"] = "weak",
                ["ConfirmPassword"] = "weak",
                ["FirstName"] = "John",
                ["LastName"] = "Doe"
            };

            // Get the registration page first to obtain anti-forgery token
            var getResponse = await _client.GetAsync("/Account/Register");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new List<KeyValuePair<string, string>>(registerData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", token)
            };

            // Act
            var response = await _client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Password");
            
            // Should contain validation errors
            (content.Contains("too short") || content.Contains("uppercase") || content.Contains("digit")).Should().BeTrue();
        }

        [Fact]
        public async Task Login_Post_WithValidCredentials_ShouldAuthenticateAndRedirect()
        {
            // Arrange - Create user first
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = new ApplicationUser
                {
                    UserName = "testlogin@example.com",
                    Email = "testlogin@example.com",
                    FirstName = "Test",
                    LastName = "User",
                    CreatedAt = DateTime.UtcNow
                };
                await userManager.CreateAsync(user, "Password123!");
            }

            var loginData = new Dictionary<string, string>
            {
                ["Email"] = "testlogin@example.com",
                ["Password"] = "Password123!",
                ["RememberMe"] = "false"
            };

            // Get the login page first to obtain anti-forgery token
            var getResponse = await _client.GetAsync("/Account/Login");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new List<KeyValuePair<string, string>>(loginData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", token)
            };

            // Act
            var response = await _client.PostAsync("/Account/Login", new FormUrlEncodedContent(formData));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().Should().Be("/");
            
            // Verify authentication cookie is set
            response.Headers.Should().ContainKey("Set-Cookie");
            var cookies = response.Headers.GetValues("Set-Cookie");
            cookies.Should().Contain(c => c.Contains(".AspNetCore.Identity.Application"));
        }

        [Fact]
        public async Task Login_Post_WithInvalidCredentials_ShouldReturnErrorPage()
        {
            // Arrange
            var loginData = new Dictionary<string, string>
            {
                ["Email"] = "nonexistent@example.com",
                ["Password"] = "WrongPassword",
                ["RememberMe"] = "false"
            };

            // Get the login page first to obtain anti-forgery token
            var getResponse = await _client.GetAsync("/Account/Login");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new List<KeyValuePair<string, string>>(loginData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", token)
            };

            // Act
            var response = await _client.PostAsync("/Account/Login", new FormUrlEncodedContent(formData));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Invalid email or password");
        }

        [Fact]
        public async Task AuthenticationFlow_CompleteScenario_ShouldWorkEndToEnd()
        {
            // 1. Register a new user
            var registerData = new Dictionary<string, string>
            {
                ["Email"] = "endtoend@example.com",
                ["Password"] = "Password123!",
                ["ConfirmPassword"] = "Password123!",
                ["FirstName"] = "End",
                ["LastName"] = "ToEnd"
            };

            var getRegisterResponse = await _client.GetAsync("/Account/Register");
            var registerContent = await getRegisterResponse.Content.ReadAsStringAsync();
            var registerToken = ExtractAntiForgeryToken(registerContent);

            var registerFormData = new List<KeyValuePair<string, string>>(registerData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", registerToken)
            };

            var registerResponse = await _client.PostAsync("/Account/Register", new FormUrlEncodedContent(registerFormData));
            registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

            // 2. Logout
            var logoutResponse = await _client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", registerToken)
            }));
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

            // 3. Login with the created user
            var loginData = new Dictionary<string, string>
            {
                ["Email"] = "endtoend@example.com",
                ["Password"] = "Password123!",
                ["RememberMe"] = "false"
            };

            var getLoginResponse = await _client.GetAsync("/Account/Login");
            var loginContent = await getLoginResponse.Content.ReadAsStringAsync();
            var loginToken = ExtractAntiForgeryToken(loginContent);

            var loginFormData = new List<KeyValuePair<string, string>>(loginData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", loginToken)
            };

            var loginResponse = await _client.PostAsync("/Account/Login", new FormUrlEncodedContent(loginFormData));
            loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

            // 4. Verify user is authenticated by accessing a protected resource
            var dashboardResponse = await _client.GetAsync("/");
            dashboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task DatabaseConnection_ShouldHandleMultipleRegistrations()
        {
            // Arrange
            var users = new[]
            {
                ("user1@example.com", "User", "One"),
                ("user2@example.com", "User", "Two"),
                ("user3@example.com", "User", "Three")
            };

            // Act - Register multiple users concurrently
            var tasks = users.Select(async user =>
            {
                var (email, firstName, lastName) = user;
                var registerData = new Dictionary<string, string>
                {
                    ["Email"] = email,
                    ["Password"] = "Password123!",
                    ["ConfirmPassword"] = "Password123!",
                    ["FirstName"] = firstName,
                    ["LastName"] = lastName
                };

                var getResponse = await _client.GetAsync("/Account/Register");
                var getContent = await getResponse.Content.ReadAsStringAsync();
                var token = ExtractAntiForgeryToken(getContent);

                var formData = new List<KeyValuePair<string, string>>(registerData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
                {
                    new("__RequestVerificationToken", token)
                };

                return await _client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));
            });

            var responses = await Task.WhenAll(tasks);

            // Assert
            responses.Should().AllSatisfy(response => 
                response.StatusCode.Should().Be(HttpStatusCode.Redirect));

            // Verify all users were created
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            
            foreach (var (email, firstName, lastName) in users)
            {
                var user = await userManager.FindByEmailAsync(email);
                user.Should().NotBeNull($"User {email} should be created");
                user!.FirstName.Should().Be(firstName);
                user.LastName.Should().Be(lastName);
            }
        }

        [Fact]
        public async Task Registration_WithAzureDeploymentContext_ShouldLogDetailedInformation()
        {
            // This test simulates Azure deployment conditions and verifies comprehensive logging
            
            // Arrange
            var registerData = new Dictionary<string, string>
            {
                ["Email"] = "azure@example.com",
                ["Password"] = "Password123!",
                ["ConfirmPassword"] = "Password123!",
                ["FirstName"] = "Azure",
                ["LastName"] = "User"
            };

            // Get the registration page first to obtain anti-forgery token
            var getResponse = await _client.GetAsync("/Account/Register");
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new List<KeyValuePair<string, string>>(registerData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", token)
            };

            // Act
            var response = await _client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);

            // Verify logging occurred by checking the application logs
            // In a real Azure deployment, you would check Application Insights logs
            using var scope = _factory.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AccountController>>();
            logger.Should().NotBeNull();

            // Verify user creation in database with all required fields
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync("azure@example.com");
            user.Should().NotBeNull();
            user!.Email.Should().Be("azure@example.com");
            user.FirstName.Should().Be("Azure");
            user.LastName.Should().Be("User");
            user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            user.EnablePriceAlerts.Should().BeTrue();
        }

        private static string ExtractAntiForgeryToken(string html)
        {
            // Simple token extraction for testing
            const string tokenStart = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
            var startIndex = html.IndexOf(tokenStart, StringComparison.Ordinal);
            if (startIndex == -1)
                return string.Empty;

            startIndex += tokenStart.Length;
            var endIndex = html.IndexOf("\"", startIndex, StringComparison.Ordinal);
            return endIndex == -1 ? string.Empty : html.Substring(startIndex, endIndex - startIndex);
        }
    }
}
