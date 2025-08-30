using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AiStockTradeApp.Entities.Models;
using AiStockTradeApp.DataAccess.Data;
using AiStockTradeApp.Services;
using System.Text.Json;

namespace AiStockTradeApp.Services.Implementations
{
    /// <summary>
    /// Service for diagnosing authentication issues in Azure deployments
    /// </summary>
    public interface IAuthenticationDiagnosticsService
    {
        Task<AuthenticationDiagnosticsResult> DiagnoseRegistrationIssueAsync(string email, string? correlationId = null);
        Task<AuthenticationDiagnosticsResult> DiagnoseLoginIssueAsync(string email, string? correlationId = null);
        Task<DatabaseHealthCheckResult> CheckDatabaseHealthAsync();
        Task<IdentityConfigurationResult> ValidateIdentityConfigurationAsync();
        Task LogSystemInformationAsync();
    }

    public class AuthenticationDiagnosticsService : IAuthenticationDiagnosticsService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationIdentityContext _identityContext;
        private readonly ILogger<AuthenticationDiagnosticsService> _logger;

        public AuthenticationDiagnosticsService(
            UserManager<ApplicationUser> userManager,
            ApplicationIdentityContext identityContext,
            ILogger<AuthenticationDiagnosticsService> logger)
        {
            _userManager = userManager;
            _identityContext = identityContext;
            _logger = logger;
        }

        public async Task<AuthenticationDiagnosticsResult> DiagnoseRegistrationIssueAsync(string email, string? correlationId = null)
        {
            var result = new AuthenticationDiagnosticsResult
            {
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                Email = email,
                Timestamp = DateTime.UtcNow,
                DiagnosticType = "Registration"
            };

            try
            {
                _logger.LogInformation("Starting registration diagnostics for {Email} with correlation {CorrelationId}", 
                    email, result.CorrelationId);

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(email);
                result.UserExists = existingUser != null;
                
                if (existingUser != null)
                {
                    result.Issues.Add($"User with email {email} already exists (ID: {existingUser.Id})");
                    result.UserDetails = new UserDiagnosticInfo
                    {
                        UserId = existingUser.Id,
                        UserName = existingUser.UserName,
                        Email = existingUser.Email,
                        EmailConfirmed = existingUser.EmailConfirmed,
                        LockoutEnabled = existingUser.LockoutEnabled,
                        LockoutEnd = existingUser.LockoutEnd?.DateTime,
                        AccessFailedCount = existingUser.AccessFailedCount,
                        CreatedAt = existingUser.CreatedAt,
                        LastLoginAt = existingUser.LastLoginAt
                    };
                }

                // Check database connection
                var canConnect = await _identityContext.Database.CanConnectAsync();
                result.DatabaseConnected = canConnect;
                
                if (!canConnect)
                {
                    result.Issues.Add("Cannot connect to identity database");
                }

                // Check database tables
                var tablesExist = await CheckRequiredTablesExist();
                result.DatabaseTablesExist = tablesExist;
                
                if (!tablesExist)
                {
                    result.Issues.Add("Required Identity tables do not exist in database");
                }

                // Check user manager configuration
                var userManagerOptions = _userManager.Options;
                result.IdentityConfiguration = new IdentityConfigurationInfo
                {
                    RequireUniqueEmail = userManagerOptions.User.RequireUniqueEmail,
                    RequireConfirmedEmail = userManagerOptions.SignIn.RequireConfirmedEmail,
                    RequireConfirmedPhoneNumber = userManagerOptions.SignIn.RequireConfirmedPhoneNumber,
                    PasswordRequiredLength = userManagerOptions.Password.RequiredLength,
                    PasswordRequireDigit = userManagerOptions.Password.RequireDigit,
                    PasswordRequireLowercase = userManagerOptions.Password.RequireLowercase,
                    PasswordRequireUppercase = userManagerOptions.Password.RequireUppercase,
                    PasswordRequireNonAlphanumeric = userManagerOptions.Password.RequireNonAlphanumeric
                };

                result.Success = !result.Issues.Any();
                
                _logger.LogAuthenticationEvent("RegistrationDiagnostics", email, result.Success, 
                    result.Success ? null : string.Join(", ", result.Issues), 
                    new { CorrelationId = result.CorrelationId, IssueCount = result.Issues.Count });

            }
            catch (Exception ex)
            {
                result.Issues.Add($"Exception during diagnostics: {ex.Message}");
                result.Success = false;
                
                _logger.LogError(ex, "Error during registration diagnostics for {Email} with correlation {CorrelationId}", 
                    email, result.CorrelationId);
            }

            return result;
        }

        public async Task<AuthenticationDiagnosticsResult> DiagnoseLoginIssueAsync(string email, string? correlationId = null)
        {
            var result = new AuthenticationDiagnosticsResult
            {
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                Email = email,
                Timestamp = DateTime.UtcNow,
                DiagnosticType = "Login"
            };

            try
            {
                _logger.LogInformation("Starting login diagnostics for {Email} with correlation {CorrelationId}", 
                    email, result.CorrelationId);

                // Check if user exists
                var user = await _userManager.FindByEmailAsync(email);
                result.UserExists = user != null;
                
                if (user == null)
                {
                    result.Issues.Add($"User with email {email} does not exist");
                }
                else
                {
                    result.UserDetails = new UserDiagnosticInfo
                    {
                        UserId = user.Id,
                        UserName = user.UserName,
                        Email = user.Email,
                        EmailConfirmed = user.EmailConfirmed,
                        LockoutEnabled = user.LockoutEnabled,
                        LockoutEnd = user.LockoutEnd?.DateTime,
                        AccessFailedCount = user.AccessFailedCount,
                        CreatedAt = user.CreatedAt,
                        LastLoginAt = user.LastLoginAt
                    };

                    // Check account status
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        result.Issues.Add($"Account is locked out until {user.LockoutEnd}");
                    }

                    if (!user.EmailConfirmed && _userManager.Options.SignIn.RequireConfirmedEmail)
                    {
                        result.Issues.Add("Email address is not confirmed and confirmation is required");
                    }

                    // Check if user can sign in (basic checks without SignInManager)
                    var isLockedOut = await _userManager.IsLockedOutAsync(user);
                    var emailConfirmed = user.EmailConfirmed;
                    var requiresEmailConfirmation = _userManager.Options.SignIn.RequireConfirmedEmail;
                    
                    if (isLockedOut)
                    {
                        result.Issues.Add($"Account is locked out until {user.LockoutEnd}");
                    }
                    
                    if (!emailConfirmed && requiresEmailConfirmation)
                    {
                        result.Issues.Add("Email address is not confirmed and confirmation is required");
                    }
                }

                // Check database connection
                result.DatabaseConnected = await _identityContext.Database.CanConnectAsync();
                if (!result.DatabaseConnected)
                {
                    result.Issues.Add("Cannot connect to identity database");
                }

                result.Success = !result.Issues.Any();
                
                _logger.LogAuthenticationEvent("LoginDiagnostics", email, result.Success, 
                    result.Success ? null : string.Join(", ", result.Issues), 
                    new { CorrelationId = result.CorrelationId, IssueCount = result.Issues.Count });

            }
            catch (Exception ex)
            {
                result.Issues.Add($"Exception during diagnostics: {ex.Message}");
                result.Success = false;
                
                _logger.LogError(ex, "Error during login diagnostics for {Email} with correlation {CorrelationId}", 
                    email, result.CorrelationId);
            }

            return result;
        }

        public async Task<DatabaseHealthCheckResult> CheckDatabaseHealthAsync()
        {
            var result = new DatabaseHealthCheckResult
            {
                Timestamp = DateTime.UtcNow
            };

            try
            {
                // Test connection
                result.CanConnect = await _identityContext.Database.CanConnectAsync();
                
                if (result.CanConnect)
                {
                    // Check if tables exist
                    result.TablesExist = await CheckRequiredTablesExist();
                    
                    // Count users
                    result.UserCount = await _identityContext.Users.CountAsync();
                    
                    // Check recent activity
                    var recentUsers = await _identityContext.Users
                        .Where(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                        .CountAsync();
                    result.RecentRegistrations = recentUsers;
                    
                    // Test write operation
                    var testGuid = Guid.NewGuid().ToString();
                    var testUser = new ApplicationUser
                    {
                        UserName = $"test_{testGuid}@diagnostics.test",
                        Email = $"test_{testGuid}@diagnostics.test",
                        FirstName = "Test",
                        LastName = "Diagnostics",
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    var createResult = await _userManager.CreateAsync(testUser);
                    result.CanWrite = createResult.Succeeded;
                    
                    if (createResult.Succeeded)
                    {
                        // Clean up test user
                        await _userManager.DeleteAsync(testUser);
                    }
                    else
                    {
                        result.WriteErrors = createResult.Errors.Select(e => e.Description).ToList();
                    }
                }

                result.Success = result.CanConnect && result.TablesExist && result.CanWrite;
                
                _logger.LogInformation("Database health check completed: {Success}. Details: {Result}", 
                    result.Success, JsonSerializer.Serialize(result));

            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                
                _logger.LogError(ex, "Error during database health check");
            }

            return result;
        }

        public Task<IdentityConfigurationResult> ValidateIdentityConfigurationAsync()
        {
            var result = new IdentityConfigurationResult
            {
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var userOptions = _userManager.Options.User;
                var passwordOptions = _userManager.Options.Password;
                var signInOptions = _userManager.Options.SignIn;
                var lockoutOptions = _userManager.Options.Lockout;

                result.UserConfiguration = new
                {
                    RequireUniqueEmail = userOptions.RequireUniqueEmail,
                    AllowedUserNameCharacters = userOptions.AllowedUserNameCharacters
                };

                result.PasswordConfiguration = new
                {
                    RequiredLength = passwordOptions.RequiredLength,
                    RequireDigit = passwordOptions.RequireDigit,
                    RequireLowercase = passwordOptions.RequireLowercase,
                    RequireUppercase = passwordOptions.RequireUppercase,
                    RequireNonAlphanumeric = passwordOptions.RequireNonAlphanumeric,
                    RequiredUniqueChars = passwordOptions.RequiredUniqueChars
                };

                result.SignInConfiguration = new
                {
                    RequireConfirmedEmail = signInOptions.RequireConfirmedEmail,
                    RequireConfirmedPhoneNumber = signInOptions.RequireConfirmedPhoneNumber
                };

                result.LockoutConfiguration = new
                {
                    AllowedForNewUsers = lockoutOptions.AllowedForNewUsers,
                    DefaultLockoutTimeSpan = lockoutOptions.DefaultLockoutTimeSpan,
                    MaxFailedAccessAttempts = lockoutOptions.MaxFailedAccessAttempts
                };

                // Check for potential issues
                if (!userOptions.RequireUniqueEmail)
                {
                    result.Warnings.Add("Email uniqueness is not required - this may cause issues");
                }

                if (signInOptions.RequireConfirmedEmail)
                {
                    result.Warnings.Add("Email confirmation is required but may not be configured properly");
                }

                result.Success = true;
                
                _logger.LogInformation("Identity configuration validation completed: {Configuration}", 
                    JsonSerializer.Serialize(result));

            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                
                _logger.LogError(ex, "Error during identity configuration validation");
            }

            return Task.FromResult(result);
        }

        public async Task LogSystemInformationAsync()
        {
            try
            {
                var systemInfo = new
                {
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    MachineName = Environment.MachineName,
                    OSVersion = Environment.OSVersion.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet,
                    Is64BitProcess = Environment.Is64BitProcess,
                    CLRVersion = Environment.Version.ToString(),
                    DatabaseProvider = _identityContext.Database.ProviderName,
                    ConnectionString = _identityContext.Database.GetConnectionString()?.Substring(0, Math.Min(50, _identityContext.Database.GetConnectionString()?.Length ?? 0)) + "...",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("System Information: {SystemInfo}", JsonSerializer.Serialize(systemInfo));

                // Log database information
                var canConnect = await _identityContext.Database.CanConnectAsync();
                var pendingMigrations = await _identityContext.Database.GetPendingMigrationsAsync();
                var appliedMigrations = await _identityContext.Database.GetAppliedMigrationsAsync();

                var databaseInfo = new
                {
                    CanConnect = canConnect,
                    PendingMigrationsCount = pendingMigrations.Count(),
                    AppliedMigrationsCount = appliedMigrations.Count(),
                    PendingMigrations = pendingMigrations.Take(5), // Log first 5 pending migrations
                    LatestAppliedMigration = appliedMigrations.LastOrDefault()
                };

                _logger.LogInformation("Database Information: {DatabaseInfo}", JsonSerializer.Serialize(databaseInfo));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging system information");
            }
        }

        private async Task<bool> CheckRequiredTablesExist()
        {
            try
            {
                // Check if Identity tables exist by trying to query them
                await _identityContext.Users.AnyAsync();
                await _identityContext.Roles.AnyAsync();
                await _identityContext.UserRoles.AnyAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // Result classes for diagnostics
    public class AuthenticationDiagnosticsResult
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string DiagnosticType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public bool UserExists { get; set; }
        public bool DatabaseConnected { get; set; }
        public bool DatabaseTablesExist { get; set; }
        public List<string> Issues { get; set; } = new();
        public UserDiagnosticInfo? UserDetails { get; set; }
        public IdentityConfigurationInfo? IdentityConfiguration { get; set; }
    }

    public class UserDiagnosticInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool LockoutEnabled { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public int AccessFailedCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class IdentityConfigurationInfo
    {
        public bool RequireUniqueEmail { get; set; }
        public bool RequireConfirmedEmail { get; set; }
        public bool RequireConfirmedPhoneNumber { get; set; }
        public int PasswordRequiredLength { get; set; }
        public bool PasswordRequireDigit { get; set; }
        public bool PasswordRequireLowercase { get; set; }
        public bool PasswordRequireUppercase { get; set; }
        public bool PasswordRequireNonAlphanumeric { get; set; }
    }

    public class DatabaseHealthCheckResult
    {
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public bool CanConnect { get; set; }
        public bool TablesExist { get; set; }
        public bool CanWrite { get; set; }
        public int UserCount { get; set; }
        public int RecentRegistrations { get; set; }
        public List<string> WriteErrors { get; set; } = new();
        public string? Error { get; set; }
    }

    public class IdentityConfigurationResult
    {
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public object? UserConfiguration { get; set; }
        public object? PasswordConfiguration { get; set; }
        public object? SignInConfiguration { get; set; }
        public object? LockoutConfiguration { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string? Error { get; set; }
    }
}
