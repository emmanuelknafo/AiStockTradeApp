using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AiStockTradeApp.DataAccess.Data;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.Services;
using AiStockTradeApp.Services.Implementations;

namespace AiStockTradeApp.Tests
{
    /// <summary>
    /// Helper class for setting up test environments with user seeding and logging
    /// </summary>
    public class TestSetupHelper : IDisposable
    {
        private ServiceProvider? _serviceProvider;
        private ApplicationIdentityContext? _context;
        private readonly List<string> _createdUserEmails = new();

        public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Services not initialized");
        public ApplicationIdentityContext Context => _context ?? throw new InvalidOperationException("Context not initialized");
        public ITestUserSeedingService SeedingService { get; private set; } = null!;
        public UserManager<ApplicationUser> UserManager { get; private set; } = null!;
        public RoleManager<IdentityRole> RoleManager { get; private set; } = null!;

        /// <summary>
        /// Initializes test environment with in-memory database and Identity services
        /// </summary>
        public async Task<TestSetupHelper> InitializeAsync(Action<IServiceCollection>? configureServices = null)
        {
            var services = new ServiceCollection();
            
            // Add in-memory database with unique name
            var databaseName = $"TestDb_{Guid.NewGuid()}";
            services.AddDbContext<ApplicationIdentityContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            // Add Identity services with test-friendly configuration
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Relaxed password requirements for testing
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationIdentityContext>()
            .AddDefaultTokenProviders();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Add test user seeding service
            services.AddScoped<ITestUserSeedingService, TestUserSeedingService>();

            // Allow additional service configuration
            configureServices?.Invoke(services);

            _serviceProvider = services.BuildServiceProvider();
            _context = _serviceProvider.GetRequiredService<ApplicationIdentityContext>();
            
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Get required services
            UserManager = _serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            SeedingService = _serviceProvider.GetRequiredService<ITestUserSeedingService>();

            return this;
        }

        /// <summary>
        /// Seeds a single test user and tracks it for cleanup
        /// </summary>
        public async Task<ApplicationUser> SeedUserAsync(TestUserProfile? profile = null)
        {
            profile ??= TestUserProfiles.StandardUser;
            var user = await SeedingService.SeedTestUserIfNotExistsAsync(profile);
            _createdUserEmails.Add(profile.Email);
            return user;
        }

        /// <summary>
        /// Seeds multiple test users and tracks them for cleanup
        /// </summary>
        public async Task<IEnumerable<ApplicationUser>> SeedUsersAsync(params TestUserProfile[] profiles)
        {
            if (profiles.Length == 0)
            {
                profiles = TestUserProfiles.AllProfiles.ToArray();
            }

            var users = await SeedingService.SeedTestUsersIfNotExistAsync(profiles);
            _createdUserEmails.AddRange(profiles.Select(p => p.Email));
            return users;
        }

        /// <summary>
        /// Seeds a user with a specific role, creating the role if needed
        /// </summary>
        public async Task<ApplicationUser> SeedUserWithRoleAsync(string email, string role, string? firstName = null, string? lastName = null)
        {
            var profile = new TestUserProfile
            {
                Email = email,
                Role = role,
                FirstName = firstName ?? "Test",
                LastName = lastName ?? "User",
                Password = "TestPassword123!"
            };

            var user = await SeedingService.SeedTestUserIfNotExistsAsync(profile);
            _createdUserEmails.Add(email);
            return user;
        }

        /// <summary>
        /// Seeds common test roles
        /// </summary>
        public async Task SeedCommonRolesAsync()
        {
            var commonRoles = new[] { "Administrator", "User", "Premium", "Moderator" };
            
            foreach (var roleName in commonRoles)
            {
                if (!await RoleManager.RoleExistsAsync(roleName))
                {
                    await RoleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }

        /// <summary>
        /// Creates a test user profile with randomized email to avoid conflicts
        /// </summary>
        public static TestUserProfile CreateRandomUserProfile(string? role = null)
        {
            var timestamp = DateTime.UtcNow.Ticks;
            return new TestUserProfile
            {
                Email = $"testuser{timestamp}@example.com",
                FirstName = "Test",
                LastName = $"User{timestamp % 1000}",
                Role = role ?? "User",
                Password = "TestPassword123!",
                PreferredCulture = "en",
                EnablePriceAlerts = true
            };
        }

        /// <summary>
        /// Validates that a user exists and matches expected properties
        /// </summary>
        public async Task<bool> ValidateUserExistsAsync(string email, string? expectedRole = null)
        {
            var user = await UserManager.FindByEmailAsync(email);
            if (user == null) return false;

            if (expectedRole != null)
            {
                return await UserManager.IsInRoleAsync(user, expectedRole);
            }

            return true;
        }

        /// <summary>
        /// Gets all users in a specific role
        /// </summary>
        public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName)
        {
            return await UserManager.GetUsersInRoleAsync(roleName);
        }

        /// <summary>
        /// Cleans up a specific user by email
        /// </summary>
        public async Task CleanupUserAsync(string email)
        {
            var user = await UserManager.FindByEmailAsync(email);
            if (user != null)
            {
                await UserManager.DeleteAsync(user);
                _createdUserEmails.Remove(email);
            }
        }

        /// <summary>
        /// Disposes of resources and cleans up created test users
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Cleanup tracked users
                if (_createdUserEmails.Any() && SeedingService != null)
                {
                    var cleanupTask = SeedingService.CleanupTestUsersAsync(_createdUserEmails);
                    cleanupTask.Wait(TimeSpan.FromSeconds(30)); // Wait max 30 seconds for cleanup
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors during disposal
            }

            _context?.Dispose();
            _serviceProvider?.Dispose();
        }
    }

    /// <summary>
    /// Base class for tests that need user seeding functionality
    /// </summary>
    public abstract class BaseUserSeedingTest : IAsyncDisposable
    {
        protected TestSetupHelper TestHelper { get; private set; } = new();
        protected ITestUserSeedingService SeedingService => TestHelper.SeedingService;
        protected UserManager<ApplicationUser> UserManager => TestHelper.UserManager;
        protected RoleManager<IdentityRole> RoleManager => TestHelper.RoleManager;

        /// <summary>
        /// Initialize test setup - call this from test constructor or setup method
        /// </summary>
        protected async Task InitializeTestAsync(Action<IServiceCollection>? configureServices = null)
        {
            await TestHelper.InitializeAsync(configureServices);
        }

        /// <summary>
        /// Seeds a test user and returns it
        /// </summary>
        protected async Task<ApplicationUser> SeedTestUserAsync(TestUserProfile? profile = null)
        {
            return await TestHelper.SeedUserAsync(profile);
        }

        /// <summary>
        /// Seeds multiple test users
        /// </summary>
        protected async Task<IEnumerable<ApplicationUser>> SeedTestUsersAsync(params TestUserProfile[] profiles)
        {
            return await TestHelper.SeedUsersAsync(profiles);
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            TestHelper?.Dispose();
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Attribute to mark tests that require user seeding
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequiresUserSeedingAttribute : Attribute
    {
        public string[] RequiredRoles { get; }

        public RequiresUserSeedingAttribute(params string[] requiredRoles)
        {
            RequiredRoles = requiredRoles ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Test data generator for user-related tests
    /// </summary>
    public static class TestUserDataGenerator
    {
        private static readonly Random Random = new();

        public static IEnumerable<object[]> GetValidUserProfiles()
        {
            yield return new object[] { TestUserProfiles.StandardUser };
            yield return new object[] { TestUserProfiles.AdminUser };
            yield return new object[] { TestUserProfiles.FrenchUser };
            yield return new object[] { TestUserProfiles.PremiumUser };
        }

        public static IEnumerable<object[]> GetUserRoleCombinations()
        {
            var roles = new[] { "User", "Administrator", "Premium", "Moderator" };
            foreach (var role in roles)
            {
                yield return new object[] { $"test{role.ToLower()}@example.com", role };
            }
        }

        public static IEnumerable<object[]> GetCultureTestData()
        {
            yield return new object[] { "en", "English User", true };
            yield return new object[] { "fr", "French User", false };
            yield return new object[] { "es", "Spanish User", true };
            yield return new object[] { "de", "German User", false };
        }

        public static TestUserProfile GenerateRandomProfile()
        {
            var cultures = new[] { "en", "fr", "es", "de" };
            var roles = new[] { "User", "Premium", "Moderator" };
            var id = Random.Next(1000, 9999);

            return new TestUserProfile
            {
                Email = $"random{id}@example.com",
                FirstName = $"First{id}",
                LastName = $"Last{id}",
                Role = roles[Random.Next(roles.Length)],
                PreferredCulture = cultures[Random.Next(cultures.Length)],
                EnablePriceAlerts = Random.Next(2) == 0,
                Password = "RandomPassword123!"
            };
        }
    }
}
