using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.Services;
using AiStockTradeApp.Services.Implementations;

namespace AiStockTradeApp.Tests.Services
{
    /// <summary>
    /// Tests for TestUserSeedingService functionality
    /// </summary>
    public class TestUserSeedingServiceTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
        private readonly Mock<ILogger<TestUserSeedingService>> _mockLogger;
        private readonly TestUserSeedingService _service;

        public TestUserSeedingServiceTests()
        {
            // Mock UserManager
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            // Mock RoleManager
            var roleStore = new Mock<IRoleStore<IdentityRole>>();
            _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
                roleStore.Object, null!, null!, null!, null!);

            _mockLogger = new Mock<ILogger<TestUserSeedingService>>();

            _service = new TestUserSeedingService(_mockUserManager.Object, _mockRoleManager.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task SeedTestUserIfNotExistsAsync_UserDoesNotExist_ShouldCreateUser()
        {
            // Arrange
            var profile = TestUserProfiles.StandardUser;
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(profile.Email))
                .ReturnsAsync((ApplicationUser?)null);
            
            _mockRoleManager.Setup(x => x.RoleExistsAsync(profile.Role))
                .ReturnsAsync(true);
            
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), profile.Password))
                .ReturnsAsync(IdentityResult.Success);
            
            _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), profile.Role))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _service.SeedTestUserIfNotExistsAsync(profile);

            // Assert
            result.Should().NotBeNull();
            result.Email.Should().Be(profile.Email);
            result.FirstName.Should().Be(profile.FirstName);
            result.LastName.Should().Be(profile.LastName);
            result.PreferredCulture.Should().Be(profile.PreferredCulture);
            result.EnablePriceAlerts.Should().Be(profile.EnablePriceAlerts);

            _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), profile.Password), Times.Once);
            _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), profile.Role), Times.Once);
        }

        [Fact]
        public async Task SeedTestUserIfNotExistsAsync_UserExists_ShouldReturnExistingUser()
        {
            // Arrange
            var profile = TestUserProfiles.StandardUser;
            var existingUser = new ApplicationUser
            {
                Id = "existing-user-id",
                Email = profile.Email,
                FirstName = profile.FirstName,
                LastName = profile.LastName
            };
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(profile.Email))
                .ReturnsAsync(existingUser);

            // Act
            var result = await _service.SeedTestUserIfNotExistsAsync(profile);

            // Assert
            result.Should().BeSameAs(existingUser);
            result.Id.Should().Be("existing-user-id");

            _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
            _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SeedTestUserIfNotExistsAsync_RoleDoesNotExist_ShouldCreateRoleAndUser()
        {
            // Arrange
            var profile = TestUserProfiles.AdminUser;
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(profile.Email))
                .ReturnsAsync((ApplicationUser?)null);
            
            _mockRoleManager.Setup(x => x.RoleExistsAsync(profile.Role))
                .ReturnsAsync(false);
            
            _mockRoleManager.Setup(x => x.CreateAsync(It.IsAny<IdentityRole>()))
                .ReturnsAsync(IdentityResult.Success);
            
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), profile.Password))
                .ReturnsAsync(IdentityResult.Success);
            
            _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), profile.Role))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _service.SeedTestUserIfNotExistsAsync(profile);

            // Assert
            result.Should().NotBeNull();
            
            _mockRoleManager.Verify(x => x.CreateAsync(It.Is<IdentityRole>(r => r.Name == profile.Role)), Times.Once);
            _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), profile.Password), Times.Once);
        }

        [Fact]
        public async Task SeedTestUserIfNotExistsAsync_UserCreationFails_ShouldThrowException()
        {
            // Arrange
            var profile = TestUserProfiles.StandardUser;
            var failureResult = IdentityResult.Failed(new IdentityError { Description = "Creation failed" });
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(profile.Email))
                .ReturnsAsync((ApplicationUser?)null);
            
            _mockRoleManager.Setup(x => x.RoleExistsAsync(profile.Role))
                .ReturnsAsync(true);
            
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), profile.Password))
                .ReturnsAsync(failureResult);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.SeedTestUserIfNotExistsAsync(profile));
            
            exception.Message.Should().Contain("Failed to create test user");
            exception.Message.Should().Contain("Creation failed");
        }

        [Fact]
        public async Task SeedTestUsersIfNotExistAsync_MultipleUsers_ShouldCreateAllUsers()
        {
            // Arrange
            var profiles = new[] { TestUserProfiles.StandardUser, TestUserProfiles.FrenchUser };
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);
            
            _mockRoleManager.Setup(x => x.RoleExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            
            _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _service.SeedTestUsersIfNotExistAsync(profiles);

            // Assert
            result.Should().HaveCount(2);
            result.Select(u => u.Email).Should().Contain(TestUserProfiles.StandardUser.Email);
            result.Select(u => u.Email).Should().Contain(TestUserProfiles.FrenchUser.Email);

            _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task SeedTestUsersIfNotExistAsync_SomeUsersFail_ShouldContinueWithOthers()
        {
            // Arrange
            var profiles = new[] { TestUserProfiles.StandardUser, TestUserProfiles.AdminUser };
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(TestUserProfiles.StandardUser.Email))
                .ReturnsAsync((ApplicationUser?)null);
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(TestUserProfiles.AdminUser.Email))
                .ReturnsAsync((ApplicationUser?)null);
            
            _mockRoleManager.Setup(x => x.RoleExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            
            // First user succeeds
            _mockUserManager.Setup(x => x.CreateAsync(It.Is<ApplicationUser>(u => u.Email == TestUserProfiles.StandardUser.Email), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            
            // Second user fails
            _mockUserManager.Setup(x => x.CreateAsync(It.Is<ApplicationUser>(u => u.Email == TestUserProfiles.AdminUser.Email), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Creation failed" }));
            
            _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _service.SeedTestUsersIfNotExistAsync(profiles);

            // Assert
            result.Should().HaveCount(1);
            result.First().Email.Should().Be(TestUserProfiles.StandardUser.Email);
        }

        [Fact]
        public async Task GetOrCreateTestUserAsync_UserExists_ShouldReturnExistingUser()
        {
            // Arrange
            var email = "existing@example.com";
            var existingUser = new ApplicationUser { Email = email, Id = "existing-id" };
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(email))
                .ReturnsAsync(existingUser);

            // Act
            var result = await _service.GetOrCreateTestUserAsync(email);

            // Assert
            result.Should().BeSameAs(existingUser);
            _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetOrCreateTestUserAsync_UserDoesNotExist_ShouldCreateDefaultUser()
        {
            // Arrange
            var email = "newuser@example.com";
            var role = "TestRole";
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(email))
                .ReturnsAsync((ApplicationUser?)null);
            
            _mockRoleManager.Setup(x => x.RoleExistsAsync(role))
                .ReturnsAsync(true);
            
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            
            _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), role))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _service.GetOrCreateTestUserAsync(email, role);

            // Assert
            result.Should().NotBeNull();
            result!.Email.Should().Be(email);
            result.FirstName.Should().Be("Test");
            result.LastName.Should().Be("User");
            
            _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "TestPassword123!"), Times.Once);
            _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), role), Times.Once);
        }

        [Fact]
        public async Task CleanupTestUsersAsync_ExistingUsers_ShouldDeleteUsers()
        {
            // Arrange
            var emails = new[] { "user1@example.com", "user2@example.com" };
            var user1 = new ApplicationUser { Email = emails[0], Id = "user1" };
            var user2 = new ApplicationUser { Email = emails[1], Id = "user2" };
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(emails[0]))
                .ReturnsAsync(user1);
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(emails[1]))
                .ReturnsAsync(user2);
            
            _mockUserManager.Setup(x => x.DeleteAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            await _service.CleanupTestUsersAsync(emails);

            // Assert
            _mockUserManager.Verify(x => x.DeleteAsync(user1), Times.Once);
            _mockUserManager.Verify(x => x.DeleteAsync(user2), Times.Once);
        }

        [Fact]
        public async Task ValidateTestUserAsync_UserMatchesProfile_ShouldReturnTrue()
        {
            // Arrange
            var profile = TestUserProfiles.StandardUser;
            var user = new ApplicationUser
            {
                Email = profile.Email,
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                PreferredCulture = profile.PreferredCulture,
                EnablePriceAlerts = profile.EnablePriceAlerts
            };
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(profile.Email))
                .ReturnsAsync(user);
            
            _mockUserManager.Setup(x => x.IsInRoleAsync(user, profile.Role))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateTestUserAsync(profile.Email, profile);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateTestUserAsync_UserDoesNotExist_ShouldReturnFalse()
        {
            // Arrange
            var profile = TestUserProfiles.StandardUser;
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(profile.Email))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _service.ValidateTestUserAsync(profile.Email, profile);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateTestUserAsync_UserPropertiesDontMatch_ShouldReturnFalse()
        {
            // Arrange
            var profile = TestUserProfiles.StandardUser;
            var user = new ApplicationUser
            {
                Email = profile.Email,
                FirstName = "Wrong Name", // Different from profile
                LastName = profile.LastName,
                PreferredCulture = profile.PreferredCulture,
                EnablePriceAlerts = profile.EnablePriceAlerts
            };
            
            _mockUserManager.Setup(x => x.FindByEmailAsync(profile.Email))
                .ReturnsAsync(user);
            
            _mockUserManager.Setup(x => x.IsInRoleAsync(user, profile.Role))
                .ReturnsAsync(true);

            // Act
            var result = await _service.ValidateTestUserAsync(profile.Email, profile);

            // Assert
            result.Should().BeFalse();
        }
    }

    /// <summary>
    /// Tests for TestUserProfile and TestUserProfiles
    /// </summary>
    public class TestUserProfileTests
    {
        [Fact]
        public void TestUserProfile_DefaultConstructor_ShouldSetDefaults()
        {
            // Act
            var profile = new TestUserProfile();

            // Assert
            profile.Email.Should().BeEmpty();
            profile.Password.Should().Be("TestPassword123!");
            profile.FirstName.Should().Be("Test");
            profile.LastName.Should().Be("User");
            profile.Role.Should().Be("User");
            profile.PreferredCulture.Should().Be("en");
            profile.EnablePriceAlerts.Should().BeTrue();
        }

        [Fact]
        public void TestUserProfiles_StandardUser_ShouldHaveExpectedProperties()
        {
            // Act
            var profile = TestUserProfiles.StandardUser;

            // Assert
            profile.Email.Should().Be("testuser@example.com");
            profile.Password.Should().Be("TestUser123!");
            profile.FirstName.Should().Be("John");
            profile.LastName.Should().Be("Doe");
            profile.Role.Should().Be("User");
            profile.PreferredCulture.Should().Be("en");
            profile.EnablePriceAlerts.Should().BeTrue();
        }

        [Fact]
        public void TestUserProfiles_AdminUser_ShouldHaveAdministratorRole()
        {
            // Act
            var profile = TestUserProfiles.AdminUser;

            // Assert
            profile.Email.Should().Be("admin@example.com");
            profile.Role.Should().Be("Administrator");
            profile.FirstName.Should().Be("Admin");
            profile.LastName.Should().Be("User");
        }

        [Fact]
        public void TestUserProfiles_FrenchUser_ShouldHaveFrenchCulture()
        {
            // Act
            var profile = TestUserProfiles.FrenchUser;

            // Assert
            profile.Email.Should().Be("frenchuser@example.com");
            profile.PreferredCulture.Should().Be("fr");
            profile.FirstName.Should().Be("Marie");
            profile.LastName.Should().Be("Dupont");
            profile.EnablePriceAlerts.Should().BeFalse();
        }

        [Fact]
        public void TestUserProfiles_PremiumUser_ShouldHavePremiumRole()
        {
            // Act
            var profile = TestUserProfiles.PremiumUser;

            // Assert
            profile.Email.Should().Be("premium@example.com");
            profile.Role.Should().Be("Premium");
            profile.FirstName.Should().Be("Premium");
            profile.LastName.Should().Be("Subscriber");
        }

        [Fact]
        public void TestUserProfiles_AllProfiles_ShouldContainAllPredefinedProfiles()
        {
            // Act
            var allProfiles = TestUserProfiles.AllProfiles.ToList();

            // Assert
            allProfiles.Should().HaveCount(4);
            allProfiles.Should().Contain(p => p.Email == TestUserProfiles.StandardUser.Email);
            allProfiles.Should().Contain(p => p.Email == TestUserProfiles.AdminUser.Email);
            allProfiles.Should().Contain(p => p.Email == TestUserProfiles.FrenchUser.Email);
            allProfiles.Should().Contain(p => p.Email == TestUserProfiles.PremiumUser.Email);
        }

        [Theory]
        [InlineData("testuser@example.com")]
        [InlineData("admin@example.com")]
        [InlineData("frenchuser@example.com")]
        [InlineData("premium@example.com")]
        public void TestUserProfiles_AllEmails_ShouldBeValidEmailFormat(string email)
        {
            // Assert
            email.Should().Contain("@");
            email.Should().Contain(".");
            email.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void TestUserProfiles_AllPasswords_ShouldMeetSecurityRequirements()
        {
            // Act
            var allProfiles = TestUserProfiles.AllProfiles;

            // Assert
            foreach (var profile in allProfiles)
            {
                profile.Password.Should().NotBeNullOrWhiteSpace();
                profile.Password.Length.Should().BeGreaterThanOrEqualTo(8);
                profile.Password.Should().MatchRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]", 
                    "Password should contain uppercase, lowercase, number, and special character");
            }
        }
    }
}
