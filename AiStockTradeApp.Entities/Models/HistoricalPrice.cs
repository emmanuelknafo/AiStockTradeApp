using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiStockTradeApp.Entities
{
    public class HistoricalPrice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Symbol { get; set; } = string.Empty;

        [Column(TypeName = "date")]
        public DateTime Date { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Open { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal High { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Low { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Close { get; set; }

        public long Volume { get; set; }

        [StringLength(100)]
        public string? Source { get; set; }

        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    }
}
