using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AiStockTradeApp.DataAccess.Data;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.Services;
using AiStockTradeApp.Services.Implementations;

namespace AiStockTradeApp.Tests.Integration
{
    /// <summary>
    /// Integration tests that combine logging and user seeding functionality
    /// These tests use in-memory database and verify end-to-end behavior
    /// </summary>
    public class LoggingAndUserSeedingIntegrationTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ApplicationIdentityContext _context;
        private readonly TestUserSeedingService _seedingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly TestLoggerProvider _loggerProvider;

        public LoggingAndUserSeedingIntegrationTests()
        {
            var services = new ServiceCollection();
            
            // Add in-memory database
            services.AddDbContext<ApplicationIdentityContext>(options =>
                options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

            // Add Identity services
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
            })
            .AddEntityFrameworkStores<ApplicationIdentityContext>()
            .AddDefaultTokenProviders();

            // Add custom logger provider for testing
            _loggerProvider = new TestLoggerProvider();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddProvider(_loggerProvider);
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Add test user seeding service
            services.AddScoped<ITestUserSeedingService, TestUserSeedingService>();

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<ApplicationIdentityContext>();
            _userManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            _roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            _seedingService = _serviceProvider.GetRequiredService<ITestUserSeedingService>() as TestUserSeedingService
                ?? throw new InvalidOperationException("Failed to get TestUserSeedingService");

            // Ensure database is created
            _context.Database.EnsureCreated();
        }

        [Fact]
        public async Task SeedTestUser_NewUser_ShouldLogCorrectOperationFlow()
        {
            // Arrange
            var profile = TestUserProfiles.StandardUser;
            _loggerProvider.Clear();

            // Act
            var user = await _seedingService.SeedTestUserIfNotExistsAsync(profile);

            // Assert
            user.Should().NotBeNull();
            user.Email.Should().Be(profile.Email);

            // Verify logging occurred
            var logs = _loggerProvider.GetLogs();
            logs.Should().Contain(l => l.Message.Contains("Starting operation: SeedTestUserIfNotExists"));
            logs.Should().Contain(l => l.Message.Contains("Created role User"));
            logs.Should().Contain(l => l.Message.Contains($"Successfully created test user {profile.Email}"));
            logs.Should().Contain(l => l.Message.Contains("Business event: TestUserCreated"));
            logs.Should().Contain(l => l.Message.Contains("Operation completed: SeedTestUserIfNotExists"));
        }

        [Fact]
        public async Task SeedTestUser_ExistingUser_ShouldLogUserAlreadyExists()
        {
            // Arrange
            var profile = TestUserProfiles.AdminUser;
            
            // Create user first
            await _seedingService.SeedTestUserIfNotExistsAsync(profile);
            _loggerProvider.Clear();

            // Act - Try to seed again
            var user = await _seedingService.SeedTestUserIfNotExistsAsync(profile);

            // Assert
            user.Should().NotBeNull();
            user.Email.Should().Be(profile.Email);

            // Verify logging shows user already exists
            var logs = _loggerProvider.GetLogs();
            logs.Should().Contain(l => l.Message.Contains($"Test user {profile.Email} already exists"));
            logs.Should().NotContain(l => l.Message.Contains("Successfully created test user"));
        }

        [Fact]
        public async Task SeedMultipleTestUsers_ShouldLogBatchOperation()
        {
            // Arrange
            var profiles = new[] { TestUserProfiles.FrenchUser, TestUserProfiles.PremiumUser };
            _loggerProvider.Clear();

            // Act
            var users = await _seedingService.SeedTestUsersIfNotExistAsync(profiles);

            // Assert
            users.Should().HaveCount(2);

            // Verify batch logging
            var logs = _loggerProvider.GetLogs();
            logs.Should().Contain(l => l.Message.Contains("Starting operation: SeedTestUsersIfNotExist") && l.Message.Contains("UserCount"));
            logs.Should().Contain(l => l.Message.Contains("Seeded 2 out of 2 test users"));
            
            // Verify individual user creation logs
            logs.Should().Contain(l => l.Message.Contains($"Successfully created test user {TestUserProfiles.FrenchUser.Email}"));
            logs.Should().Contain(l => l.Message.Contains($"Successfully created test user {TestUserProfiles.PremiumUser.Email}"));
        }

        [Fact]
        public async Task GetOrCreateTestUser_NewUser_ShouldLogCreationWithDefaults()
        {
            // Arrange
            var email = "dynamic@example.com";
            var role = "TestRole";
            _loggerProvider.Clear();

            // Act
            var user = await _seedingService.GetOrCreateTestUserAsync(email, role);

            // Assert
            user.Should().NotBeNull();
            user!.Email.Should().Be(email);
            user.FirstName.Should().Be("Test");
            user.LastName.Should().Be("User");

            // Verify logging
            var logs = _loggerProvider.GetLogs();
            logs.Should().Contain(l => l.Message.Contains("Starting operation: GetOrCreateTestUser"));
            logs.Should().Contain(l => l.Message.Contains($"Created role {role}"));
            logs.Should().Contain(l => l.Message.Contains($"Successfully created test user {email}"));
        }

        [Fact]
        public async Task ValidateTestUser_ValidUser_ShouldLogValidationSuccess()
        {
            // Arrange
            var profile = TestUserProfiles.StandardUser;
            await _seedingService.SeedTestUserIfNotExistsAsync(profile);
            _loggerProvider.Clear();

            // Act
            var isValid = await _seedingService.ValidateTestUserAsync(profile.Email, profile);

            // Assert
            isValid.Should().BeTrue();

            // Verify validation logging
            var logs = _loggerProvider.GetLogs();
            logs.Should().Contain(l => l.Message.Contains("Starting operation: ValidateTestUser"));
            logs.Should().Contain(l => l.Message.Contains("Operation completed: ValidateTestUser"));
            logs.Should().NotContain(l => l.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task ValidateTestUser_InvalidUser_ShouldLogValidationFailure()
        {
            // Arrange
            var profile = TestUserProfiles.StandardUser;
            var wrongProfile = new TestUserProfile
            {
                Email = profile.Email,
                FirstName = "Wrong Name",
                LastName = profile.LastName,
                Role = profile.Role,
                PreferredCulture = profile.PreferredCulture,
                EnablePriceAlerts = profile.EnablePriceAlerts
            };

            await _seedingService.SeedTestUserIfNotExistsAsync(profile);
            _loggerProvider.Clear();

            // Act
            var isValid = await _seedingService.ValidateTestUserAsync(profile.Email, wrongProfile);

            // Assert
            isValid.Should().BeFalse();

            // Verify warning is logged
            var logs = _loggerProvider.GetLogs();
            logs.Should().Contain(l => l.Level == LogLevel.Warning && 
                l.Message.Contains($"Test user {profile.Email} exists but doesn't match expected profile"));
        }

        [Fact]
        public async Task CleanupTestUsers_ExistingUsers_ShouldLogDeletions()
        {
            // Arrange
            var profiles = new[] { TestUserProfiles.StandardUser, TestUserProfiles.AdminUser };
            await _seedingService.SeedTestUsersIfNotExistAsync(profiles);
            
            var emails = profiles.Select(p => p.Email);
            _loggerProvider.Clear();

            // Act
            await _seedingService.CleanupTestUsersAsync(emails);

            // Assert
            // Verify cleanup logging
            var logs = _loggerProvider.GetLogs();
            logs.Should().Contain(l => l.Message.Contains("Starting operation: CleanupTestUsers"));
            logs.Should().Contain(l => l.Message.Contains($"Deleted test user {TestUserProfiles.StandardUser.Email}"));
            logs.Should().Contain(l => l.Message.Contains($"Deleted test user {TestUserProfiles.AdminUser.Email}"));
            logs.Should().Contain(l => l.Message.Contains("Cleaned up 2 out of 2 test users"));
        }

        [Fact]
        public async Task SeedingService_WithDisabledLogging_ShouldNotImpactPerformance()
        {
            // Arrange
            _loggerProvider.SetMinimumLevel(LogLevel.None); // Disable all logging
            var profile = TestUserProfiles.PremiumUser;

            // Act
            var startTime = DateTime.UtcNow;
            var user = await _seedingService.SeedTestUserIfNotExistsAsync(profile);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            user.Should().NotBeNull();
            duration.Should().BeLessThan(TimeSpan.FromSeconds(5)); // Should complete quickly

            // Verify no logs were created
            var logs = _loggerProvider.GetLogs();
            logs.Should().BeEmpty();
        }

        [Fact]
        public async Task SeedingService_StructuredLogging_ShouldContainCorrectProperties()
        {
            // Arrange
            var profile = TestUserProfiles.FrenchUser;
            _loggerProvider.Clear();

            // Act
            await _seedingService.SeedTestUserIfNotExistsAsync(profile);

            // Assert
            var businessEventLog = _loggerProvider.GetLogs()
                .FirstOrDefault(l => l.Message.Contains("Business event: TestUserCreated"));

            businessEventLog.Should().NotBeNull();
            businessEventLog!.Properties.Should().ContainKey("UserId");
            businessEventLog.Properties.Should().ContainKey("Email");
            businessEventLog.Properties.Should().ContainKey("Role");
            businessEventLog.Properties.Should().ContainKey("Culture");
            
            businessEventLog.Properties["Email"].Should().Be(profile.Email);
            businessEventLog.Properties["Role"].Should().Be(profile.Role);
            businessEventLog.Properties["Culture"].Should().Be(profile.PreferredCulture);
        }

        public void Dispose()
        {
            _context?.Dispose();
            _serviceProvider?.Dispose();
        }
    }

    /// <summary>
    /// Test logger provider for capturing logs during integration tests
    /// </summary>
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<LogEntry> _logs = new();
        private LogLevel _minimumLevel = LogLevel.Debug;

        public void Clear() => _logs.Clear();
        public IReadOnlyList<LogEntry> GetLogs() => _logs.AsReadOnly();
        public void SetMinimumLevel(LogLevel level) => _minimumLevel = level;

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(categoryName, _logs, () => _minimumLevel);
        }

        public void Dispose()
        {
            _logs.Clear();
        }
    }

    /// <summary>
    /// Test logger for capturing log entries
    /// </summary>
    public class TestLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly List<LogEntry> _logs;
        private readonly Func<LogLevel> _getMinimumLevel;

        public TestLogger(string categoryName, List<LogEntry> logs, Func<LogLevel> getMinimumLevel)
        {
            _categoryName = categoryName;
            _logs = logs;
            _getMinimumLevel = getMinimumLevel;
        }

        public IDisposable BeginScope<TState>(TState state) => NullDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _getMinimumLevel();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var properties = new Dictionary<string, object?>();

            // Extract structured logging properties if state is a collection
            if (state is IEnumerable<KeyValuePair<string, object?>> keyValuePairs)
            {
                foreach (var kvp in keyValuePairs)
                {
                    if (kvp.Key != "{OriginalFormat}")
                    {
                        properties[kvp.Key] = kvp.Value;
                    }
                }
            }

            _logs.Add(new LogEntry
            {
                CategoryName = _categoryName,
                Level = logLevel,
                EventId = eventId,
                Message = message,
                Exception = exception,
                Timestamp = DateTime.UtcNow,
                Properties = properties
            });
        }

        private class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Represents a captured log entry
    /// </summary>
    public class LogEntry
    {
        public string CategoryName { get; set; } = string.Empty;
        public LogLevel Level { get; set; }
        public EventId EventId { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object?> Properties { get; set; } = new();
    }
}
