using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiStockTradeApp.Entities
{
    public class ListedStock
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal LastSale { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal NetChange { get; set; }

        // Store percent as a numeric value (e.g., 0.278 means 0.278%)
        [Column(TypeName = "decimal(18,4)")]
        public decimal PercentChange { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MarketCap { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        public int? IpoYear { get; set; }

        public long Volume { get; set; }

        [StringLength(100)]
        public string? Sector { get; set; }

        [StringLength(200)]
        public string? Industry { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
