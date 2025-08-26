using AiStockTradeApp.Services.Interfaces;
using AiStockTradeApp.Entities;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Services.Implementations
{
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
                // Parse percent using invariant to handle both 1.23 and -0.96 across cultures
                var percentText = stockData.PercentChange.Replace("%", "").Trim();
                if (!decimal.TryParse(percentText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percentVal))
                {
                    // Try current culture as a backup
                    decimal.TryParse(percentText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out percentVal);
                }

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

                var analysis = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0} closed at ${1:F2}, {2} ${3:F2} ({4}).{5}",
                    symbol,
                    priceVal,
                    changeVal >= 0 ? "up" : "down",
                    Math.Abs(changeVal),
                    stockData.PercentChange,
                    priceAnalysis);

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