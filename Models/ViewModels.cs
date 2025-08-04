namespace ai_stock_trade_app.Models
{
    public class DashboardViewModel
    {
        public List<WatchlistItem> Watchlist { get; set; } = new();
        public PortfolioSummary Portfolio { get; set; } = new();
        public List<PriceAlert> Alerts { get; set; } = new();
        public UserSettings Settings { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
    }

    public class UserSettings
    {
        public bool AutoRefresh { get; set; } = true;
        public int RefreshInterval { get; set; } = 30000; // 30 seconds
        public bool SoundNotifications { get; set; } = false;
        public bool ShowCharts { get; set; } = true;
        public string Theme { get; set; } = "light";
    }

    public class ExportData
    {
        public List<WatchlistItem> Watchlist { get; set; } = new();
        public PortfolioSummary Portfolio { get; set; } = new();
        public DateTime ExportDate { get; set; } = DateTime.UtcNow;
        public string Version { get; set; } = "1.0";
    }
}
