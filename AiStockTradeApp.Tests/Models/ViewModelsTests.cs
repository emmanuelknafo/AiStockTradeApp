using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.ViewModels;

namespace AiStockTradeApp.Tests.Models
{
    public class ViewModelsTests
    {
        [Fact]
        public void DashboardViewModel_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var viewModel = new DashboardViewModel();

            // Assert
            viewModel.Watchlist.Should().NotBeNull();
            viewModel.Watchlist.Should().BeEmpty();
            viewModel.Portfolio.Should().NotBeNull();
            viewModel.Settings.Should().NotBeNull();
            viewModel.Alerts.Should().NotBeNull();
        }

        [Fact]
        public void DashboardViewModel_WithData_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var watchlist = new List<WatchlistItem>
            {
                new WatchlistItem { Symbol = "AAPL", StockData = new StockData { Symbol = "AAPL", Price = 150m } },
                new WatchlistItem { Symbol = "GOOGL", StockData = new StockData { Symbol = "GOOGL", Price = 2500m } }
            };

            var portfolio = new PortfolioSummary
            {
                TotalValue = 2650m,
                TotalChange = 50m,
                StockCount = 2
            };

            var userSettings = new UserSettings
            {
                AutoRefresh = true,
                RefreshInterval = 30000,
                ShowCharts = true
            };

            // Act
            var viewModel = new DashboardViewModel
            {
                Watchlist = watchlist,
                Portfolio = portfolio,
                Settings = userSettings
            };

            // Assert
            viewModel.Watchlist.Should().HaveCount(2);
            viewModel.Portfolio.TotalValue.Should().Be(2650m);
            viewModel.Settings.AutoRefresh.Should().BeTrue();
        }

        [Fact]
        public void PortfolioSummary_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var portfolio = new PortfolioSummary();

            // Assert
            portfolio.TotalValue.Should().Be(0);
            portfolio.TotalChange.Should().Be(0);
            portfolio.TotalChangePercent.Should().Be(0);
            portfolio.StockCount.Should().Be(0);
        }

        [Fact]
        public void PortfolioSummary_WithPositiveChange_ShouldCalculateCorrectly()
        {
            // Arrange & Act
            var portfolio = new PortfolioSummary
            {
                TotalValue = 1000m,
                TotalChange = 50m,
                TotalChangePercent = 5.26m,
                StockCount = 3
            };

            // Assert
            portfolio.TotalValue.Should().Be(1000m);
            portfolio.TotalChange.Should().Be(50m);
            portfolio.TotalChangePercent.Should().Be(5.26m);
            portfolio.StockCount.Should().Be(3);
            portfolio.IsPositive.Should().BeTrue();
            portfolio.ChangeClass.Should().Be("positive");
            portfolio.ChangePrefix.Should().Be("+");
        }

        [Fact]
        public void PortfolioSummary_WithNegativeChange_ShouldCalculateCorrectly()
        {
            // Arrange & Act
            var portfolio = new PortfolioSummary
            {
                TotalValue = 1000m,
                TotalChange = -50m,
                TotalChangePercent = -4.76m,
                StockCount = 3
            };

            // Assert
            portfolio.TotalValue.Should().Be(1000m);
            portfolio.TotalChange.Should().Be(-50m);
            portfolio.TotalChangePercent.Should().Be(-4.76m);
            portfolio.StockCount.Should().Be(3);
            portfolio.IsPositive.Should().BeFalse();
            portfolio.ChangeClass.Should().Be("negative");
            portfolio.ChangePrefix.Should().Be("");
        }

        [Fact]
        public void UserSettings_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var settings = new UserSettings();

            // Assert
            settings.AutoRefresh.Should().BeFalse();
            settings.RefreshInterval.Should().Be(30000);
            settings.Theme.Should().Be("light");
            settings.SoundNotifications.Should().BeFalse();
            settings.ShowCharts.Should().BeTrue();
        }

        [Fact]
        public void UserSettings_WithCustomValues_ShouldSetPropertiesCorrectly()
        {
            // Arrange & Act
            var settings = new UserSettings
            {
                AutoRefresh = true,
                RefreshInterval = 60000,
                Theme = "dark",
                SoundNotifications = true,
                ShowCharts = false
            };

            // Assert
            settings.AutoRefresh.Should().BeTrue();
            settings.RefreshInterval.Should().Be(60000);
            settings.Theme.Should().Be("dark");
            settings.SoundNotifications.Should().BeTrue();
            settings.ShowCharts.Should().BeFalse();
        }

        [Theory]
        [InlineData("light")]
        [InlineData("dark")]
        [InlineData("auto")]
        public void UserSettings_ValidTheme_ShouldSetThemeCorrectly(string theme)
        {
            // Arrange & Act
            var settings = new UserSettings { Theme = theme };

            // Assert
            settings.Theme.Should().Be(theme);
        }

        [Theory]
        [InlineData(5000)]
        [InlineData(10000)]
        [InlineData(30000)]
        [InlineData(60000)]
        [InlineData(300000)]
        public void UserSettings_ValidRefreshInterval_ShouldSetIntervalCorrectly(int interval)
        {
            // Arrange & Act
            var settings = new UserSettings { RefreshInterval = interval };

            // Assert
            settings.RefreshInterval.Should().Be(interval);
        }

        [Fact]
        public void StockQuoteResponse_SuccessResponse_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var stockData = new StockData
            {
                Symbol = "AAPL",
                Price = 150m,
                Change = 5m
            };

            // Act
            var response = new StockQuoteResponse
            {
                Success = true,
                Data = stockData,
                ErrorMessage = null
            };

            // Assert
            response.Success.Should().BeTrue();
            response.Data.Should().NotBeNull();
            response.Data!.Symbol.Should().Be("AAPL");
            response.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void StockQuoteResponse_ErrorResponse_ShouldSetPropertiesCorrectly()
        {
            // Arrange & Act
            var response = new StockQuoteResponse
            {
                Success = false,
                Data = null,
                ErrorMessage = "Stock not found"
            };

            // Assert
            response.Success.Should().BeFalse();
            response.Data.Should().BeNull();
            response.ErrorMessage.Should().Be("Stock not found");
        }

        [Fact]
        public void ExportData_ShouldHaveValidValues()
        {
            // Arrange & Act
            var exportData = new ExportData
            {
                Version = "1.0",
                ExportDate = DateTime.UtcNow,
                Watchlist = new List<WatchlistItem>(),
                Portfolio = new PortfolioSummary()
            };

            // Assert
            exportData.Version.Should().Be("1.0");
            exportData.ExportDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            exportData.Watchlist.Should().NotBeNull();
            exportData.Portfolio.Should().NotBeNull();
        }

        [Fact]
        public void WatchlistItem_ShouldSetPropertiesCorrectly()
        {
            // Arrange & Act
            var item = new WatchlistItem
            {
                Symbol = "AAPL",
                AddedDate = DateTime.UtcNow,
                StockData = new StockData { Symbol = "AAPL", Price = 150m }
            };

            // Assert
            item.Symbol.Should().Be("AAPL");
            item.AddedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            item.StockData.Should().NotBeNull();
            item.StockData!.Symbol.Should().Be("AAPL");
        }

        [Fact]
        public void PriceAlert_ShouldSetPropertiesCorrectly()
        {
            // Arrange & Act
            var alert = new PriceAlert
            {
                Symbol = "AAPL",
                TargetPrice = 160m,
                AlertType = "above",
                CreatedDate = DateTime.UtcNow,
                IsTriggered = false
            };

            // Assert
            alert.Symbol.Should().Be("AAPL");
            alert.TargetPrice.Should().Be(160m);
            alert.AlertType.Should().Be("above");
            alert.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            alert.IsTriggered.Should().BeFalse();
        }

        [Fact]
        public void AddStockRequest_ShouldValidateSymbol()
        {
            // Arrange & Act
            var validRequest = new AddStockRequest { Symbol = "AAPL" };
            var invalidRequest = new AddStockRequest { Symbol = "" };

            // Assert
            validRequest.Symbol.Should().Be("AAPL");
            invalidRequest.Symbol.Should().Be("");
        }

        [Fact]
        public void SetAlertRequest_ShouldSetPropertiesCorrectly()
        {
            // Arrange & Act
            var request = new SetAlertRequest
            {
                Symbol = "AAPL",
                TargetPrice = 160m
            };

            // Assert
            request.Symbol.Should().Be("AAPL");
            request.TargetPrice.Should().Be(160m);
        }
    }
}
