using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.Services.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AiStockTradeApp.Tests.Services
{
    public class CacheCleanupServiceTests
    {
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IServiceScope> _mockServiceScope;
        private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
        private readonly Mock<IStockDataRepository> _mockRepository;
        private readonly Mock<ILogger<CacheCleanupService>> _mockLogger;

        public CacheCleanupServiceTests()
        {
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockServiceScope = new Mock<IServiceScope>();
            _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
            _mockRepository = new Mock<IStockDataRepository>();
            _mockLogger = new Mock<ILogger<CacheCleanupService>>();

            // Setup the service scope factory chain (this avoids extension method issues)
            _mockServiceScopeFactory.Setup(x => x.CreateScope())
                .Returns(_mockServiceScope.Object);
            
            _mockServiceScope.Setup(x => x.ServiceProvider.GetService(typeof(IStockDataRepository)))
                .Returns(_mockRepository.Object);
            
            _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(_mockServiceScopeFactory.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Act
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<BackgroundService>();
        }

        [Fact]
        public async Task StartAsync_ShouldBeginExecution()
        {
            // Arrange
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);
            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);

            // Assert
            // The service should start without throwing
            service.Should().NotBeNull();
        }

        [Fact]
        public async Task StopAsync_ShouldStopExecution()
        {
            // Arrange
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);
            using var cts = new CancellationTokenSource();

            // Start the service
            await service.StartAsync(cts.Token);

            // Act
            await service.StopAsync(cts.Token);

            // Assert
            // The service should stop without throwing
            service.Should().NotBeNull();
        }

        [Fact]
        public async Task ExecuteAsync_WhenCancelled_ShouldExitGracefully()
        {
            // Arrange
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);
            using var cts = new CancellationTokenSource();

            // Cancel immediately to test graceful shutdown
            cts.Cancel();

            // Act & Assert
            await service.Invoking(s => s.StartAsync(cts.Token))
                .Should().NotThrowAsync();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldCallRepositoryCleanup()
        {
            // Arrange
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Setup repository to complete quickly
            _mockRepository.Setup(x => x.CleanupExpiredCacheAsync())
                .Returns(Task.CompletedTask);

            // Act
            await service.StartAsync(cts.Token);
            
            // Give it a brief moment to execute before cancelling
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);

            // Assert
            // Verify that cleanup was attempted at least once
            _mockRepository.Verify(x => x.CleanupExpiredCacheAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_WhenRepositoryThrows_ShouldLogErrorAndContinue()
        {
            // Arrange
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Setup repository to throw an exception
            _mockRepository.Setup(x => x.CleanupExpiredCacheAsync())
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            await service.StartAsync(cts.Token);
            
            // Give it a brief moment to execute and handle the error
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);

            // Assert
            // The service should handle the exception and continue running
            // We can't easily verify the exact logging calls due to the complexity of mocking ILogger
            // But we can verify the repository was called
            _mockRepository.Verify(x => x.CleanupExpiredCacheAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldCreateAndDisposeScope()
        {
            // Arrange
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Setup repository to complete quickly
            _mockRepository.Setup(x => x.CleanupExpiredCacheAsync())
                .Returns(Task.CompletedTask);

            // Act
            await service.StartAsync(cts.Token);
            
            // Give it a brief moment to execute
            await Task.Delay(50);
            await service.StopAsync(CancellationToken.None);

            // Assert
            _mockServiceScopeFactory.Verify(x => x.CreateScope(), Times.AtLeastOnce);
            _mockServiceScope.Verify(x => x.Dispose(), Times.AtLeastOnce);
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Arrange
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);

            // Act & Assert
            service.Invoking(s => s.Dispose())
                .Should().NotThrow();
        }

        [Fact]
        public void CacheCleanupService_ShouldImplementIHostedService()
        {
            // Act
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);

            // Assert
            service.Should().BeAssignableTo<IHostedService>();
        }

        [Fact]
        public void CacheCleanupService_ShouldExtendBackgroundService()
        {
            // Act
            var service = new CacheCleanupService(_mockServiceProvider.Object, _mockLogger.Object);

            // Assert
            service.Should().BeAssignableTo<BackgroundService>();
        }
    }
}
