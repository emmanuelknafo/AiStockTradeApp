using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Tests.Models
{
    public class HistoricalPriceTests
    {
        [Fact]
        public void HistoricalPrice_DefaultConstructor_ShouldInitializeProperties()
        {
            // Act
            var historicalPrice = new HistoricalPrice();

            // Assert
            historicalPrice.Id.Should().Be(0);
            historicalPrice.Symbol.Should().Be(string.Empty);
            historicalPrice.Date.Should().Be(default(DateTime));
            historicalPrice.Open.Should().Be(0);
            historicalPrice.High.Should().Be(0);
            historicalPrice.Low.Should().Be(0);
            historicalPrice.Close.Should().Be(0);
            historicalPrice.Volume.Should().Be(0);
        }

        [Fact]
        public void HistoricalPrice_SetProperties_ShouldRetainValues()
        {
            // Arrange
            const string expectedSymbol = "AAPL";
            var expectedDate = DateTime.Today.AddDays(-1);
            const decimal expectedOpen = 150.25m;
            const decimal expectedHigh = 152.50m;
            const decimal expectedLow = 149.75m;
            const decimal expectedClose = 151.80m;
            const long expectedVolume = 25000000L;

            // Act
            var historicalPrice = new HistoricalPrice
            {
                Id = 1,
                Symbol = expectedSymbol,
                Date = expectedDate,
                Open = expectedOpen,
                High = expectedHigh,
                Low = expectedLow,
                Close = expectedClose,
                Volume = expectedVolume
            };

            // Assert
            historicalPrice.Id.Should().Be(1);
            historicalPrice.Symbol.Should().Be(expectedSymbol);
            historicalPrice.Date.Should().Be(expectedDate);
            historicalPrice.Open.Should().Be(expectedOpen);
            historicalPrice.High.Should().Be(expectedHigh);
            historicalPrice.Low.Should().Be(expectedLow);
            historicalPrice.Close.Should().Be(expectedClose);
            historicalPrice.Volume.Should().Be(expectedVolume);
        }

        [Theory]
        [InlineData("AAPL", "2023-01-15", 150.25, 152.50, 149.75, 151.80, 25000000)]
        [InlineData("MSFT", "2023-02-20", 250.00, 255.75, 248.50, 254.30, 15000000)]
        [InlineData("GOOGL", "2023-03-10", 2800.00, 2825.50, 2790.25, 2810.75, 1200000)]
        public void HistoricalPrice_WithValidData_ShouldSetAllProperties(
            string symbol, string dateStr, decimal open, decimal high, decimal low, decimal close, long volume)
        {
            // Arrange
            var date = DateTime.Parse(dateStr);

            // Act
            var historicalPrice = new HistoricalPrice
            {
                Symbol = symbol,
                Date = date,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            };

            // Assert
            historicalPrice.Symbol.Should().Be(symbol);
            historicalPrice.Date.Should().Be(date);
            historicalPrice.Open.Should().Be(open);
            historicalPrice.High.Should().Be(high);
            historicalPrice.Low.Should().Be(low);
            historicalPrice.Close.Should().Be(close);
            historicalPrice.Volume.Should().Be(volume);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(long.MinValue)]
        public void HistoricalPrice_WithInvalidVolume_ShouldAcceptValue(long volume)
        {
            // Act
            var historicalPrice = new HistoricalPrice { Volume = volume };

            // Assert
            historicalPrice.Volume.Should().Be(volume);
        }

        [Fact]
        public void HistoricalPrice_WithEdgeCasePrices_ShouldAcceptValues()
        {
            // Test various edge case prices
            var testPrices = new[] { 0m, -10.50m, decimal.MaxValue, decimal.MinValue };

            foreach (var price in testPrices)
            {
                // Act
                var historicalPrice = new HistoricalPrice
                {
                    Open = price,
                    High = price,
                    Low = price,
                    Close = price
                };

                // Assert
                historicalPrice.Open.Should().Be(price);
                historicalPrice.High.Should().Be(price);
                historicalPrice.Low.Should().Be(price);
                historicalPrice.Close.Should().Be(price);
            }
        }

        [Fact]
        public void HistoricalPrice_WithNullSymbol_ShouldHandleGracefully()
        {
            // Act
            var historicalPrice = new HistoricalPrice();
            
            // Should not throw when accessing Symbol property even if not explicitly set
            // Default should be empty string based on the model initialization

            // Assert
            historicalPrice.Symbol.Should().NotBeNull();
        }

        [Fact]
        public void HistoricalPrice_WithFutureDate_ShouldAcceptValue()
        {
            // Arrange
            var futureDate = DateTime.Today.AddDays(30);

            // Act
            var historicalPrice = new HistoricalPrice { Date = futureDate };

            // Assert
            historicalPrice.Date.Should().Be(futureDate);
        }

        [Fact]
        public void HistoricalPrice_WithMinMaxDateTime_ShouldAcceptValues()
        {
            // Act
            var minPrice = new HistoricalPrice { Date = DateTime.MinValue };
            var maxPrice = new HistoricalPrice { Date = DateTime.MaxValue };

            // Assert
            minPrice.Date.Should().Be(DateTime.MinValue);
            maxPrice.Date.Should().Be(DateTime.MaxValue);
        }

        [Fact]
        public void HistoricalPrice_EqualityComparison_ShouldWorkWithSameData()
        {
            // Arrange
            var price1 = new HistoricalPrice
            {
                Id = 1,
                Symbol = "AAPL",
                Date = DateTime.Today,
                Close = 150.00m
            };

            var price2 = new HistoricalPrice
            {
                Id = 1,
                Symbol = "AAPL",
                Date = DateTime.Today,
                Close = 150.00m
            };

            // Assert
            // Note: This tests reference equality since HistoricalPrice likely doesn't override Equals
            price1.Should().NotBeSameAs(price2);
            price1.Id.Should().Be(price2.Id);
            price1.Symbol.Should().Be(price2.Symbol);
            price1.Date.Should().Be(price2.Date);
            price1.Close.Should().Be(price2.Close);
        }
    }
}
