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
            var token = ExtractAntiForgeryToken(getContent);

            if (string.IsNullOrEmpty(token))
            {
                Assert.True(false, "Could not extract anti-forgery token from registration page");
            }

            var formData = new List<KeyValuePair<string, string>>(registerData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", token)
            };

            // Act
            var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));

            // Debug information
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert - Check if we got a successful response (either redirect or OK with success)
            var isSuccess = response.StatusCode == HttpStatusCode.Redirect || 
                           response.StatusCode == HttpStatusCode.SeeOther ||
                           response.StatusCode == HttpStatusCode.Found ||
                           (response.StatusCode == HttpStatusCode.OK && !responseContent.Contains("error") && !responseContent.Contains("Invalid"));
            
            isSuccess.Should().BeTrue($"Expected successful registration but got {response.StatusCode}. Content: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}");

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
            // Try multiple patterns to find the anti-forgery token
            var patterns = new[]
            {
                @"name=""__RequestVerificationToken""\s+value=""([^""]+)""",
                @"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""[^>]*>",
                @"<input[^>]*value=""([^""]+)""[^>]*name=""__RequestVerificationToken""[^>]*>"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return string.Empty;
        }
    }
}
