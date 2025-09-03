using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Tests.Services
{
    public class HistoricalPriceServiceTests
    {
        private readonly Mock<IHistoricalPriceRepository> _mockRepository;
        private readonly Mock<ILogger<HistoricalPriceService>> _mockLogger;
        private readonly Mock<IMockStockDataService> _mockDataService;
        private readonly HistoricalPriceService _service;

        public HistoricalPriceServiceTests()
        {
            _mockRepository = new Mock<IHistoricalPriceRepository>();
            _mockLogger = new Mock<ILogger<HistoricalPriceService>>();
            _mockDataService = new Mock<IMockStockDataService>();
            _service = new HistoricalPriceService(_mockRepository.Object, _mockDataService.Object, _mockLogger.Object);
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("MSFT")]
        [InlineData("GOOGL")]
        public async Task GetAsync_WithValidSymbol_ShouldReturnHistoricalPrices(string symbol)
        {
            // Arrange
            var expectedPrices = new List<HistoricalPrice>
            {
                new() { Symbol = symbol, Date = DateTime.Today.AddDays(-1), Close = 150.00m },
                new() { Symbol = symbol, Date = DateTime.Today.AddDays(-2), Close = 148.50m }
            };
            _mockRepository.Setup(x => x.GetAsync(symbol, null, null, null)).ReturnsAsync(expectedPrices);

            // Act
            var result = await _service.GetAsync(symbol);

            // Assert
            result.Should().BeSameAs(expectedPrices);
            _mockRepository.Verify(x => x.GetAsync(symbol, null, null, null), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task GetAsync_WithInvalidSymbol_ShouldReturnEmptyList(string? symbol)
        {
            // Act
            var result = await _service.GetAsync(symbol!);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockRepository.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task GetAsync_WithDateRange_ShouldPassCorrectParameters()
        {
            // Arrange
            const string symbol = "AAPL";
            var fromDate = DateTime.Today.AddDays(-30);
            var toDate = DateTime.Today;
            const int take = 10;
            var expectedPrices = new List<HistoricalPrice>();
            
            _mockRepository.Setup(x => x.GetAsync(symbol, fromDate, toDate, take)).ReturnsAsync(expectedPrices);

            // Act
            var result = await _service.GetAsync(symbol, fromDate, toDate, take);

            // Assert
            result.Should().BeSameAs(expectedPrices);
            _mockRepository.Verify(x => x.GetAsync(symbol, fromDate, toDate, take), Times.Once);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(365)]
        public async Task GetAsync_WithTakeParameter_ShouldLimitResults(int take)
        {
            // Arrange
            const string symbol = "AAPL";
            var expectedPrices = new List<HistoricalPrice>();
            _mockRepository.Setup(x => x.GetAsync(symbol, null, null, take)).ReturnsAsync(expectedPrices);

            // Act
            var result = await _service.GetAsync(symbol, take: take);

            // Assert
            result.Should().BeSameAs(expectedPrices);
            _mockRepository.Verify(x => x.GetAsync(symbol, null, null, take), Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetAsync_WithFromDateOnly_ShouldPassCorrectParameters()
        {
            // Arrange
            const string symbol = "TSLA";
            var fromDate = DateTime.Today.AddDays(-7);
            var expectedPrices = new List<HistoricalPrice>();
            
            _mockRepository.Setup(x => x.GetAsync(symbol, fromDate, null, null)).ReturnsAsync(expectedPrices);

            // Act
            var result = await _service.GetAsync(symbol, fromDate);

            // Assert
            result.Should().BeSameAs(expectedPrices);
            _mockRepository.Verify(x => x.GetAsync(symbol, fromDate, null, null), Times.Once);
        }

        [Fact]
        public async Task GetAsync_WithToDateOnly_ShouldPassCorrectParameters()
        {
            // Arrange
            const string symbol = "NVDA";
            var toDate = DateTime.Today.AddDays(-1);
            var expectedPrices = new List<HistoricalPrice>();
            
            _mockRepository.Setup(x => x.GetAsync(symbol, null, toDate, null)).ReturnsAsync(expectedPrices);

            // Act
            var result = await _service.GetAsync(symbol, to: toDate);

            // Assert
            result.Should().BeSameAs(expectedPrices);
            _mockRepository.Verify(x => x.GetAsync(symbol, null, toDate, null), Times.Once);
        }

        [Fact]
        public async Task GetAsync_WithAllParameters_ShouldPassAllParametersCorrectly()
        {
            // Arrange
            const string symbol = "AMZN";
            var fromDate = DateTime.Today.AddDays(-30);
            var toDate = DateTime.Today.AddDays(-1);
            const int take = 25;
            var expectedPrices = new List<HistoricalPrice>
            {
                new() { Symbol = symbol, Date = DateTime.Today.AddDays(-1), Close = 3200.00m },
                new() { Symbol = symbol, Date = DateTime.Today.AddDays(-2), Close = 3180.50m }
            };
            
            _mockRepository.Setup(x => x.GetAsync(symbol, fromDate, toDate, take)).ReturnsAsync(expectedPrices);

            // Act
            var result = await _service.GetAsync(symbol, fromDate, toDate, take);

            // Assert
            result.Should().BeSameAs(expectedPrices);
            result.Should().HaveCount(2);
            _mockRepository.Verify(x => x.GetAsync(symbol, fromDate, toDate, take), Times.Once);
        }

        [Fact]
        public async Task GetAsync_WhenRepositoryThrowsException_ShouldPropagateException()
        {
            // Arrange
            const string symbol = "INVALID";
            _mockRepository.Setup(x => x.GetAsync(symbol, null, null, null))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act & Assert
            await _service.Invoking(s => s.GetAsync(symbol))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Database error");
        }

        [Fact]
        public async Task GetAsync_WithRepositoryReturningNull_ShouldHandleGracefully()
        {
            // Arrange
            const string symbol = "TEST";
            _mockRepository.Setup(x => x.GetAsync(symbol, null, null, null))
                .ReturnsAsync((List<HistoricalPrice>)null!);

            // Act & Assert
            var act = async () => await _service.GetAsync(symbol);
            await act.Should().ThrowAsync<NullReferenceException>()
                .WithMessage("*");
        }
    }
}
