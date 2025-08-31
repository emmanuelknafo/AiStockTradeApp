using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using AiStockTradeApp.DataAccess.Data;
using AiStockTradeApp.DataAccess;
using AiStockTradeApp.Entities.Models;
using Microsoft.AspNetCore.Identity;
using System.Net;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace AiStockTradeApp.Tests.Integration
{
    public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _baseFactory;

        public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _baseFactory = factory;
        }

        private (HttpClient client, WebApplicationFactory<Program> factory) CreateTestClient(string testName)
        {
            var testIdentityDbName = $"TestIdentityDb_{testName}";
            var testStockDataDbName = $"TestStockDataDb_{testName}";
            
            var factory = _baseFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext registrations
                    var identityDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationIdentityContext>));
                    if (identityDescriptor != null)
                        services.Remove(identityDescriptor);

                    var stockDataDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<StockDataContext>));
                    if (stockDataDescriptor != null)
                        services.Remove(stockDataDescriptor);

                    // Add InMemory database for testing with test-specific naming
                    services.AddDbContext<ApplicationIdentityContext>(options =>
                    {
                        options.UseInMemoryDatabase(testIdentityDbName);
                    });

                    // Add InMemory StockDataContext for testing (required by app startup)
                    services.AddDbContext<StockDataContext>(options =>
                    {
                        options.UseInMemoryDatabase(testStockDataDbName);
                    });

                    // Configure test logging
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                        logging.SetMinimumLevel(LogLevel.Warning); // Reduce log noise
                    });
                });

                // Override configuration for testing
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "InMemory",
                        ["USE_INMEMORY_DB"] = "true",
                        ["StockApi:BaseUrl"] = "https://localhost:5001",
                        ["ApplicationInsights:ConnectionString"] = "",
                        ["AlphaVantage:ApiKey"] = "",
                        ["TwelveData:ApiKey"] = ""
                    });
                });
            });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            return (client, factory);
        }

        [Fact]
        public async Task Register_Post_WithValidData_ShouldCreateUserAndRedirect()
        {
            // Arrange
            var (client, factory) = CreateTestClient(nameof(Register_Post_WithValidData_ShouldCreateUserAndRedirect));
            var registerData = new Dictionary<string, string>
            {
                ["Email"] = "test@example.com",
                ["Password"] = "Password123!",
                ["ConfirmPassword"] = "Password123!",
                ["FirstName"] = "John",
                ["LastName"] = "Doe"
            };

            // Get the registration page first to obtain anti-forgery token
            var getResponse = await client.GetAsync("/Account/Register");
            getResponse.EnsureSuccessStatusCode();
            var getContent = await getResponse.Content.ReadAsStringAsync();
            
            // Extract anti-forgery token
            var token = ExtractAntiForgeryToken(getContent);

            if (string.IsNullOrEmpty(token))
            {
                // Log part of the HTML for debugging
                var htmlSnippet = getContent.Length > 1000 ? getContent.Substring(0, 1000) : getContent;
                Assert.True(false, $"Could not extract anti-forgery token from registration page. HTML snippet: {htmlSnippet}");
            }

            var formData = new List<KeyValuePair<string, string>>(registerData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", token)
            };

            // Act
            var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));

            // Debug information
            var responseContent = await response.Content.ReadAsStringAsync();

            // Check for model validation errors first
            if (response.StatusCode == HttpStatusCode.OK && responseContent.Contains("field-validation-error"))
            {
                // Extract validation errors
                var errorPattern = @"<span class=""field-validation-error""[^>]*>([^<]+)</span>";
                var errorMatches = Regex.Matches(responseContent, errorPattern);
                var errors = string.Join(", ", errorMatches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value));
                Assert.True(false, $"Registration failed with validation errors: {errors}");
            }

            // Assert - Check if we got a successful response (either redirect or successful OK)
            var isSuccess = response.StatusCode == HttpStatusCode.Redirect || 
                           response.StatusCode == HttpStatusCode.SeeOther ||
                           response.StatusCode == HttpStatusCode.Found;
            
            if (!isSuccess)
            {
                // For debugging, show a meaningful portion of the response
                var debugContent = responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent;
                Assert.True(false, $"Expected successful registration but got {response.StatusCode}. Response contains: {debugContent}");
            }

            // Verify user was created in database using the same factory
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync("test@example.com");
            user.Should().NotBeNull();
            user!.FirstName.Should().Be("John");
            user.LastName.Should().Be("Doe");
            user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        private static string ExtractAntiForgeryToken(string html)
        {
            // Simple pattern to find the anti-forgery token
            var pattern = @"name=""__RequestVerificationToken""\s+value=""([^""]+)""";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Try alternative pattern
            pattern = @"value=""([^""]+)""\s+name=""__RequestVerificationToken""";
            match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return string.Empty;
        }
    }
}
