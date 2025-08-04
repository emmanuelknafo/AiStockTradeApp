// Configuration file for the Stock Tracker
// Replace 'YOUR_ALPHA_VANTAGE_API_KEY' with your actual API key from https://www.alphavantage.co/support/#api-key

window.CONFIG = {
    ALPHA_VANTAGE_API_KEY: 'UNS2EQBZKW1T8QAP',
    
    // Optional: You can also configure other settings here
    REFRESH_INTERVAL: 30000, // 30 seconds
    MAX_WATCHLIST_SIZE: 20,
    
    // API endpoints (you usually don't need to change these)
    APIS: {
        ALPHA_VANTAGE: 'https://www.alphavantage.co/query',
        YAHOO_FINANCE: 'https://query1.finance.yahoo.com/v8/finance/chart',
        TWELVE_DATA: 'https://api.twelvedata.com'
    }
};
