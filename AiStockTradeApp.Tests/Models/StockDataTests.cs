using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Tests.Models
{
    public class StockDataTests
    {
        [Fact]
        public void StockData_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var stockData = new StockData();

            // Assert
            stockData.Symbol.Should().Be(string.Empty);
            stockData.Price.Should().Be(0);
            stockData.Change.Should().Be(0);
            stockData.PercentChange.Should().Be(string.Empty);
            stockData.CompanyName.Should().Be(string.Empty);
            stockData.Currency.Should().Be("USD");
        }

        [Theory]
        [InlineData(10.5, true)]
        [InlineData(0, true)]
        [InlineData(-5.2, false)]
        public void IsPositive_ShouldReturnCorrectValue(decimal change, bool expectedIsPositive)
        {
            // Arrange
            var stockData = new StockData { Change = change };

            // Act & Assert
            stockData.IsPositive.Should().Be(expectedIsPositive);
        }

        [Theory]
        [InlineData(10.5, "positive")]
        [InlineData(0, "positive")]
        [InlineData(-5.2, "negative")]
        public void ChangeClass_ShouldReturnCorrectCssClass(decimal change, string expectedClass)
        {
            // Arrange
            var stockData = new StockData { Change = change };

            // Act & Assert
            stockData.ChangeClass.Should().Be(expectedClass);
        }

        [Theory]
        [InlineData(10.5, "+")]
        [InlineData(0, "+")]
        [InlineData(-5.2, "")]
        public void ChangePrefix_ShouldReturnCorrectPrefix(decimal change, string expectedPrefix)
        {
            // Arrange
            var stockData = new StockData { Change = change };

            // Act & Assert
            stockData.ChangePrefix.Should().Be(expectedPrefix);
        }

        [Fact]
        public void StockData_WithCompleteData_ShouldSetAllProperties()
        {
            // Arrange
            var symbol = "AAPL";
            var price = 150.25m;
            var change = 5.75m;
            var percentChange = "+3.98%";
            var companyName = "Apple Inc.";
            var lastUpdated = DateTime.Now;
            var aiAnalysis = "Strong buy signal";
            var recommendation = "BUY";
            var recommendationReason = "Strong growth potential";

            // Act
            var stockData = new StockData
            {
                Symbol = symbol,
                Price = price,
                Change = change,
                PercentChange = percentChange,
                CompanyName = companyName,
                LastUpdated = lastUpdated,
                AIAnalysis = aiAnalysis,
                Recommendation = recommendation,
                RecommendationReason = recommendationReason
            };

            // Assert
            stockData.Symbol.Should().Be(symbol);
            stockData.Price.Should().Be(price);
            stockData.Change.Should().Be(change);
            stockData.PercentChange.Should().Be(percentChange);
            stockData.CompanyName.Should().Be(companyName);
            stockData.LastUpdated.Should().Be(lastUpdated);
            stockData.AIAnalysis.Should().Be(aiAnalysis);
            stockData.Recommendation.Should().Be(recommendation);
            stockData.RecommendationReason.Should().Be(recommendationReason);
            stockData.Currency.Should().Be("USD");
            stockData.IsPositive.Should().BeTrue();
            stockData.ChangeClass.Should().Be("positive");
            stockData.ChangePrefix.Should().Be("+");
        }
    }

    public class ChartDataPointTests
    {
        [Fact]
        public void ChartDataPoint_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var date = DateTime.Now.Date;
            var price = 125.50m;
            var volume = 1000000;

            // Act
            var chartPoint = new ChartDataPoint
            {
                Date = date,
                Price = price,
                Volume = volume
            };

            // Assert
            chartPoint.Date.Should().Be(date);
            chartPoint.Price.Should().Be(price);
            chartPoint.Volume.Should().Be(volume);
        }
    }
}
