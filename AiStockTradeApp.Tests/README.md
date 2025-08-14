# Test Configuration

This directory contains comprehensive unit and integration tests for the AI Stock Trade App.

## Test Structure

```
ai-stock-trade-app.Tests/
├── Controllers/
│   ├── StockControllerTests.cs    # Tests for StockController endpoints
│   └── HomeControllerTests.cs     # Tests for HomeController
├── Services/
│   ├── StockDataServiceTests.cs   # Tests for external API integration
│   ├── AIAnalysisServiceTests.cs  # Tests for AI analysis generation
│   └── WatchlistServiceTests.cs   # Tests for watchlist management
├── Models/
│   ├── StockDataTests.cs          # Tests for StockData model
│   └── ViewModelsTests.cs         # Tests for view models
└── Integration/
    └── WebApplicationTests.cs     # End-to-end integration tests
```

## Running Tests

### Command Line
```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"

# Run tests in parallel
dotnet test --parallel
```

### Visual Studio
1. Open Test Explorer (Test → Test Explorer)
2. Click "Run All Tests" or right-click specific tests
3. View test results and coverage

## Test Coverage Areas

### Unit Tests (80+ tests)
- ✅ **Models**: StockData, ViewModels, ChartDataPoint
- ✅ **Services**: StockDataService, AIAnalysisService, WatchlistService
- ✅ **Controllers**: StockController, HomeController
- ✅ **Business Logic**: Portfolio calculations, session management
- ✅ **Error Handling**: Invalid inputs, API failures, edge cases

### Integration Tests (10+ tests)
- ✅ **HTTP Endpoints**: All controller actions
- ✅ **Static Assets**: CSS, JavaScript, images
- ✅ **Health Checks**: Application health monitoring
- ✅ **Session Management**: Cross-request state persistence
- ✅ **Dependency Injection**: Service registration validation

## Test Frameworks Used

- **xUnit**: Primary testing framework
- **FluentAssertions**: Readable assertions
- **Moq**: Mocking external dependencies
- **ASP.NET Core Testing**: Integration testing support
- **Coverlet**: Code coverage analysis

## Mocking Strategy

- **External APIs**: Stock data services are mocked for reliable testing
- **HTTP Clients**: Network calls are mocked to avoid external dependencies
- **Session State**: Session management is mocked for controller tests
- **Logging**: Logger interfaces are mocked for clean test output

## Best Practices Implemented

1. **Arrange-Act-Assert Pattern**: Clear test structure
2. **Descriptive Test Names**: Self-documenting test methods
3. **Theory Tests**: Data-driven testing with multiple inputs
4. **Parallel Execution**: Tests run independently and in parallel
5. **Mocking External Dependencies**: Isolated unit tests
6. **Integration Testing**: End-to-end scenario validation

## Coverage Goals

- **Unit Tests**: >80% code coverage
- **Critical Paths**: 100% coverage for business logic
- **Error Handling**: All exception paths tested
- **Edge Cases**: Boundary conditions validated

## Continuous Integration

Tests are automatically run in CI/CD pipeline:
- On every pull request
- Before deployment to any environment
- With code coverage reporting
- With test result publishing

## Adding New Tests

When adding new features:
1. Write tests first (TDD approach)
2. Add unit tests for new methods/classes
3. Add integration tests for new endpoints
4. Update this documentation
5. Verify coverage meets standards
