using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using AiStockTradeApp.Services.Implementations;
using System.Text.Json;

namespace AiStockTradeApp.Controllers
{
    /// <summary>
    /// Controller for authentication diagnostics - should be removed or secured in production
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IAuthenticationDiagnosticsService _diagnosticsService;
        private readonly ILogger<DiagnosticsController> _logger;
        private readonly IConfiguration _configuration;

        public DiagnosticsController(
            IAuthenticationDiagnosticsService diagnosticsService,
            ILogger<DiagnosticsController> logger,
            IConfiguration configuration)
        {
            _diagnosticsService = diagnosticsService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Health check endpoint for basic service availability
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            
            // Only allow in development or specific environments
            if (environment != "Development" && environment != "Testing")
            {
                return NotFound();
            }

            return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow, Environment = environment });
        }

        /// <summary>
        /// Diagnose registration issues for a specific email
        /// </summary>
        [HttpPost("registration/{email}")]
        public async Task<IActionResult> DiagnoseRegistration(string email, [FromQuery] string? correlationId = null)
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            
            // Only allow in development or specific environments
            if (environment != "Development" && environment != "Testing")
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Email is required");
            }

            try
            {
                var result = await _diagnosticsService.DiagnoseRegistrationIssueAsync(email, correlationId);
                
                _logger.LogInformation("Registration diagnostics completed for {Email} with correlation {CorrelationId}: {Success}", 
                    email, result.CorrelationId, result.Success);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration diagnostics for {Email}", email);
                return StatusCode(500, new { Error = "Internal server error during diagnostics", Message = ex.Message });
            }
        }

        /// <summary>
        /// Diagnose login issues for a specific email
        /// </summary>
        [HttpPost("login/{email}")]
        public async Task<IActionResult> DiagnoseLogin(string email, [FromQuery] string? correlationId = null)
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            
            // Only allow in development or specific environments
            if (environment != "Development" && environment != "Testing")
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Email is required");
            }

            try
            {
                var result = await _diagnosticsService.DiagnoseLoginIssueAsync(email, correlationId);
                
                _logger.LogInformation("Login diagnostics completed for {Email} with correlation {CorrelationId}: {Success}", 
                    email, result.CorrelationId, result.Success);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login diagnostics for {Email}", email);
                return StatusCode(500, new { Error = "Internal server error during diagnostics", Message = ex.Message });
            }
        }

        /// <summary>
        /// Check database health and connectivity
        /// </summary>
        [HttpGet("database")]
        public async Task<IActionResult> CheckDatabaseHealth()
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            
            // Only allow in development or specific environments
            if (environment != "Development" && environment != "Testing")
            {
                return NotFound();
            }

            try
            {
                var result = await _diagnosticsService.CheckDatabaseHealthAsync();
                
                _logger.LogInformation("Database health check completed: {Success}", result.Success);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database health check");
                return StatusCode(500, new { Error = "Internal server error during database check", Message = ex.Message });
            }
        }

        /// <summary>
        /// Validate Identity configuration
        /// </summary>
        [HttpGet("configuration")]
        public async Task<IActionResult> ValidateConfiguration()
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            
            // Only allow in development or specific environments
            if (environment != "Development" && environment != "Testing")
            {
                return NotFound();
            }

            try
            {
                var result = await _diagnosticsService.ValidateIdentityConfigurationAsync();
                
                _logger.LogInformation("Identity configuration validation completed: {Success}", result.Success);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during configuration validation");
                return StatusCode(500, new { Error = "Internal server error during configuration validation", Message = ex.Message });
            }
        }

        /// <summary>
        /// Log system information for troubleshooting
        /// </summary>
        [HttpPost("system-info")]
        public async Task<IActionResult> LogSystemInformation()
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            
            // Only allow in development or specific environments
            if (environment != "Development" && environment != "Testing")
            {
                return NotFound();
            }

            try
            {
                await _diagnosticsService.LogSystemInformationAsync();
                
                _logger.LogInformation("System information logged successfully");

                return Ok(new { Message = "System information logged successfully", Timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging system information");
                return StatusCode(500, new { Error = "Internal server error logging system information", Message = ex.Message });
            }
        }

        /// <summary>
        /// Comprehensive diagnostics report
        /// </summary>
        [HttpGet("report")]
        public async Task<IActionResult> GenerateReport([FromQuery] string? email = null)
        {
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            
            // Only allow in development or specific environments
            if (environment != "Development" && environment != "Testing")
            {
                return NotFound();
            }

            try
            {
                var report = new
                {
                    Timestamp = DateTime.UtcNow,
                    Environment = environment,
                    DatabaseHealth = await _diagnosticsService.CheckDatabaseHealthAsync(),
                    IdentityConfiguration = await _diagnosticsService.ValidateIdentityConfigurationAsync(),
                    UserDiagnostics = !string.IsNullOrEmpty(email) 
                        ? new
                        {
                            Registration = await _diagnosticsService.DiagnoseRegistrationIssueAsync(email),
                            Login = await _diagnosticsService.DiagnoseLoginIssueAsync(email)
                        }
                        : null
                };

                // Log system information as well
                await _diagnosticsService.LogSystemInformationAsync();

                _logger.LogInformation("Comprehensive diagnostics report generated for {Email}", email ?? "system-only");

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating diagnostics report");
                return StatusCode(500, new { Error = "Internal server error generating report", Message = ex.Message });
            }
        }
    }
}
