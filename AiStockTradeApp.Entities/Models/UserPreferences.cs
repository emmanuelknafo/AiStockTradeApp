using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiStockTradeApp.Entities.Models
{
    /// <summary>
    /// User-specific watchlist and portfolio items
    /// Replaces session-based watchlist with persistent user data
    /// </summary>
    public class UserWatchlistItem
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Reference to the user who owns this watchlist item
        /// </summary>
        [Required]
        [StringLength(450)] // Standard length for AspNetCore Identity user IDs
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Stock symbol (e.g., "AAPL", "MSFT")
        /// </summary>
        [Required]
        [StringLength(10)]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// User-defined alias/nickname for the stock
        /// </summary>
        [StringLength(100)]
        public string? Alias { get; set; }

        /// <summary>
        /// When this item was added to the watchlist
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Order/priority for display in the watchlist
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Whether to show price alerts for this stock
        /// </summary>
        public bool EnableAlerts { get; set; } = true;

        /// <summary>
        /// Target price for price alerts (optional)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TargetPrice { get; set; }

        /// <summary>
        /// Stop loss price for alerts (optional)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? StopLossPrice { get; set; }

        /// <summary>
        /// Navigation property to ApplicationUser
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }
    }

    /// <summary>
    /// User price alerts configuration
    /// </summary>
    public class UserPriceAlert
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Reference to the user who owns this alert
        /// </summary>
        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Stock symbol for the alert
        /// </summary>
        [Required]
        [StringLength(10)]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Alert type (e.g., "above", "below", "change_percent")
        /// </summary>
        [Required]
        [StringLength(20)]
        public string AlertType { get; set; } = string.Empty;

        /// <summary>
        /// Target value for the alert
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TargetValue { get; set; }

        /// <summary>
        /// Whether this alert is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// When this alert was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this alert was last triggered (if ever)
        /// </summary>
        public DateTime? LastTriggeredAt { get; set; }

        /// <summary>
        /// User-defined message for the alert
        /// </summary>
        [StringLength(500)]
        public string? Message { get; set; }

        /// <summary>
        /// Navigation property to ApplicationUser
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }
    }
}
