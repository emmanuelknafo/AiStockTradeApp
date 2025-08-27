using AiStockTradeApp.Entities;
using AiStockTradeApp.Entities.ViewModels;

namespace AiStockTradeApp.Tests.Models
{
    public class AdditionalViewModelsTests
    {
        [Fact]
        public void DashboardViewModel_DefaultConstructor_ShouldInitializeCollections()
        {
            // Act
            var viewModel = new DashboardViewModel();

            // Assert
            viewModel.Watchlist.Should().NotBeNull();
            viewModel.Watchlist.Should().BeEmpty();
            viewModel.Portfolio.Should().NotBeNull();
            viewModel.Alerts.Should().NotBeNull();
            viewModel.Alerts.Should().BeEmpty();
            viewModel.Settings.Should().NotBeNull();
            viewModel.ErrorMessage.Should().BeNull();
            viewModel.SuccessMessage.Should().BeNull();
        }

        [Fact]
        public void DashboardViewModel_WithData_ShouldRetainValues()
        {
            // Arrange
            var watchlistItems = new List<WatchlistItem>
            {
                new() { Symbol = "AAPL", AddedDate = DateTime.Today },
                new() { Symbol = "MSFT", AddedDate = DateTime.Today.AddDays(-1) }
            };
            var portfolio = new PortfolioSummary { TotalValue = 1000m };
            var alerts = new List<PriceAlert>
            {
                new() { Symbol = "AAPL", TargetPrice = 150m }
            };
            var settings = new UserSettings { AutoRefresh = true };

            // Act
            var viewModel = new DashboardViewModel
            {
                Watchlist = watchlistItems,
                Portfolio = portfolio,
                Alerts = alerts,
                Settings = settings,
                ErrorMessage = "Test Error",
                SuccessMessage = "Test Success"
            };

            // Assert
            viewModel.Watchlist.Should().BeSameAs(watchlistItems);
            viewModel.Portfolio.Should().BeSameAs(portfolio);
            viewModel.Alerts.Should().BeSameAs(alerts);
            viewModel.Settings.Should().BeSameAs(settings);
            viewModel.ErrorMessage.Should().Be("Test Error");
            viewModel.SuccessMessage.Should().Be("Test Success");
        }

        [Fact]
        public void UserSettings_DefaultConstructor_ShouldSetDefaults()
        {
            // Act
            var settings = new UserSettings();

            // Assert
            settings.AutoRefresh.Should().BeFalse();
            settings.RefreshInterval.Should().Be(30000);
            settings.SoundNotifications.Should().BeFalse();
            settings.ShowCharts.Should().BeTrue();
            settings.Theme.Should().Be("light");
        }

        [Theory]
        [InlineData(true, 15000, true, false, "dark")]
        [InlineData(false, 60000, false, true, "auto")]
        [InlineData(true, 5000, true, true, "custom")]
        public void UserSettings_WithCustomValues_ShouldRetainValues(
            bool autoRefresh, int refreshInterval, bool soundNotifications, bool showCharts, string theme)
        {
            // Act
            var settings = new UserSettings
            {
                AutoRefresh = autoRefresh,
                RefreshInterval = refreshInterval,
                SoundNotifications = soundNotifications,
                ShowCharts = showCharts,
                Theme = theme
            };

            // Assert
            settings.AutoRefresh.Should().Be(autoRefresh);
            settings.RefreshInterval.Should().Be(refreshInterval);
            settings.SoundNotifications.Should().Be(soundNotifications);
            settings.ShowCharts.Should().Be(showCharts);
            settings.Theme.Should().Be(theme);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1000)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void UserSettings_WithEdgeCaseRefreshInterval_ShouldAcceptValue(int refreshInterval)
        {
            // Act
            var settings = new UserSettings { RefreshInterval = refreshInterval };

            // Assert
            settings.RefreshInterval.Should().Be(refreshInterval);
        }

        [Fact]
        public void ExportData_DefaultConstructor_ShouldInitializeDefaults()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;

            // Act
            var exportData = new ExportData();
            var afterCreation = DateTime.UtcNow;

            // Assert
            exportData.Watchlist.Should().NotBeNull();
            exportData.Watchlist.Should().BeEmpty();
            exportData.Portfolio.Should().NotBeNull();
            exportData.ExportDate.Should().BeOnOrAfter(beforeCreation);
            exportData.ExportDate.Should().BeOnOrBefore(afterCreation);
            exportData.ExportDate.Kind.Should().Be(DateTimeKind.Utc);
            exportData.Version.Should().Be("1.0");
        }

        [Fact]
        public void ExportData_WithCustomData_ShouldRetainValues()
        {
            // Arrange
            var watchlist = new List<WatchlistItem>
            {
                new() { Symbol = "AAPL", AddedDate = DateTime.Today }
            };
            var portfolio = new PortfolioSummary { TotalValue = 5000m };
            var exportDate = DateTime.UtcNow.AddDays(-1);
            const string version = "2.0";

            // Act
            var exportData = new ExportData
            {
                Watchlist = watchlist,
                Portfolio = portfolio,
                ExportDate = exportDate,
                Version = version
            };

            // Assert
            exportData.Watchlist.Should().BeSameAs(watchlist);
            exportData.Portfolio.Should().BeSameAs(portfolio);
            exportData.ExportDate.Should().Be(exportDate);
            exportData.Version.Should().Be(version);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("12345")]
        [InlineData("test-request-id")]
        public void ErrorViewModel_WithVariousRequestIds_ShouldSetCorrectly(string? requestId)
        {
            // Act
            var errorViewModel = new ErrorViewModel { RequestId = requestId };

            // Assert
            errorViewModel.RequestId.Should().Be(requestId);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", true)] // ShowRequestId uses !string.IsNullOrEmpty which considers whitespace as valid
        [InlineData("12345", true)]
        [InlineData("test-request-id", true)]
        public void ErrorViewModel_ShowRequestId_ShouldReturnCorrectValue(string? requestId, bool expectedShow)
        {
            // Act
            var errorViewModel = new ErrorViewModel { RequestId = requestId };

            // Assert
            errorViewModel.ShowRequestId.Should().Be(expectedShow);
        }

        [Fact]
        public void ErrorViewModel_DefaultConstructor_ShouldHaveNullRequestId()
        {
            // Act
            var errorViewModel = new ErrorViewModel();

            // Assert
            errorViewModel.RequestId.Should().BeNull();
            errorViewModel.ShowRequestId.Should().BeFalse();
        }
    }
}
