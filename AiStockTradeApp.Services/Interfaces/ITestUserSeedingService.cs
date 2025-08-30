using AiStockTradeApp.Entities.Models;

namespace AiStockTradeApp.Services
{
    /// <summary>
    /// Interface for seeding and managing test users for testing scenarios
    /// </summary>
    public interface ITestUserSeedingService
    {
        /// <summary>
        /// Seeds a test user if it doesn't exist
        /// </summary>
        /// <param name="profile">Profile defining the test user properties</param>
        /// <returns>The created or existing test user</returns>
        Task<ApplicationUser> SeedTestUserIfNotExistsAsync(TestUserProfile profile);

        /// <summary>
        /// Seeds multiple test users if they don't exist
        /// </summary>
        /// <param name="profiles">Collection of user profiles to seed</param>
        /// <returns>Collection of created or existing test users</returns>
        Task<IEnumerable<ApplicationUser>> SeedTestUsersIfNotExistAsync(IEnumerable<TestUserProfile> profiles);

        /// <summary>
        /// Gets a test user by email, creating it if it doesn't exist with default settings
        /// </summary>
        /// <param name="email">Email address of the test user</param>
        /// <param name="role">Role to assign to the user if created (defaults to "User")</param>
        /// <returns>The existing or newly created test user</returns>
        Task<ApplicationUser?> GetOrCreateTestUserAsync(string email, string? role = "User");

        /// <summary>
        /// Cleans up test users by email addresses (for test teardown)
        /// </summary>
        /// <param name="emails">Email addresses of users to clean up</param>
        Task CleanupTestUsersAsync(IEnumerable<string> emails);

        /// <summary>
        /// Validates that a test user exists and has expected properties
        /// </summary>
        /// <param name="email">Email address of the user to validate</param>
        /// <param name="expectedProfile">Expected user profile to validate against</param>
        /// <returns>True if user exists and matches expected profile</returns>
        Task<bool> ValidateTestUserAsync(string email, TestUserProfile expectedProfile);
    }

    /// <summary>
    /// Profile for creating test users
    /// </summary>
    public class TestUserProfile
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = "TestPassword123!";
        public string FirstName { get; set; } = "Test";
        public string LastName { get; set; } = "User";
        public string Role { get; set; } = "User";
        public string PreferredCulture { get; set; } = "en";
        public bool EnablePriceAlerts { get; set; } = true;
    }
}
