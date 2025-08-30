using FluentAssertions;
using Xunit;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.Services;
using AiStockTradeApp.Services.Implementations;

namespace AiStockTradeApp.Tests.Examples
{
    /// <summary>
    /// Example tests demonstrating how to use the test setup helper and user seeding
    /// These serve as both tests and documentation for other developers
    /// </summary>
    [RequiresUserSeeding("User", "Administrator")]
    public class UserSeedingExampleTests : BaseUserSeedingTest
    {
        public UserSeedingExampleTests()
        {
            // Initialize the test environment
            InitializeTestAsync().Wait();
        }

        [Fact]
        public async Task Example_SeedSingleUser_ShouldCreateUserSuccessfully()
        {
            // Arrange & Act - Seed a standard test user
            var user = await SeedTestUserAsync();

            // Assert
            user.Should().NotBeNull();
            user.Email.Should().Be(TestUserProfiles.StandardUser.Email);
            user.FirstName.Should().Be("John");
            user.LastName.Should().Be("Doe");

            // Verify user exists in database
            var dbUser = await UserManager.FindByEmailAsync(user.Email);
            dbUser.Should().NotBeNull();
            dbUser!.Id.Should().Be(user.Id);
        }

        [Fact]
        public async Task Example_SeedCustomUser_ShouldCreateUserWithSpecifiedProperties()
        {
            // Arrange
            var customProfile = new TestUserProfile
            {
                Email = "custom@example.com",
                FirstName = "Custom",
                LastName = "User",
                Role = "Administrator",
                PreferredCulture = "fr",
                EnablePriceAlerts = false,
                Password = "CustomPassword123!"
            };

            // Act
            var user = await SeedTestUserAsync(customProfile);

            // Assert
            user.Should().NotBeNull();
            user.Email.Should().Be("custom@example.com");
            user.FirstName.Should().Be("Custom");
            user.PreferredCulture.Should().Be("fr");
            user.EnablePriceAlerts.Should().BeFalse();

            // Verify user is in correct role
            var isInRole = await UserManager.IsInRoleAsync(user, "Administrator");
            isInRole.Should().BeTrue();
        }

        [Fact]
        public async Task Example_SeedMultipleUsers_ShouldCreateAllUsers()
        {
            // Act - Seed all predefined user profiles
            var users = await SeedTestUsersAsync();

            // Assert
            users.Should().HaveCount(4); // All profiles from TestUserProfiles.AllProfiles
            
            var emails = users.Select(u => u.Email).ToList();
            emails.Should().Contain(TestUserProfiles.StandardUser.Email);
            emails.Should().Contain(TestUserProfiles.AdminUser.Email);
            emails.Should().Contain(TestUserProfiles.FrenchUser.Email);
            emails.Should().Contain(TestUserProfiles.PremiumUser.Email);

            // Verify each user has correct role
            foreach (var user in users)
            {
                var profile = TestUserProfiles.AllProfiles.First(p => p.Email == user.Email);
                var isInRole = await UserManager.IsInRoleAsync(user, profile.Role);
                isInRole.Should().BeTrue($"User {user.Email} should be in role {profile.Role}");
            }
        }

        [Theory]
        [MemberData(nameof(TestUserDataGenerator.GetValidUserProfiles), MemberType = typeof(TestUserDataGenerator))]
        public async Task Example_SeedFromTestData_ShouldCreateUserCorrectly(TestUserProfile profile)
        {
            // Act
            var user = await SeedTestUserAsync(profile);

            // Assert
            user.Should().NotBeNull();
            user.Email.Should().Be(profile.Email);
            user.FirstName.Should().Be(profile.FirstName);
            user.LastName.Should().Be(profile.LastName);
            user.PreferredCulture.Should().Be(profile.PreferredCulture);

            // Verify password was set (by attempting to check password)
            var passwordCheck = await UserManager.CheckPasswordAsync(user, profile.Password);
            passwordCheck.Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(TestUserDataGenerator.GetUserRoleCombinations), MemberType = typeof(TestUserDataGenerator))]
        public async Task Example_SeedUserWithRole_ShouldCreateUserInCorrectRole(string email, string role)
        {
            // Act
            var user = await TestHelper.SeedUserWithRoleAsync(email, role);

            // Assert
            user.Should().NotBeNull();
            user.Email.Should().Be(email);

            var isInRole = await UserManager.IsInRoleAsync(user, role);
            isInRole.Should().BeTrue();

            // Verify role exists
            var roleExists = await RoleManager.RoleExistsAsync(role);
            roleExists.Should().BeTrue();
        }

        [Fact]
        public async Task Example_ValidateUserExists_ShouldReturnCorrectResults()
        {
            // Arrange
            var user = await SeedTestUserAsync(TestUserProfiles.AdminUser);

            // Act & Assert - User should exist
            var exists = await TestHelper.ValidateUserExistsAsync(user.Email!);
            exists.Should().BeTrue();

            // Act & Assert - User should be in correct role
            var existsWithRole = await TestHelper.ValidateUserExistsAsync(user.Email!, "Administrator");
            existsWithRole.Should().BeTrue();

            // Act & Assert - User should not be in wrong role
            var existsWithWrongRole = await TestHelper.ValidateUserExistsAsync(user.Email!, "WrongRole");
            existsWithWrongRole.Should().BeFalse();

            // Act & Assert - Non-existent user should not exist
            var nonExistentExists = await TestHelper.ValidateUserExistsAsync("nonexistent@example.com");
            nonExistentExists.Should().BeFalse();
        }

        [Fact]
        public async Task Example_GetUsersInRole_ShouldReturnCorrectUsers()
        {
            // Arrange - Seed users with different roles
            await SeedTestUsersAsync(TestUserProfiles.StandardUser, TestUserProfiles.AdminUser, TestUserProfiles.PremiumUser);

            // Act
            var adminUsers = await TestHelper.GetUsersInRoleAsync("Administrator");
            var regularUsers = await TestHelper.GetUsersInRoleAsync("User");
            var premiumUsers = await TestHelper.GetUsersInRoleAsync("Premium");

            // Assert
            adminUsers.Should().HaveCount(1);
            adminUsers.First().Email.Should().Be(TestUserProfiles.AdminUser.Email);

            regularUsers.Should().HaveCount(1);
            regularUsers.First().Email.Should().Be(TestUserProfiles.StandardUser.Email);

            premiumUsers.Should().HaveCount(1);
            premiumUsers.First().Email.Should().Be(TestUserProfiles.PremiumUser.Email);
        }

        [Fact]
        public async Task Example_CleanupSpecificUser_ShouldRemoveUser()
        {
            // Arrange
            var user = await SeedTestUserAsync();
            var userEmail = user.Email;

            // Verify user exists
            var existsBefore = await TestHelper.ValidateUserExistsAsync(userEmail!);
            existsBefore.Should().BeTrue();

            // Act
            await TestHelper.CleanupUserAsync(userEmail!);

            // Assert
            var existsAfter = await TestHelper.ValidateUserExistsAsync(userEmail!);
            existsAfter.Should().BeFalse();
        }

        [Fact]
        public async Task Example_RandomUserGeneration_ShouldCreateUniqueUsers()
        {
            // Arrange & Act - Generate multiple random users
            var randomProfile1 = TestUserDataGenerator.GenerateRandomProfile();
            var randomProfile2 = TestUserDataGenerator.GenerateRandomProfile();

            var user1 = await SeedTestUserAsync(randomProfile1);
            var user2 = await SeedTestUserAsync(randomProfile2);

            // Assert
            user1.Email.Should().NotBe(user2.Email);
            user1.FirstName.Should().NotBe(user2.FirstName);
            user1.LastName.Should().NotBe(user2.LastName);

            // Both users should exist
            var exists1 = await TestHelper.ValidateUserExistsAsync(user1.Email!);
            var exists2 = await TestHelper.ValidateUserExistsAsync(user2.Email!);
            
            exists1.Should().BeTrue();
            exists2.Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(TestUserDataGenerator.GetCultureTestData), MemberType = typeof(TestUserDataGenerator))]
        public async Task Example_SeedUserWithCulture_ShouldSetCultureCorrectly(string culture, string displayName, bool enableAlerts)
        {
            // Arrange
            var profile = new TestUserProfile
            {
                Email = $"culture-{culture}@example.com",
                FirstName = displayName.Split(' ')[0],
                LastName = displayName.Split(' ')[1],
                PreferredCulture = culture,
                EnablePriceAlerts = enableAlerts,
                Role = "User"
            };

            // Act
            var user = await SeedTestUserAsync(profile);

            // Assert
            user.PreferredCulture.Should().Be(culture);
            user.EnablePriceAlerts.Should().Be(enableAlerts);
            user.FirstName.Should().Be(profile.FirstName);
            user.LastName.Should().Be(profile.LastName);
        }

        [Fact]
        public async Task Example_CommonRolesSeeding_ShouldCreateAllRoles()
        {
            // Act
            await TestHelper.SeedCommonRolesAsync();

            // Assert
            var commonRoles = new[] { "Administrator", "User", "Premium", "Moderator" };
            
            foreach (var roleName in commonRoles)
            {
                var roleExists = await RoleManager.RoleExistsAsync(roleName);
                roleExists.Should().BeTrue($"Role {roleName} should exist");
            }
        }
    }

    /// <summary>
    /// Example tests showing logging verification patterns
    /// </summary>
    public class LoggingExampleTests
    {
        [Fact]
        public void Example_VerifyLoggingPatterns_ShouldDemonstrateCorrectUsage()
        {
            // This test demonstrates how to verify logging in your services
            // See ServiceLoggingIntegrationTests for actual implementations
            
            // Example pattern for testing logging:
            // 1. Arrange - Set up mock logger
            // 2. Act - Call service method
            // 3. Assert - Verify correct log entries were created
            
            true.Should().BeTrue("This is an example test showing logging patterns");
        }
    }
}
