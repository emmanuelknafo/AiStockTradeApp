using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.Services;
using AiStockTradeApp.Services.Interfaces;

namespace AiStockTradeApp.Services.Implementations
{
    /// <summary>
    /// Service for seeding test users and managing user data for testing scenarios
    /// </summary>
    public class TestUserSeedingService : ITestUserSeedingService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<TestUserSeedingService> _logger;

        public TestUserSeedingService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<TestUserSeedingService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        /// <summary>
        /// Seeds a test user if it doesn't exist
        /// </summary>
        public async Task<ApplicationUser> SeedTestUserIfNotExistsAsync(TestUserProfile profile)
        {
            using var operationTimer = _logger.LogOperationStart("SeedTestUserIfNotExists", new { Email = profile.Email, Role = profile.Role });

            try
            {
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(profile.Email);
                if (existingUser != null)
                {
                    _logger.LogInformation("Test user {Email} already exists with ID {UserId}", profile.Email, existingUser.Id);
                    return existingUser;
                }

                // Ensure role exists
                await EnsureRoleExistsAsync(profile.Role);

                // Create new test user
                var testUser = new ApplicationUser
                {
                    UserName = profile.Email,
                    Email = profile.Email,
                    EmailConfirmed = true,
                    FirstName = profile.FirstName,
                    LastName = profile.LastName,
                    PreferredCulture = profile.PreferredCulture,
                    EnablePriceAlerts = profile.EnablePriceAlerts,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(testUser, profile.Password);
                
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create test user {Email}: {Errors}", profile.Email, errors);
                    throw new InvalidOperationException($"Failed to create test user: {errors}");
                }

                // Add user to role
                await _userManager.AddToRoleAsync(testUser, profile.Role);

                _logger.LogInformation("Successfully created test user {Email} with ID {UserId} in role {Role}", 
                    profile.Email, testUser.Id, profile.Role);

                _logger.LogBusinessEvent("TestUserCreated", new 
                { 
                    UserId = testUser.Id, 
                    Email = profile.Email, 
                    Role = profile.Role,
                    Culture = profile.PreferredCulture
                });

                return testUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding test user {Email}", profile.Email);
                throw;
            }
        }

        /// <summary>
        /// Seeds multiple test users if they don't exist
        /// </summary>
        public async Task<IEnumerable<ApplicationUser>> SeedTestUsersIfNotExistAsync(IEnumerable<TestUserProfile> profiles)
        {
            using var operationTimer = _logger.LogOperationStart("SeedTestUsersIfNotExist", new { UserCount = profiles.Count() });

            var users = new List<ApplicationUser>();

            foreach (var profile in profiles)
            {
                try
                {
                    var user = await SeedTestUserIfNotExistsAsync(profile);
                    users.Add(user);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to seed test user {Email}, continuing with others", profile.Email);
                    // Continue with other users rather than failing completely
                }
            }

            _logger.LogInformation("Seeded {SuccessCount} out of {TotalCount} test users", users.Count, profiles.Count());
            return users;
        }

        /// <summary>
        /// Gets a test user by email, creating it if it doesn't exist
        /// </summary>
        public async Task<ApplicationUser?> GetOrCreateTestUserAsync(string email, string? role = "User")
        {
            using var operationTimer = _logger.LogOperationStart("GetOrCreateTestUser", new { Email = email, Role = role });

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                return existingUser;
            }

            var defaultProfile = new TestUserProfile
            {
                Email = email,
                Password = "TestPassword123!",
                FirstName = "Test",
                LastName = "User",
                Role = role ?? "User",
                PreferredCulture = "en",
                EnablePriceAlerts = true
            };

            return await SeedTestUserIfNotExistsAsync(defaultProfile);
        }

        /// <summary>
        /// Cleans up test users (for test teardown)
        /// </summary>
        public async Task CleanupTestUsersAsync(IEnumerable<string> emails)
        {
            using var operationTimer = _logger.LogOperationStart("CleanupTestUsers", new { EmailCount = emails.Count() });

            var deletedCount = 0;

            foreach (var email in emails)
            {
                try
                {
                    var user = await _userManager.FindByEmailAsync(email);
                    if (user != null)
                    {
                        var result = await _userManager.DeleteAsync(user);
                        if (result.Succeeded)
                        {
                            deletedCount++;
                            _logger.LogInformation("Deleted test user {Email}", email);
                        }
                        else
                        {
                            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                            _logger.LogWarning("Failed to delete test user {Email}: {Errors}", email, errors);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting test user {Email}", email);
                }
            }

            _logger.LogInformation("Cleaned up {DeletedCount} out of {TotalCount} test users", deletedCount, emails.Count());
        }

        /// <summary>
        /// Validates that a test user exists and has expected properties
        /// </summary>
        public async Task<bool> ValidateTestUserAsync(string email, TestUserProfile expectedProfile)
        {
            using var operationTimer = _logger.LogOperationStart("ValidateTestUser", new { Email = email });

            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning("Test user {Email} does not exist", email);
                    return false;
                }

                var isInRole = await _userManager.IsInRoleAsync(user, expectedProfile.Role);
                
                var isValid = user.Email == expectedProfile.Email &&
                             user.FirstName == expectedProfile.FirstName &&
                             user.LastName == expectedProfile.LastName &&
                             user.PreferredCulture == expectedProfile.PreferredCulture &&
                             user.EnablePriceAlerts == expectedProfile.EnablePriceAlerts &&
                             isInRole;

                if (!isValid)
                {
                    _logger.LogWarning("Test user {Email} exists but doesn't match expected profile", email);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating test user {Email}", email);
                return false;
            }
        }

        private async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole(roleName);
                var result = await _roleManager.CreateAsync(role);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("Created role {RoleName}", roleName);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to create role {RoleName}: {Errors}", roleName, errors);
                    throw new InvalidOperationException($"Failed to create role {roleName}: {errors}");
                }
            }
        }
    }

    /// <summary>
    /// Predefined test user profiles for common testing scenarios
    /// </summary>
    public static class TestUserProfiles
    {
        public static TestUserProfile StandardUser => new()
        {
            Email = "testuser@example.com",
            Password = "TestUser123!",
            FirstName = "John",
            LastName = "Doe",
            Role = "User",
            PreferredCulture = "en",
            EnablePriceAlerts = true
        };

        public static TestUserProfile AdminUser => new()
        {
            Email = "admin@example.com",
            Password = "AdminUser123!",
            FirstName = "Admin",
            LastName = "User",
            Role = "Administrator",
            PreferredCulture = "en",
            EnablePriceAlerts = true
        };

        public static TestUserProfile FrenchUser => new()
        {
            Email = "frenchuser@example.com",
            Password = "FrenchUser123!",
            FirstName = "Marie",
            LastName = "Dupont",
            Role = "User",
            PreferredCulture = "fr",
            EnablePriceAlerts = false
        };

        public static TestUserProfile PremiumUser => new()
        {
            Email = "premium@example.com",
            Password = "PremiumUser123!",
            FirstName = "Premium",
            LastName = "Subscriber",
            Role = "Premium",
            PreferredCulture = "en",
            EnablePriceAlerts = true
        };

        public static IEnumerable<TestUserProfile> AllProfiles => new[]
        {
            StandardUser,
            AdminUser,
            FrenchUser,
            PremiumUser
        };
    }
}
