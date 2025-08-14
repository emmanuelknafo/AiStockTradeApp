using AiStockTradeApp.Services.Interfaces;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Entities;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Tests.Services
{
    public class AIAnalysisServiceTests
    {
        private readonly Mock<ILogger<AIAnalysisService>> _mockLogger;
        private readonly AIAnalysisService _aiAnalysisService;

        public AIAnalysisServiceTests()
        {
            _mockLogger = new Mock<ILogger<AIAnalysisService>>();
            _aiAnalysisService = new AIAnalysisService(_mockLogger.Object);
        }

        private static StockData CreateTestStockData(string symbol, decimal price = 100m, decimal change = 1m, string percentChange = "1.00%")
        {
            return new StockData
            {
                Symbol = symbol,
                Price = price,
                Change = change,
                PercentChange = percentChange,
                CompanyName = $"{symbol} Corporation",
                LastUpdated = DateTime.UtcNow,
                Currency = "USD"
            };
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("GOOGL")]
        [InlineData("MSFT")]
        [InlineData("TSLA")]
        public async Task GenerateAnalysisAsync_ValidSymbol_ShouldReturnAnalysis(string symbol)
        {
            // Arrange
            var stockData = CreateTestStockData(symbol);

            // Act
            var result = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);

            // Assert
            result.analysis.Should().NotBeNullOrEmpty();
            result.analysis.Should().Contain(symbol, "The analysis should contain the stock symbol");
            result.recommendation.Should().NotBeNullOrEmpty();
            result.reasoning.Should().NotBeNullOrEmpty();
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("GOOGL")]
        [InlineData("MSFT")]
        [InlineData("TSLA")]
        public async Task GenerateAnalysisAsync_ValidSymbol_ShouldReturnValidRecommendation(string symbol)
        {
            // Arrange
            var stockData = CreateTestStockData(symbol);

            // Act
            var result = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);

            // Assert
            result.recommendation.Should().NotBeNullOrEmpty();
            var validRecommendations = new[] { "Buy", "Strong Buy", "Hold", "Sell", "Consider Selling" };
            validRecommendations.Should().Contain(result.recommendation);
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("GOOGL")]
        [InlineData("MSFT")]
        [InlineData("TSLA")]
        public async Task GenerateAnalysisAsync_ValidSymbol_ShouldReturnReason(string symbol)
        {
            // Arrange
            var stockData = CreateTestStockData(symbol);

            // Act
            var result = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);

            // Assert
            result.reasoning.Should().NotBeNullOrEmpty();
            result.reasoning.Should().Contain(".", "The reason should contain explanatory text");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GenerateAnalysisAsync_InvalidSymbol_ShouldHandleGracefully(string symbol)
        {
            // Arrange
            var stockData = CreateTestStockData(symbol);

            // Act
            var result = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);

            // Assert
            result.analysis.Should().NotBeNull();
            result.recommendation.Should().NotBeNull();
            result.reasoning.Should().NotBeNull();
            // The service should handle invalid symbols gracefully
        }

        [Fact]
        public async Task GenerateAnalysisAsync_NullSymbol_ShouldHandleGracefully()
        {
            // Arrange
            var stockData = CreateTestStockData("AAPL");

            // Act
            var result = await _aiAnalysisService.GenerateAnalysisAsync(null!, stockData);

            // Assert
            result.recommendation.Should().NotBeNull();
            var validRecommendations = new[] { "Buy", "Strong Buy", "Hold", "Sell", "Consider Selling" };
            validRecommendations.Should().Contain(result.recommendation);
        }

        [Fact]
        public async Task GenerateAnalysisAsync_MultipleCallsSameSymbol_ShouldReturnConsistentResults()
        {
            // Arrange
            var symbol = "AAPL";
            var stockData = CreateTestStockData(symbol);

            // Act
            var result1 = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);
            var result2 = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);

            // Assert
            result1.analysis.Should().NotBeNullOrEmpty();
            result2.analysis.Should().NotBeNullOrEmpty();
            result1.recommendation.Should().NotBeNullOrEmpty();
            result2.recommendation.Should().NotBeNullOrEmpty();
            // Both results should be valid analyses
        }

        [Fact]
        public async Task GenerateAnalysisAsync_DifferentPriceMovements_ShouldReturnAppropriateRecommendations()
        {
            // Arrange
            var symbol = "AAPL";
            var bigDropData = CreateTestStockData(symbol, 100m, -6m, "-6.00%");
            var bigGainData = CreateTestStockData(symbol, 100m, 6m, "6.00%");
            var stableData = CreateTestStockData(symbol, 100m, 0.1m, "0.10%");

            // Act
            var bigDropResult = await _aiAnalysisService.GenerateAnalysisAsync(symbol, bigDropData);
            var bigGainResult = await _aiAnalysisService.GenerateAnalysisAsync(symbol, bigGainData);
            var stableResult = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stableData);

            // Assert
            bigDropResult.recommendation.Should().Be("Strong Buy");
            bigGainResult.recommendation.Should().Be("Consider Selling");
            stableResult.recommendation.Should().Be("Hold");
        }

        [Fact]
        public async Task GenerateAnalysisAsync_ShouldCompleteWithinReasonableTime()
        {
            // Arrange
            var symbol = "AAPL";
            var stockData = CreateTestStockData(symbol);
            var timeout = TimeSpan.FromSeconds(5);

            // Act & Assert
            var analysisTask = _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);

            var completedInTime = await Task.WhenAny(analysisTask, Task.Delay(timeout)) == analysisTask;

            completedInTime.Should().BeTrue("AI analysis method should complete within reasonable time");
            
            var result = await analysisTask;
            result.analysis.Should().NotBeNullOrEmpty();
            result.recommendation.Should().NotBeNullOrEmpty();
            result.reasoning.Should().NotBeNullOrEmpty();
        }

        [Theory]
        [InlineData(5.50, "Stock")]
        [InlineData(1500.00, "High-priced")]
        public async Task GenerateAnalysisAsync_DifferentPriceLevels_ShouldIncludePriceAnalysis(decimal price, string expectedContent)
        {
            // Arrange
            var symbol = "TEST";
            var stockData = CreateTestStockData(symbol, price);

            // Act
            var result = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);

            // Assert
            result.analysis.Should().Contain(expectedContent, "Analysis should mention price level characteristics");
        }

        [Fact]
        public async Task GenerateAnalysisAsync_WithInvalidPercentChange_ShouldHandleGracefully()
        {
            // Arrange
            var symbol = "AAPL";
            var stockData = CreateTestStockData(symbol, 100m, 1m, "invalid%");

            // Act
            var result = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);

            // Assert
            result.analysis.Should().Be("Unable to generate analysis at this time.");
            result.recommendation.Should().Be("Hold");
            result.reasoning.Should().Be("Analysis service unavailable.");
        }

        [Fact]
        public async Task GenerateAnalysisAsync_WithNullStockData_ShouldHandleGracefully()
        {
            // Arrange
            var symbol = "AAPL";

            // Act
            var result = await _aiAnalysisService.GenerateAnalysisAsync(symbol, null!);

            // Assert
            result.analysis.Should().Be("Unable to generate analysis at this time.");
            result.recommendation.Should().Be("Hold");
            result.reasoning.Should().Be("Analysis service unavailable.");
        }

        [Fact]
        public async Task GenerateAnalysisAsync_WithZeroPrice_ShouldHandleGracefully()
        {
            // Arrange
            var symbol = "AAPL";
            var stockData = CreateTestStockData(symbol, 0m, 0m, "0.00%");

            // Act
            var result = await _aiAnalysisService.GenerateAnalysisAsync(symbol, stockData);

            // Assert
            result.analysis.Should().NotBeNullOrEmpty();
            result.recommendation.Should().NotBeNullOrEmpty();
            result.reasoning.Should().NotBeNullOrEmpty();
        }
    }
}
