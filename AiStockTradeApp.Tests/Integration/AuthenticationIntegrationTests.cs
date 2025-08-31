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
            var getContent = await getResponse.Content.ReadAsStringAsync();
            var token = ExtractAntiForgeryToken(getContent);

            var formData = new List<KeyValuePair<string, string>>(registerData.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)))
            {
                new("__RequestVerificationToken", token)
            };

            // Act
            var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));

            // Assert - Accept both Redirect and SeeOther status codes as valid success responses
            var isSuccessRedirect = response.StatusCode == HttpStatusCode.Redirect || 
                                   response.StatusCode == HttpStatusCode.SeeOther ||
                                   response.StatusCode == HttpStatusCode.Found;
            
            isSuccessRedirect.Should().BeTrue($"Expected redirect status code but got {response.StatusCode}");
            
            // The location should point to home page or a success page
            var location = response.Headers.Location?.ToString();
            (location == "/" || location?.Contains("login") == true || string.IsNullOrEmpty(location)).Should().BeTrue();

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
            const string tokenName = "__RequestVerificationToken";
            var startIndex = html.IndexOf($"name=\"{tokenName}\"");
            if (startIndex == -1) return string.Empty;

            var valueIndex = html.IndexOf("value=\"", startIndex);
            if (valueIndex == -1) return string.Empty;

            valueIndex += 7; // Length of "value=\""
            var endIndex = html.IndexOf("\"", valueIndex);
            if (endIndex == -1) return string.Empty;

            return html.Substring(valueIndex, endIndex - valueIndex);
        }
    }
}
