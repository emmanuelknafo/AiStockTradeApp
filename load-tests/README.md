# AI Stock Trading API - Load Testing Suite

This directory contains comprehensive load testing configurations and scripts for the AI Stock Trading API microservice.

## 📁 Directory Structure

```
load-tests/
├── locust/
│   └── api_load_test.py          # Comprehensive Locust test script
├── jmeter/
│   ├── comprehensive-api-test.jmx # Advanced JMeter test plan
│   └── test-plan.azure.jmx       # Existing Azure-optimized plan
├── test-data/
│   ├── stock_symbols.csv         # Test stock symbols
│   └── test_users.csv            # Test user data
├── azure-load-test-config.yaml   # Azure Load Testing configuration
├── requirements.txt              # Python dependencies
├── run-load-test.ps1             # PowerShell test runner
└── README.md                     # This file
```

## 🎯 Test Coverage

The load testing suite covers all major API endpoints:

### Core Stock Data Endpoints
- **GET /api/stocks/quote** - Get real-time stock quotes
- **GET /api/stocks/historical** - Get historical price data
- **GET /api/stocks/suggestions** - Get stock symbol suggestions

### Listed Stocks Management
- **GET /api/listed-stocks** - Paginated stock listings
- **GET /api/listed-stocks/search** - Search stocks by criteria
- **GET /api/listed-stocks/{symbol}** - Get specific stock details
- **POST /api/listed-stocks** - Create/update single stock
- **POST /api/listed-stocks/bulk** - Bulk create/update stocks
- **GET /api/listed-stocks/count** - Get total stock count

### Historical Data (Database)
- **GET /api/historical-prices/{symbol}** - Get historical prices from DB
- **GET /api/historical-prices/count** - Get total historical records count
- **GET /api/historical-prices/{symbol}/count** - Get count by symbol

### Data Import Operations
- **POST /api/listed-stocks/import-csv** - Import stocks via CSV
- **POST /api/historical-prices/{symbol}/import-csv** - Import historical data
- **GET /api/listed-stocks/import-jobs/{id}** - Check import job status

### Utility Endpoints
- **GET /health** - Health check endpoint
- **GET /api/listed-stocks/facets/sectors** - Get available sectors
- **GET /api/listed-stocks/facets/industries** - Get available industries

## 🛠️ Test Tools

### 1. Locust (Python-based)
- **File**: `locust/api_load_test.py`
- **Features**: 
  - Multiple user classes with different behavior patterns
  - Weighted task distribution (realistic usage patterns)
  - Comprehensive endpoint coverage
  - Error handling and response validation
  - Custom think times and realistic data

### 2. JMeter (Java-based)
- **File**: `jmeter/comprehensive-api-test.jmx`
- **Features**:
  - Thread groups for different load patterns
  - CSV data-driven testing
  - Response assertions
  - Built-in reporting and dashboards
  - Parameterized configuration

### 3. Azure Load Testing
- **File**: `azure-load-test-config.yaml`
- **Features**:
  - Cloud-scale load testing
  - Application Insights integration
  - Success criteria definitions
  - Automated reporting

## 🚀 Quick Start

### Prerequisites

1. **Python 3.8+** with pip
2. **Apache JMeter 5.6+** (for JMeter tests)
3. **API running** (local or deployed)

### Install Dependencies

```powershell
# Install Python dependencies
pip install -r requirements.txt

# Verify Locust installation
locust --version
```

### Run Tests

#### Option 1: Using PowerShell Script (Recommended)

```powershell
# Basic Locust test (50 users, 5 minutes)
.\run-load-test.ps1 -TestType locust

# JMeter test with custom parameters
.\run-load-test.ps1 -TestType jmeter -Users 100 -Duration 600

# Run both tools against different environment
.\run-load-test.ps1 -TestType both -Environment development -Html

# Full parameter example
.\run-load-test.ps1 `
    -TestType both `
    -Environment local `
    -Users 75 `
    -Duration 300 `
    -TargetHost localhost `
    -Port 5001 `
    -Protocol https `
    -Html `
    -OutputDir "test-results-$(Get-Date -Format 'yyyyMMdd-HHmm')"
```

#### Option 2: Direct Command Line

```bash
# Locust - Headless mode with HTML report
locust -f locust/api_load_test.py \
    --host=https://localhost:5001 \
    --users=50 \
    --spawn-rate=5 \
    --run-time=300s \
    --headless \
    --html=test-results/locust-report.html

# Locust - Web UI mode (interactive)
locust -f locust/api_load_test.py --host=https://localhost:5001

# JMeter - Command line
jmeter -n \
    -t jmeter/comprehensive-api-test.jmx \
    -l test-results/jmeter-results.jtl \
    -e -o test-results/jmeter-dashboard \
    -JHOST=localhost -JPORT=5001 -JPROTOCOL=https \
    -JTHREADS=50 -JDURATION=300
```

## 📊 Load Test Patterns

### User Behavior Simulation

The tests simulate realistic user behavior with different patterns:

#### Regular API Users (80% of load)
- **Stock Quote Requests**: 20% - Most common operation
- **Historical Data**: 15% - Chart viewing
- **Stock Search**: 12% - Browse listings
- **Suggestions**: 10% - Autocomplete searches
- **Stock Details**: 5% - Detail page views
- **Facet Queries**: 3% - Filter operations

#### Bulk Operations Users (15% of load)
- **Single Stock Creation**: 3% - Admin operations
- **Bulk Stock Creation**: 1% - Data management
- **Longer think times** - Administrative workflows

#### Data Import Users (5% of load)
- **CSV Import Operations**: Very rare but high impact
- **Job Status Monitoring**: Following up on imports
- **Very long think times** - Batch processing workflows

### Load Progression

1. **Ramp-up Phase**: Gradually increase users over 1 minute
2. **Sustained Load**: Maintain peak load for test duration
3. **Ramp-down Phase**: Gradually decrease users over 1 minute

## 🎯 Success Criteria

### Performance Targets
- **95th percentile response time**: < 2 seconds
- **Error rate**: < 5%
- **Throughput**: > 100 requests/second
- **Availability**: > 99.5%

### Monitored Metrics
- Response times (avg, median, 95th, 99th percentile)
- Throughput (requests per second)
- Error rates by endpoint
- HTTP status code distribution
- Resource utilization (if monitoring available)

## Legacy JMeter Setup (Docker)

A corresponding Azure DevOps pipeline YAML is included at `azure-pipelines/load-tests.yml`.

How to use

- In Azure DevOps create a new pipeline and point it to the repository file `azure-pipelines/load-tests.yml`.
- The pipeline is configured for manual runs (no CI trigger). It runs JMeter in Docker on an `ubuntu-latest` agent and publishes `results.jtl` as a pipeline artifact.

Customize run parameters by editing the variables at the top of `azure-pipelines/load-tests.yml` (host, port, threads, ramp, loop).
