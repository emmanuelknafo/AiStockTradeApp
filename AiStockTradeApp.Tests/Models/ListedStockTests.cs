using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Tests.Models
{
    public class ListedStockTests
    {
        [Fact]
        public void ListedStock_DefaultConstructor_ShouldInitializeWithDefaults()
        {
            // Act
            var stock = new ListedStock();

            // Assert
            stock.Id.Should().Be(0);
            stock.Symbol.Should().Be(string.Empty);
            stock.Name.Should().Be(string.Empty);
            stock.LastSale.Should().Be(0);
            stock.NetChange.Should().Be(0);
            stock.PercentChange.Should().Be(0);
            stock.MarketCap.Should().Be(0);
            stock.Country.Should().BeNull();
            stock.IpoYear.Should().BeNull();
            stock.Volume.Should().Be(0);
            stock.Sector.Should().BeNull();
            stock.Industry.Should().BeNull();
            stock.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void ListedStock_SetAllProperties_ShouldRetainValues()
        {
            // Arrange
            var expectedDate = DateTime.UtcNow.AddHours(-1);

            // Act
            var stock = new ListedStock
            {
                Id = 1,
                Symbol = "AAPL",
                Name = "Apple Inc.",
                LastSale = 150.25m,
                NetChange = 2.50m,
                PercentChange = 1.69m,
                MarketCap = 2500000000.00m,
                Country = "United States",
                IpoYear = 1980,
                Volume = 75000000L,
                Sector = "Technology",
                Industry = "Consumer Electronics",
                UpdatedAt = expectedDate
            };

            // Assert
            stock.Id.Should().Be(1);
            stock.Symbol.Should().Be("AAPL");
            stock.Name.Should().Be("Apple Inc.");
            stock.LastSale.Should().Be(150.25m);
            stock.NetChange.Should().Be(2.50m);
            stock.PercentChange.Should().Be(1.69m);
            stock.MarketCap.Should().Be(2500000000.00m);
            stock.Country.Should().Be("United States");
            stock.IpoYear.Should().Be(1980);
            stock.Volume.Should().Be(75000000L);
            stock.Sector.Should().Be("Technology");
            stock.Industry.Should().Be("Consumer Electronics");
            stock.UpdatedAt.Should().Be(expectedDate);
        }

        [Theory]
        [InlineData("AAPL", "Apple Inc.", "Technology", "Consumer Electronics")]
        [InlineData("MSFT", "Microsoft Corporation", "Technology", "Software")]
        [InlineData("JPM", "JPMorgan Chase & Co.", "Financial Services", "Banks")]
        [InlineData("JNJ", "Johnson & Johnson", "Healthcare", "Pharmaceuticals")]
        public void ListedStock_WithVariousCompanies_ShouldSetPropertiesCorrectly(
            string symbol, string name, string sector, string industry)
        {
            // Act
            var stock = new ListedStock
            {
                Symbol = symbol,
                Name = name,
                Sector = sector,
                Industry = industry
            };

            // Assert
            stock.Symbol.Should().Be(symbol);
            stock.Name.Should().Be(name);
            stock.Sector.Should().Be(sector);
            stock.Industry.Should().Be(industry);
        }

        [Fact]
        public void ListedStock_WithVariousPrices_ShouldAcceptValues()
        {
            // Test various price edge cases
            var testPrices = new[] { 0m, -5.25m, 1000.50m, decimal.MaxValue, decimal.MinValue };

            foreach (var price in testPrices)
            {
                // Act
                var stock = new ListedStock { LastSale = price };

                // Assert
                stock.LastSale.Should().Be(price);
            }
        }

        [Theory]
        [InlineData(-10.50)]
        [InlineData(0)]
        [InlineData(5.75)]
        [InlineData(100.25)]
        public void ListedStock_WithVariousNetChanges_ShouldAcceptValues(decimal netChange)
        {
            // Act
            var stock = new ListedStock { NetChange = netChange };

            // Assert
            stock.NetChange.Should().Be(netChange);
        }

        [Theory]
        [InlineData(-15.5)]
        [InlineData(0)]
        [InlineData(2.78)]
        [InlineData(25.0)]
        public void ListedStock_WithVariousPercentChanges_ShouldAcceptValues(decimal percentChange)
        {
            // Act
            var stock = new ListedStock { PercentChange = percentChange };

            // Assert
            stock.PercentChange.Should().Be(percentChange);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000000.50)]
        [InlineData(2500000000000.75)] // Large market cap
        public void ListedStock_WithVariousMarketCaps_ShouldAcceptValues(decimal marketCap)
        {
            // Act
            var stock = new ListedStock { MarketCap = marketCap };

            // Assert
            stock.MarketCap.Should().Be(marketCap);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("United States")]
        [InlineData("Canada")]
        [InlineData("United Kingdom")]
        public void ListedStock_WithVariousCountries_ShouldAcceptValues(string? country)
        {
            // Act
            var stock = new ListedStock { Country = country };

            // Assert
            stock.Country.Should().Be(country);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(1980)]
        [InlineData(2023)]
        [InlineData(1900)]
        public void ListedStock_WithVariousIpoYears_ShouldAcceptValues(int? ipoYear)
        {
            // Act
            var stock = new ListedStock { IpoYear = ipoYear };

            // Assert
            stock.IpoYear.Should().Be(ipoYear);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000000)]
        [InlineData(100000000L)]
        [InlineData(long.MaxValue)]
        public void ListedStock_WithVariousVolumes_ShouldAcceptValues(long volume)
        {
            // Act
            var stock = new ListedStock { Volume = volume };

            // Assert
            stock.Volume.Should().Be(volume);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("Technology", null)]
        [InlineData(null, "Software")]
        [InlineData("Technology", "Software")]
        [InlineData("Financial Services", "Investment Banking")]
        public void ListedStock_WithVariousSectorIndustry_ShouldAcceptValues(string? sector, string? industry)
        {
            // Act
            var stock = new ListedStock 
            { 
                Sector = sector, 
                Industry = industry 
            };

            // Assert
            stock.Sector.Should().Be(sector);
            stock.Industry.Should().Be(industry);
        }

        [Fact]
        public void ListedStock_UpdatedAt_ShouldBeUtcTime()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;

            // Act
            var stock = new ListedStock();
            var afterCreation = DateTime.UtcNow;

            // Assert
            stock.UpdatedAt.Should().BeOnOrAfter(beforeCreation);
            stock.UpdatedAt.Should().BeOnOrBefore(afterCreation);
            stock.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public void ListedStock_WithMaxLengthStrings_ShouldAcceptValues()
        {
            // Arrange
            var symbol = new string('A', 10); // Max length for Symbol
            var name = new string('B', 500); // Max length for Name
            var country = new string('C', 100); // Max length for Country
            var sector = new string('D', 100); // Max length for Sector
            var industry = new string('E', 200); // Max length for Industry

            // Act
            var stock = new ListedStock
            {
                Symbol = symbol,
                Name = name,
                Country = country,
                Sector = sector,
                Industry = industry
            };

            // Assert
            stock.Symbol.Should().Be(symbol);
            stock.Name.Should().Be(name);
            stock.Country.Should().Be(country);
            stock.Sector.Should().Be(sector);
            stock.Industry.Should().Be(industry);
        }

        [Fact]
        public void ListedStock_EqualityComparison_ShouldWorkWithSameData()
        {
            // Arrange
            var stock1 = new ListedStock
            {
                Id = 1,
                Symbol = "AAPL",
                Name = "Apple Inc.",
                LastSale = 150.00m
            };

            var stock2 = new ListedStock
            {
                Id = 1,
                Symbol = "AAPL",
                Name = "Apple Inc.",
                LastSale = 150.00m
            };

            // Assert
            // Note: This tests reference equality since ListedStock likely doesn't override Equals
            stock1.Should().NotBeSameAs(stock2);
            stock1.Id.Should().Be(stock2.Id);
            stock1.Symbol.Should().Be(stock2.Symbol);
            stock1.Name.Should().Be(stock2.Name);
            stock1.LastSale.Should().Be(stock2.LastSale);
        }
    }
}
