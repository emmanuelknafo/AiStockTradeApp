using AiStockTradeApp.SeleniumTests.Infrastructure;

namespace AiStockTradeApp.SeleniumTests;

/// <summary>
/// Assembly-level test class to handle global teardown for Selenium tests.
/// This ensures that any auto-started processes are properly cleaned up when tests complete.
/// </summary>
public sealed class AssemblyTeardownTests : IDisposable
{
    private static readonly object _lock = new();
    private static bool _disposed = false;

    static AssemblyTeardownTests()
    {
        // Register for AppDomain shutdown to ensure cleanup
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => GlobalCleanup();
        AppDomain.CurrentDomain.DomainUnload += (sender, e) => GlobalCleanup();
    }

    public void Dispose()
    {
        GlobalCleanup();
    }

    private static void GlobalCleanup()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            try
            {
                TestBase.GlobalTeardown();
            }
            catch
            {
                // Ignore cleanup errors to avoid masking test failures
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    [Fact]
    public void EnsureCleanupRegistered()
    {
        // This test exists solely to ensure the static constructor runs
        // and cleanup handlers are registered. The actual cleanup happens
        // in the Dispose method and static event handlers.
        Assert.True(true);
    }
}