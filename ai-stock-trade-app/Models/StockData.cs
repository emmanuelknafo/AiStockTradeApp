using System.ComponentModel.DataAnnotations;

namespace ai_stock_trade_app.Models
{
    public class StockData
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Change { get; set; }
        public string PercentChange { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public string Currency { get; set; } = "USD";
        
        // AI-generated data
        public string? AIAnalysis { get; set; }
        public string? Recommendation { get; set; }
        public string? RecommendationReason { get; set; }
        
        // Chart data
        public List<ChartDataPoint>? ChartData { get; set; }
        
        // Calculated properties
        public bool IsPositive => Change >= 0;
        public string ChangeClass => IsPositive ? "positive" : "negative";
        public string ChangePrefix => IsPositive ? "+" : "";
    }

    public class ChartDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
    }

    public class StockQuoteResponse
    {
        public bool Success { get; set; }
        public StockData? Data { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class WatchlistItem
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime AddedDate { get; set; }
        public StockData? StockData { get; set; }
    }

    public class PortfolioSummary
    {
        public decimal TotalValue { get; set; }
        public decimal TotalChange { get; set; }
        public decimal TotalChangePercent { get; set; }
        public int StockCount { get; set; }
        public DateTime LastUpdated { get; set; }
        
        public bool IsPositive => TotalChange >= 0;
        public string ChangeClass => IsPositive ? "positive" : "negative";
        public string ChangePrefix => IsPositive ? "+" : "";
    }

    public class PriceAlert
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal TargetPrice { get; set; }
        public string AlertType { get; set; } = string.Empty; // "above" or "below"
        public DateTime CreatedDate { get; set; }
        public bool IsTriggered { get; set; }
    }

    public class AddStockRequest
    {
        [Required]
        [StringLength(10, MinimumLength = 1)]
        public string Symbol { get; set; } = string.Empty;
    }

    public class SetAlertRequest
    {
        [Required]
        public string Symbol { get; set; } = string.Empty;
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal TargetPrice { get; set; }
    }
}
