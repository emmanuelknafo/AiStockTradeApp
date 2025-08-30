using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AiStockTradeApp.Entities.Models
{
    /// <summary>
    /// Application user model extending IdentityUser for authentication and authorization
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// User's first name
        /// </summary>
        [StringLength(50)]
        public string? FirstName { get; set; }

        /// <summary>
        /// User's last name
        /// </summary>
        [StringLength(50)]
        public string? LastName { get; set; }

        /// <summary>
        /// Date when the user account was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the user last logged in
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// User's preferred language/culture (e.g., "en", "fr")
        /// </summary>
        [StringLength(10)]
        public string PreferredCulture { get; set; } = "en";

        /// <summary>
        /// Whether the user wants to receive price alerts
        /// </summary>
        public bool EnablePriceAlerts { get; set; } = true;

        /// <summary>
        /// Full name computed property
        /// </summary>
        public string FullName => $"{FirstName} {LastName}".Trim();

        /// <summary>
        /// Display name that falls back to email if name is not provided
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(FullName) ? FullName : Email ?? UserName ?? "User";
    }
}
