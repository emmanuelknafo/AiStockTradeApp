using ai_stock_trade_app.Models;

namespace ai_stock_trade_app.Services
{
    public interface IAIAnalysisService
    {
        Task<(string analysis, string recommendation, string reasoning)> GenerateAnalysisAsync(string symbol, StockData stockData);
    }

    public class AIAnalysisService : IAIAnalysisService
    {
        private readonly ILogger<AIAnalysisService> _logger;

        public AIAnalysisService(ILogger<AIAnalysisService> logger)
        {
            _logger = logger;
        }

        public Task<(string analysis, string recommendation, string reasoning)> GenerateAnalysisAsync(string symbol, StockData stockData)
        {
            try
            {
                var changeVal = stockData.Change;
                var priceVal = stockData.Price;
                var percentVal = decimal.Parse(stockData.PercentChange.Replace("%", ""));

                string recommendation = "Hold";
                string reasoning = "Stable price movement; monitor for trends.";

                // More sophisticated analysis based on actual data (same logic as JS app)
                if (percentVal < -5)
                {
                    recommendation = "Strong Buy";
                    reasoning = "Significant price drop may present buying opportunity.";
                }
                else if (percentVal < -2)
                {
                    recommendation = "Buy";
                    reasoning = "Price dipped recently; potential value opportunity.";
                }
                else if (percentVal > 5)
                {
                    recommendation = "Consider Selling";
                    reasoning = "Strong price increase; consider taking profits.";
                }
                else if (percentVal > 2)
                {
                    recommendation = "Sell";
                    reasoning = "Price increased significantly; good profit opportunity.";
                }
                else if (Math.Abs(percentVal) < 0.5m)
                {
                    recommendation = "Hold";
                    reasoning = "Minimal price movement; wait for clearer signals.";
                }

                // Add price level analysis
                string priceAnalysis = "";
                if (priceVal < 10)
                {
                    priceAnalysis = " Stock is in penny stock territory - high risk/reward.";
                }
                else if (priceVal > 1000)
                {
                    priceAnalysis = " High-priced stock - consider fractional shares.";
                }

                var analysis = $"{symbol} closed at ${priceVal:F2}, {(changeVal >= 0 ? "up" : "down")} ${Math.Abs(changeVal):F2} ({stockData.PercentChange}).{priceAnalysis}";

                return Task.FromResult((analysis, recommendation, reasoning));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI analysis for {Symbol}", symbol);
                return Task.FromResult((
                    "Unable to generate analysis at this time.",
                    "Hold",
                    "Analysis service unavailable."
                ));
            }
        }
    }
}
