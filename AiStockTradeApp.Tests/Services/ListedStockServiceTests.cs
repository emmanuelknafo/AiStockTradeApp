using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Implementations;
using AiStockTradeApp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Tests.Services
{
    public class ListedStockServiceTests
    {
        private readonly Mock<IListedStockRepository> _mockRepository;
        private readonly Mock<ILogger<ListedStockService>> _mockLogger;
        private readonly ListedStockService _service;

        public ListedStockServiceTests()
        {
            _mockRepository = new Mock<IListedStockRepository>();
            _mockLogger = new Mock<ILogger<ListedStockService>>();
            _service = new ListedStockService(_mockRepository.Object);
        }

        [Fact]
        public async Task UpsertAsync_ShouldCallRepository()
        {
            // Arrange
            var stock = new ListedStock 
            { 
                Symbol = "AAPL", 
                Name = "Apple Inc.", 
                Sector = "Technology",
                Industry = "Consumer Electronics"
            };

            // Act
            await _service.UpsertAsync(stock);

            // Assert
            _mockRepository.Verify(x => x.UpsertAsync(stock), Times.Once);
        }

        [Fact]
        public async Task BulkUpsertAsync_ShouldCallRepository()
        {
            // Arrange
            var stocks = new List<ListedStock>
            {
                new() { Symbol = "AAPL", Name = "Apple Inc." },
                new() { Symbol = "MSFT", Name = "Microsoft Corporation" }
            };

            // Act
            await _service.BulkUpsertAsync(stocks);

            // Assert
            _mockRepository.Verify(x => x.BulkUpsertAsync(stocks), Times.Once);
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("aapl")]
        [InlineData("Aapl")]
        public async Task GetAsync_ShouldConvertSymbolToUppercase(string inputSymbol)
        {
            // Arrange
            var expectedStock = new ListedStock { Symbol = "AAPL", Name = "Apple Inc." };
            _mockRepository.Setup(x => x.GetBySymbolAsync("AAPL")).ReturnsAsync(expectedStock);

            // Act
            var result = await _service.GetAsync(inputSymbol);

            // Assert
            _mockRepository.Verify(x => x.GetBySymbolAsync("AAPL"), Times.Once);
            result.Should().Be(expectedStock);
        }

        [Fact]
        public async Task GetAsync_WithNonExistentSymbol_ShouldReturnNull()
        {
            // Arrange
            _mockRepository.Setup(x => x.GetBySymbolAsync("INVALID")).ReturnsAsync((ListedStock?)null);

            // Act
            var result = await _service.GetAsync("INVALID");

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(0, 50)]
        [InlineData(100, 25)]
        [InlineData(500, 100)]
        public async Task GetAllAsync_ShouldCallRepositoryWithCorrectParameters(int skip, int take)
        {
            // Arrange
            var expectedStocks = new List<ListedStock>();
            _mockRepository.Setup(x => x.GetAllAsync(skip, take)).ReturnsAsync(expectedStocks);

            // Act
            var result = await _service.GetAllAsync(skip, take);

            // Assert
            _mockRepository.Verify(x => x.GetAllAsync(skip, take), Times.Once);
            result.Should().BeSameAs(expectedStocks);
        }

        [Fact]
        public async Task GetAllAsync_WithDefaultParameters_ShouldUseDefaults()
        {
            // Arrange
            var expectedStocks = new List<ListedStock>();
            _mockRepository.Setup(x => x.GetAllAsync(0, 500)).ReturnsAsync(expectedStocks);

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            _mockRepository.Verify(x => x.GetAllAsync(0, 500), Times.Once);
            result.Should().BeSameAs(expectedStocks);
        }

        [Theory]
        [InlineData("Technology", "Software", "AAPL", 0, 50)]
        [InlineData(null, "Banking", "BANK", 10, 25)]
        [InlineData("Healthcare", null, null, 20, 100)]
        [InlineData(null, null, null, 0, 500)]
        public async Task SearchAsync_ShouldCallRepositoryWithCorrectParameters(string? sector, string? industry, string? q, int skip, int take)
        {
            // Arrange
            var expectedStocks = new List<ListedStock>();
            _mockRepository.Setup(x => x.SearchAsync(sector, industry, q, skip, take)).ReturnsAsync(expectedStocks);

            // Act
            var result = await _service.SearchAsync(sector, industry, q, skip, take);

            // Assert
            _mockRepository.Verify(x => x.SearchAsync(sector, industry, q, skip, take), Times.Once);
            result.Should().BeSameAs(expectedStocks);
        }

        [Fact]
        public async Task SearchAsync_WithDefaultParameters_ShouldUseDefaults()
        {
            // Arrange
            var expectedStocks = new List<ListedStock>();
            _mockRepository.Setup(x => x.SearchAsync(null, null, null, 0, 500)).ReturnsAsync(expectedStocks);

            // Act
            var result = await _service.SearchAsync(null, null, null);

            // Assert
            _mockRepository.Verify(x => x.SearchAsync(null, null, null, 0, 500), Times.Once);
            result.Should().BeSameAs(expectedStocks);
        }

        [Fact]
        public async Task CountAsync_ShouldReturnRepositoryResult()
        {
            // Arrange
            const int expectedCount = 1500;
            _mockRepository.Setup(x => x.CountAsync()).ReturnsAsync(expectedCount);

            // Act
            var result = await _service.CountAsync();

            // Assert
            result.Should().Be(expectedCount);
            _mockRepository.Verify(x => x.CountAsync(), Times.Once);
        }

        [Theory]
        [InlineData("Technology", "Software", "AAPL", 25)]
        [InlineData(null, "Banking", null, 100)]
        [InlineData("Healthcare", null, "HEALTH", 50)]
        public async Task SearchCountAsync_ShouldReturnRepositoryResult(string? sector, string? industry, string? q, int expectedCount)
        {
            // Arrange
            _mockRepository.Setup(x => x.SearchCountAsync(sector, industry, q)).ReturnsAsync(expectedCount);

            // Act
            var result = await _service.SearchCountAsync(sector, industry, q);

            // Assert
            result.Should().Be(expectedCount);
            _mockRepository.Verify(x => x.SearchCountAsync(sector, industry, q), Times.Once);
        }

        [Fact]
        public async Task GetDistinctSectorsAsync_ShouldReturnRepositoryResult()
        {
            // Arrange
            var expectedSectors = new List<string> { "Technology", "Healthcare", "Finance" };
            _mockRepository.Setup(x => x.GetDistinctSectorsAsync()).ReturnsAsync(expectedSectors);

            // Act
            var result = await _service.GetDistinctSectorsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedSectors);
            _mockRepository.Verify(x => x.GetDistinctSectorsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetDistinctIndustriesAsync_ShouldReturnRepositoryResult()
        {
            // Arrange
            var expectedIndustries = new List<string> { "Software", "Banking", "Pharmaceuticals" };
            _mockRepository.Setup(x => x.GetDistinctIndustriesAsync()).ReturnsAsync(expectedIndustries);

            // Act
            var result = await _service.GetDistinctIndustriesAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedIndustries);
            _mockRepository.Verify(x => x.GetDistinctIndustriesAsync(), Times.Once);
        }

        [Fact]
        public async Task DeleteAllAsync_ShouldCallRepository()
        {
            // Act
            await _service.DeleteAllAsync();

            // Assert
            _mockRepository.Verify(x => x.DeleteAllAsync(), Times.Once);
        }
    }
}
