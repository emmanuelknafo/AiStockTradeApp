"""
AI Stock Trade API - Comprehensive Load Test Script
Tests various API endpoints with realistic user behavior patterns
"""

import random
import time
from locust import HttpUser, task, between
from urllib.parse import urlencode
import json


class StockApiUser(HttpUser):
    """
    Simulates a user interacting with the Stock Trading API
    """
    wait_time = between(1, 3)  # Wait 1-3 seconds between requests
    
    # Common stock symbols for testing
    STOCK_SYMBOLS = [
        "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA", 
        "NVDA", "META", "NFLX", "AMD", "INTC",
        "SPY", "QQQ", "IWM", "VTI", "BRK.B"
    ]
    
    SECTORS = ["Technology", "Healthcare", "Finance", "Energy", "Consumer"]
    INDUSTRIES = ["Software", "Hardware", "Biotech", "Banking", "Oil & Gas"]
    
    def on_start(self):
        """Called when a user starts - can be used for login, setup, etc."""
        # Test health endpoint first
        self.client.get("/health")
    
    @task(20)
    def get_stock_quote(self):
        """Get stock quote - most common operation (20% weight)"""
        symbol = random.choice(self.STOCK_SYMBOLS)
        with self.client.get(f"/api/stocks/quote?symbol={symbol}", 
                           catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            elif response.status_code == 404:
                # 404 is acceptable for invalid symbols
                response.success()
            else:
                response.failure(f"Unexpected status code: {response.status_code}")
    
    @task(15)
    def get_historical_data(self):
        """Get historical stock data (15% weight)"""
        symbol = random.choice(self.STOCK_SYMBOLS)
        days = random.choice([7, 14, 30, 60, 90])
        params = {"symbol": symbol, "days": days}
        
        with self.client.get(f"/api/stocks/historical?{urlencode(params)}", 
                           catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            else:
                response.failure(f"Historical data failed: {response.status_code}")
    
    @task(10)
    def get_stock_suggestions(self):
        """Get stock symbol suggestions (10% weight)"""
        queries = ["AP", "GOO", "MS", "AM", "TS", "NV", "ME", "NF"]
        query = random.choice(queries)
        
        with self.client.get(f"/api/stocks/suggestions?query={query}", 
                           catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            else:
                response.failure(f"Suggestions failed: {response.status_code}")
    
    @task(12)
    def get_listed_stocks(self):
        """Get paginated list of stocks (12% weight)"""
        skip = random.randint(0, 1000)
        take = random.choice([50, 100, 200, 500])
        params = {"skip": skip, "take": take}
        
        with self.client.get(f"/api/listed-stocks?{urlencode(params)}", 
                           catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            else:
                response.failure(f"Listed stocks failed: {response.status_code}")
    
    @task(8)
    def search_listed_stocks(self):
        """Search stocks by criteria (8% weight)"""
        search_params = {}
        
        # Randomly add search criteria
        if random.random() < 0.4:
            search_params["q"] = random.choice(["Apple", "Microsoft", "Google", "Tesla"])
        if random.random() < 0.3:
            search_params["sector"] = random.choice(self.SECTORS)
        if random.random() < 0.3:
            search_params["industry"] = random.choice(self.INDUSTRIES)
        
        search_params["skip"] = random.randint(0, 500)
        search_params["take"] = random.choice([50, 100, 200])
        
        with self.client.get(f"/api/listed-stocks/search?{urlencode(search_params)}", 
                           catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            else:
                response.failure(f"Stock search failed: {response.status_code}")
    
    @task(5)
    def get_historical_prices_db(self):
        """Get historical prices from database (5% weight)"""
        symbol = random.choice(self.STOCK_SYMBOLS)
        take = random.choice([10, 50, 100])
        
        with self.client.get(f"/api/historical-prices/{symbol}?take={take}", 
                           catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            elif response.status_code == 404:
                response.success()  # No data is acceptable
            else:
                response.failure(f"Historical prices DB failed: {response.status_code}")
    
    @task(5)
    def get_listed_stock_details(self):
        """Get details for a specific stock (5% weight)"""
        symbol = random.choice(self.STOCK_SYMBOLS)
        
        with self.client.get(f"/api/listed-stocks/{symbol}", 
                           catch_response=True) as response:
            if response.status_code in [200, 404]:
                response.success()
            else:
                response.failure(f"Stock details failed: {response.status_code}")
    
    @task(3)
    def get_facets(self):
        """Get filter facets (3% weight)"""
        facet_endpoint = random.choice([
            "/api/listed-stocks/facets/sectors",
            "/api/listed-stocks/facets/industries"
        ])
        
        with self.client.get(facet_endpoint, catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            else:
                response.failure(f"Facets failed: {response.status_code}")
    
    @task(3)
    def get_counts(self):
        """Get various count endpoints (3% weight)"""
        count_endpoint = random.choice([
            "/api/listed-stocks/count",
            "/api/historical-prices/count"
        ])
        
        with self.client.get(count_endpoint, catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            else:
                response.failure(f"Count failed: {response.status_code}")
    
    @task(2)
    def get_historical_prices_count_by_symbol(self):
        """Get historical price count for specific symbol (2% weight)"""
        symbol = random.choice(self.STOCK_SYMBOLS)
        
        with self.client.get(f"/api/historical-prices/{symbol}/count", 
                           catch_response=True) as response:
            if response.status_code in [200, 400]:  # 400 for invalid symbol is ok
                response.success()
            else:
                response.failure(f"Price count by symbol failed: {response.status_code}")
    
    @task(1)
    def test_health_endpoint(self):
        """Health check endpoint (1% weight)"""
        with self.client.get("/health", catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            else:
                response.failure(f"Health check failed: {response.status_code}")


class BulkOperationsUser(HttpUser):
    """
    Simulates users performing bulk operations (lower frequency, higher impact)
    """
    wait_time = between(5, 15)  # Longer wait times for bulk operations
    weight = 1  # Lower weight compared to regular users
    
    SAMPLE_STOCKS = [
        {
            "symbol": "TEST1",
            "name": "Test Stock 1",
            "lastSale": 150.50,
            "netChange": 2.50,
            "percentChange": 1.69,
            "marketCap": 1000000000,
            "country": "USA",
            "ipoYear": 2020,
            "volume": 1000000,
            "sector": "Technology",
            "industry": "Software"
        },
        {
            "symbol": "TEST2", 
            "name": "Test Stock 2",
            "lastSale": 75.25,
            "netChange": -1.25,
            "percentChange": -1.63,
            "marketCap": 500000000,
            "country": "USA",
            "ipoYear": 2019,
            "volume": 500000,
            "sector": "Healthcare",
            "industry": "Biotech"
        }
    ]
    
    @task(3)
    def create_single_stock(self):
        """Create/update a single stock (3% weight)"""
        stock = random.choice(self.SAMPLE_STOCKS).copy()
        stock["symbol"] = f"LOAD{random.randint(1000, 9999)}"
        stock["name"] = f"Load Test Stock {random.randint(1, 1000)}"
        
        with self.client.post("/api/listed-stocks", 
                            json=stock,
                            headers={"Content-Type": "application/json"},
                            catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            else:
                response.failure(f"Stock creation failed: {response.status_code}")
    
    @task(1)
    def bulk_create_stocks(self):
        """Bulk create/update stocks (1% weight - heavy operation)"""
        stocks = []
        for i in range(5):  # Create 5 stocks at once
            stock = random.choice(self.SAMPLE_STOCKS).copy()
            stock["symbol"] = f"BULK{random.randint(1000, 9999)}"
            stock["name"] = f"Bulk Test Stock {i}"
            stocks.append(stock)
        
        with self.client.post("/api/listed-stocks/bulk",
                            json=stocks,
                            headers={"Content-Type": "application/json"},
                            catch_response=True) as response:
            if response.status_code == 200:
                response.success()
            else:
                response.failure(f"Bulk stock creation failed: {response.status_code}")


class DataImportUser(HttpUser):
    """
    Simulates users importing data via CSV (very low frequency, high impact)
    """
    wait_time = between(30, 60)  # Very long wait times
    weight = 0.1  # Very low weight - rare operations
    
    SAMPLE_CSV_DATA = """Symbol,Name,Last Sale,Net Change,% Change,Market Cap,Country,IPO Year,Volume,Sector,Industry
LOADTEST1,Load Test Company 1,$100.50,$2.50,2.55%,$1000000000,USA,2020,1000000,Technology,Software
LOADTEST2,Load Test Company 2,$50.25,-$1.25,-2.43%,$500000000,USA,2019,500000,Healthcare,Biotech"""

    SAMPLE_HISTORICAL_CSV = """Date,Open,High,Low,Close,Volume
2024-01-01,100.00,105.00,99.00,104.50,1000000
2024-01-02,104.50,106.00,103.00,105.25,1100000
2024-01-03,105.25,107.00,104.00,106.75,900000"""
    
    @task(1)
    def import_stocks_csv(self):
        """Import stocks via CSV (very rare operation)"""
        with self.client.post("/api/listed-stocks/import-csv",
                            data=self.SAMPLE_CSV_DATA,
                            headers={
                                "Content-Type": "text/csv",
                                "X-File-Name": f"load-test-{random.randint(1000, 9999)}.csv"
                            },
                            catch_response=True) as response:
            if response.status_code == 202:  # Accepted for background processing
                response.success()
                # Optionally check job status
                if hasattr(response, 'json') and 'jobId' in response.json():
                    job_id = response.json()['jobId']
                    self.check_import_job_status(job_id)
            else:
                response.failure(f"CSV import failed: {response.status_code}")
    
    def check_import_job_status(self, job_id):
        """Check the status of an import job"""
        with self.client.get(f"/api/listed-stocks/import-jobs/{job_id}",
                           catch_response=True) as response:
            if response.status_code in [200, 404]:  # 404 if job not found is ok
                response.success()
            else:
                response.failure(f"Job status check failed: {response.status_code}")


if __name__ == "__main__":
    # For running locally with custom settings
    import os
    from locust import run_single_user
    
    # Set environment variables if needed
    os.environ.setdefault("LOCUST_HOST", "https://localhost:5001")
    
    # Run a single user for testing
    run_single_user(StockApiUser)
