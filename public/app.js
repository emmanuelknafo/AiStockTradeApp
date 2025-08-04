// Get API key from config file or fallback to inline constant
const API_KEY = (window.CONFIG && window.CONFIG.ALPHA_VANTAGE_API_KEY) || 'YOUR_ALPHA_VANTAGE_API_KEY';
const WATCHLIST_KEY = 'stockWatchlist';
let watchlist = JSON.parse(localStorage.getItem(WATCHLIST_KEY)) || [];
// Store fetched data for portfolio summary and export
const stockDataMap = {};

// Rate limiting for free APIs
const rateLimitDelay = 1000; // 1 second between requests
let lastRequestTime = 0;

// Auto-refresh functionality
let autoRefreshInterval = null;
let isAutoRefreshEnabled = false;

// Price alerts
let priceAlerts = JSON.parse(localStorage.getItem('priceAlerts')) || {};

// Theme management
let currentTheme = localStorage.getItem('theme') || 'light';

document.addEventListener('DOMContentLoaded', () => {
  document.getElementById('add-button').addEventListener('click', addStock);
  loadWatchlist();
  // Export CSV button
  document.getElementById('export-csv').addEventListener('click', exportCSV);
  
  // Show API status
  showAPIStatus();
  
  // Initialize new features
  initializeTheme();
  initializeAutoRefresh();
  initializeSearchSuggestions();
  setupKeyboardShortcuts();
});

function showAPIStatus() {
  const statusDiv = document.createElement('div');
  statusDiv.id = 'api-status';
  statusDiv.style.cssText = 'background: #f0f0f0; padding: 10px; margin: 10px 0; border-radius: 5px; font-size: 12px;';
  
  if (API_KEY && API_KEY !== 'YOUR_ALPHA_VANTAGE_API_KEY') {
    statusDiv.innerHTML = '<strong>Status:</strong> Using Alpha Vantage API (Real-time data)';
    statusDiv.style.backgroundColor = '#d4edda';
  } else {
    statusDiv.innerHTML = '<strong>Status:</strong> Using free APIs (Yahoo Finance, Twelve Data) - <a href="https://www.alphavantage.co/support/#api-key" target="_blank">Get Alpha Vantage API key for premium features</a>';
    statusDiv.style.backgroundColor = '#fff3cd';
  }
  
  document.querySelector('.container').insertBefore(statusDiv, document.querySelector('.search-bar'));
}

function addStock() {
  const input = document.getElementById('ticker-input');
  const ticker = input.value.trim().toUpperCase();
  if (ticker && !watchlist.includes(ticker)) {
    if (watchlist.length >= (window.CONFIG?.MAX_WATCHLIST_SIZE || 20)) {
      showNotification('Maximum watchlist size reached!', 'warning');
      return;
    }
    watchlist.push(ticker);
    localStorage.setItem(WATCHLIST_KEY, JSON.stringify(watchlist));
    renderStock(ticker);
    showNotification(`Added ${ticker} to watchlist`, 'success');
  }
  input.value = '';
  hideSuggestions();
}

function loadWatchlist() {
  watchlist.forEach(ticker => renderStock(ticker));
}

async function renderStock(ticker) {
  const watchlistDiv = document.getElementById('watchlist');
  const card = document.createElement('div');
  card.className = 'stock-card';
  card.id = `card-${ticker}`;
  card.innerHTML = `
    <div class="card-header">
      <h2>${ticker}</h2>
      <button class="remove-button" data-ticker="${ticker}" title="Remove ${ticker}">&times;</button>
    </div>
    <div class="card-body">
      <p class="price">Price: <span>Loading...</span></p>
      <p class="change">Change: <span>Loading...</span></p>
      <p class="percent">Percent: <span>Loading...</span></p>
      ${localStorage.getItem('show-charts') === 'true' ? '<div class="mini-chart">📈 Chart Coming Soon</div>' : ''}
      <div class="ai-analysis"><strong>Analysis:</strong> <em>Loading...</em></div>
      <div class="ai-recommend"><strong>Recommendation:</strong> <em>Loading...</em></div>
    </div>`;
  watchlistDiv.appendChild(card);
  
  card.querySelector('.remove-button').addEventListener('click', () => removeStock(ticker));
  
  // Add double-click to set alert
  card.addEventListener('dblclick', () => {
    const price = prompt(`Set price alert for ${ticker}. Current price: $${stockDataMap[ticker]?.price || 'Loading...'}\n\nEnter target price:`);
    if (price && !isNaN(price)) {
      setPriceAlert(ticker, parseFloat(price));
    }
  });
  
  try {
    const data = await fetchStockData(ticker);
    updateCardWithData(card, data);
    // store for portfolio
    stockDataMap[ticker] = data;
    updatePortfolioSummary();
    const ai = await generateMockAIAnalysis(ticker, data);
    stockDataMap[ticker] = { ...data, ...ai }; // Store AI data too
    updateCardWithAI(card, ai);
    
    // Update alert indicators
    updateAlertIndicators();
  } catch (error) {
    console.error(`Error fetching ${ticker}:`, error);
    card.querySelector('.price span').textContent = 'Error';
    card.querySelector('.change span').textContent = 'N/A';
    card.querySelector('.percent span').textContent = 'N/A';
    card.querySelector('.ai-analysis').innerHTML = `<strong>Analysis:</strong> <em>Unable to fetch data: ${error.message}</em>`;
    card.querySelector('.ai-recommend').innerHTML = `<strong>Recommendation:</strong> <em>Data unavailable</em>`;
  }
}

function setPriceAlert(ticker, targetPrice) {
  const currentPrice = stockDataMap[ticker]?.price;
  if (!currentPrice) {
    showNotification('Cannot set alert: current price unavailable', 'error');
    return;
  }
  
  const type = targetPrice > currentPrice ? 'above' : 'below';
  
  if (!priceAlerts[ticker]) {
    priceAlerts[ticker] = [];
  }
  
  priceAlerts[ticker].push({
    price: targetPrice,
    type: type,
    created: new Date().toISOString()
  });
  
  localStorage.setItem('priceAlerts', JSON.stringify(priceAlerts));
  updateAlertIndicators();
  
  showNotification(
    `Alert set: ${ticker} ${type} $${targetPrice}`,
    'success'
  );
}

async function fetchStockData(ticker) {
  // Rate limiting to respect API limits
  const now = Date.now();
  const timeSinceLastRequest = now - lastRequestTime;
  if (timeSinceLastRequest < rateLimitDelay) {
    await new Promise(resolve => setTimeout(resolve, rateLimitDelay - timeSinceLastRequest));
  }
  lastRequestTime = Date.now();

  // Try Alpha Vantage first if API key is provided
  if (API_KEY && API_KEY !== 'YOUR_ALPHA_VANTAGE_API_KEY') {
    try {
      return await fetchFromAlphaVantage(ticker);
    } catch (error) {
      console.warn('Alpha Vantage failed, trying backup API:', error.message);
    }
  }

  // Fallback to Yahoo Finance via YH Finance API (free)
  try {
    return await fetchFromYahooFinance(ticker);
  } catch (error) {
    console.warn('Yahoo Finance failed, trying another backup:', error.message);
  }

  // Fallback to Twelve Data (free tier)
  try {
    return await fetchFromTwelveData(ticker);
  } catch (error) {
    console.warn('Twelve Data failed:', error.message);
  }

  // If all APIs fail, throw error
  throw new Error(`Unable to fetch data for ${ticker} from any source`);
}

async function fetchFromAlphaVantage(ticker) {
  const url = `https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol=${ticker}&apikey=${API_KEY}`;
  const resp = await fetch(url);
  if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
  
  const json = await resp.json();
  
  if (json['Error Message']) {
    throw new Error(json['Error Message']);
  }
  
  if (json['Note']) {
    throw new Error('API rate limit exceeded');
  }
  
  const quote = json['Global Quote'];
  if (!quote || !quote['05. price']) {
    throw new Error('Invalid response format');
  }
  
  const price = parseFloat(quote['05. price']);
  const change = parseFloat(quote['09. change']);
  const percent = quote['10. change percent'];
  
  return { price, change, percent };
}

async function fetchFromYahooFinance(ticker) {
  // Using a public Yahoo Finance API proxy
  const url = `https://query1.finance.yahoo.com/v8/finance/chart/${ticker}`;
  const resp = await fetch(url);
  if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
  
  const json = await resp.json();
  
  if (json.chart.error) {
    throw new Error(json.chart.error.description);
  }
  
  const result = json.chart.result[0];
  if (!result || !result.meta) {
    throw new Error('Invalid ticker symbol');
  }
  
  const meta = result.meta;
  const price = meta.regularMarketPrice;
  const previousClose = meta.previousClose;
  const change = price - previousClose;
  const percent = ((change / previousClose) * 100).toFixed(2) + '%';
  
  return { 
    price: parseFloat(price.toFixed(2)), 
    change: parseFloat(change.toFixed(2)), 
    percent 
  };
}

async function fetchFromTwelveData(ticker) {
  // Using Twelve Data free API (no key required for basic usage)
  const url = `https://api.twelvedata.com/quote?symbol=${ticker}&apikey=demo`;
  const resp = await fetch(url);
  if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
  
  const json = await resp.json();
  
  if (json.status === 'error') {
    throw new Error(json.message);
  }
  
  if (!json.close) {
    throw new Error('Invalid response format');
  }
  
  const price = parseFloat(json.close);
  const change = parseFloat(json.change);
  const percent = json.percent_change + '%';
  
  return { price, change, percent };
}

function updateCardWithData(card, { price, change, percent }) {
  card.querySelector('.price span').textContent = `$${price}`;
  card.querySelector('.change span').textContent = `${change}`;
  card.querySelector('.percent span').textContent = `${percent}`;
  const changeVal = parseFloat(change);
  if (changeVal > 0) {
    card.querySelector('.change').classList.add('positive');
  } else if (changeVal < 0) {
    card.querySelector('.change').classList.add('negative');
  }
}

function removeStock(ticker) {
  watchlist = watchlist.filter(t => t !== ticker);
  localStorage.setItem(WATCHLIST_KEY, JSON.stringify(watchlist));
  const card = document.getElementById(`card-${ticker}`);
  if (card) card.remove();
  // remove from data and update summary
  delete stockDataMap[ticker];
  updatePortfolioSummary();
}

async function generateMockAIAnalysis(ticker, data) {
  const changeVal = parseFloat(data.change);
  const priceVal = parseFloat(data.price);
  const percentVal = parseFloat(data.percent.replace('%', ''));
  
  let recommendation = 'Hold';
  let reasoning = 'Stable price movement; monitor for trends.';
  
  // More sophisticated analysis based on actual data
  if (percentVal < -5) {
    recommendation = 'Strong Buy';
    reasoning = 'Significant price drop may present buying opportunity.';
  } else if (percentVal < -2) {
    recommendation = 'Buy';
    reasoning = 'Price dipped recently; potential value opportunity.';
  } else if (percentVal > 5) {
    recommendation = 'Consider Selling';
    reasoning = 'Strong price increase; consider taking profits.';
  } else if (percentVal > 2) {
    recommendation = 'Sell';
    reasoning = 'Price increased significantly; good profit opportunity.';
  } else if (Math.abs(percentVal) < 0.5) {
    recommendation = 'Hold';
    reasoning = 'Minimal price movement; wait for clearer signals.';
  }
  
  // Add price level analysis
  let priceAnalysis = '';
  if (priceVal < 10) {
    priceAnalysis = ' Stock is in penny stock territory - high risk/reward.';
  } else if (priceVal > 1000) {
    priceAnalysis = ' High-priced stock - consider fractional shares.';
  }
  
  const summary = `${ticker} closed at $${priceVal.toFixed(2)}, ${changeVal >= 0 ? 'up' : 'down'} $${Math.abs(changeVal).toFixed(2)} (${data.percent}).${priceAnalysis}`;
  
  return { recommendation, reasoning, summary };
}

function updateCardWithAI(card, { recommendation, reasoning, summary }) {
  card.querySelector('.ai-analysis').innerHTML = `<strong>Analysis:</strong> ${summary}`;
  card.querySelector('.ai-recommend').innerHTML = `<strong>Recommendation:</strong> ${recommendation} - ${reasoning}`;
}
/**
 * Update the portfolio summary total value
 */
function updatePortfolioSummary() {
  const totalEl = document.getElementById('total-value');
  const changeEl = document.getElementById('total-change');
  const countEl = document.getElementById('stock-count');
  
  let total = 0;
  let totalChange = 0;
  let stockCount = 0;
  
  for (const ticker of watchlist) {
    const data = stockDataMap[ticker];
    if (data && typeof data.price === 'number') {
      total += data.price;
      totalChange += data.change || 0;
      stockCount++;
    }
  }
  
  totalEl.textContent = total.toFixed(2);
  
  const changePercent = total > 0 ? ((totalChange / (total - totalChange)) * 100).toFixed(2) : '0.00';
  changeEl.textContent = `$${totalChange.toFixed(2)} (${changePercent}%)`;
  changeEl.className = totalChange >= 0 ? 'positive' : 'negative';
  
  countEl.textContent = stockCount;
}

/**
 * Export watchlist data to CSV
 */
function exportCSV() {
  const headers = ['Ticker', 'Price', 'Change', 'Percent', 'Recommendation', 'Analysis'];
  const rows = [headers];
  for (const ticker of watchlist) {
    const data = stockDataMap[ticker];
    if (data) {
      rows.push([
        ticker,
        data.price,
        data.change,
        data.percent,
        data.recommendation || 'N/A',
        data.analysis || 'N/A'
      ]);
    }
  }
  const csvContent = rows.map(r => r.join(',')).join('\n');
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  downloadFile(blob, 'watchlist.csv');
  showNotification('CSV exported successfully!', 'success');
}

// ============== NEW FEATURES ==============

/**
 * Initialize theme functionality
 */
function initializeTheme() {
  document.documentElement.setAttribute('data-theme', currentTheme);
  const themeToggle = document.getElementById('theme-toggle');
  themeToggle.textContent = currentTheme === 'dark' ? '☀️' : '🌙';
  
  themeToggle.addEventListener('click', () => {
    currentTheme = currentTheme === 'light' ? 'dark' : 'light';
    document.documentElement.setAttribute('data-theme', currentTheme);
    localStorage.setItem('theme', currentTheme);
    themeToggle.textContent = currentTheme === 'dark' ? '☀️' : '🌙';
    showNotification(`Switched to ${currentTheme} theme`, 'info');
  });
}

/**
 * Initialize auto-refresh functionality
 */
function initializeAutoRefresh() {
  const refreshToggle = document.getElementById('auto-refresh-toggle');
  const refreshCheckbox = document.getElementById('auto-refresh-checkbox');
  const refreshInterval = document.getElementById('refresh-interval');
  
  // Load saved settings
  isAutoRefreshEnabled = localStorage.getItem('autoRefresh') === 'true';
  const savedInterval = localStorage.getItem('refreshInterval') || '30000';
  refreshInterval.value = savedInterval;
  refreshCheckbox.checked = isAutoRefreshEnabled;
  
  updateRefreshToggleButton();
  
  if (isAutoRefreshEnabled) {
    startAutoRefresh();
  }
  
  refreshToggle.addEventListener('click', toggleAutoRefresh);
  refreshCheckbox.addEventListener('change', (e) => {
    isAutoRefreshEnabled = e.target.checked;
    localStorage.setItem('autoRefresh', isAutoRefreshEnabled);
    if (isAutoRefreshEnabled) {
      startAutoRefresh();
    } else {
      stopAutoRefresh();
    }
    updateRefreshToggleButton();
  });
  
  refreshInterval.addEventListener('change', (e) => {
    localStorage.setItem('refreshInterval', e.target.value);
    if (isAutoRefreshEnabled) {
      stopAutoRefresh();
      startAutoRefresh();
    }
  });
}

function toggleAutoRefresh() {
  isAutoRefreshEnabled = !isAutoRefreshEnabled;
  document.getElementById('auto-refresh-checkbox').checked = isAutoRefreshEnabled;
  localStorage.setItem('autoRefresh', isAutoRefreshEnabled);
  
  if (isAutoRefreshEnabled) {
    startAutoRefresh();
  } else {
    stopAutoRefresh();
  }
  updateRefreshToggleButton();
}

function startAutoRefresh() {
  const interval = parseInt(document.getElementById('refresh-interval').value);
  autoRefreshInterval = setInterval(() => {
    refreshAllStocks();
  }, interval);
  showNotification('Auto-refresh enabled', 'success');
}

function stopAutoRefresh() {
  if (autoRefreshInterval) {
    clearInterval(autoRefreshInterval);
    autoRefreshInterval = null;
  }
  showNotification('Auto-refresh disabled', 'info');
}

function updateRefreshToggleButton() {
  const button = document.getElementById('auto-refresh-toggle');
  button.textContent = isAutoRefreshEnabled ? '⏸️' : '▶️';
  button.classList.toggle('active', isAutoRefreshEnabled);
}

async function refreshAllStocks() {
  console.log('Refreshing all stocks...');
  for (const ticker of watchlist) {
    try {
      const card = document.getElementById(`card-${ticker}`);
      if (card) {
        card.classList.add('loading');
        const data = await fetchStockData(ticker);
        stockDataMap[ticker] = data;
        updateCardWithData(card, data);
        const ai = await generateMockAIAnalysis(ticker, data);
        updateCardWithAI(card, ai);
        card.classList.remove('loading');
        
        // Check price alerts
        checkPriceAlerts(ticker, data);
      }
    } catch (error) {
      console.error(`Error refreshing ${ticker}:`, error);
    }
    // Small delay between requests
    await new Promise(resolve => setTimeout(resolve, 200));
  }
  updatePortfolioSummary();
}

/**
 * Initialize search suggestions
 */
function initializeSearchSuggestions() {
  const input = document.getElementById('ticker-input');
  const suggestions = document.getElementById('search-suggestions');
  
  // Popular stock symbols for suggestions
  const popularStocks = [
    'AAPL', 'GOOGL', 'MSFT', 'AMZN', 'TSLA', 'NVDA', 'META', 'NFLX',
    'DIS', 'BABA', 'V', 'JPM', 'JNJ', 'WMT', 'PG', 'UNH', 'HD', 'MA',
    'PYPL', 'ADBE', 'CRM', 'INTC', 'CSCO', 'PFE', 'VZ', 'KO', 'PEP',
    'T', 'XOM', 'CVX', 'BAC', 'WFC', 'C', 'GS', 'MS'
  ];
  
  input.addEventListener('input', (e) => {
    const value = e.target.value.toUpperCase();
    if (value.length > 0) {
      const matches = popularStocks.filter(stock => 
        stock.includes(value) && !watchlist.includes(stock)
      ).slice(0, 8);
      
      if (matches.length > 0) {
        showSuggestions(matches);
      } else {
        hideSuggestions();
      }
    } else {
      hideSuggestions();
    }
  });
  
  input.addEventListener('blur', () => {
    // Delay hiding to allow clicking on suggestions
    setTimeout(hideSuggestions, 200);
  });
}

function showSuggestions(stocks) {
  const suggestions = document.getElementById('search-suggestions');
  suggestions.innerHTML = '';
  
  stocks.forEach(stock => {
    const item = document.createElement('div');
    item.className = 'suggestion-item';
    item.textContent = stock;
    item.addEventListener('click', () => {
      document.getElementById('ticker-input').value = stock;
      addStock();
    });
    suggestions.appendChild(item);
  });
  
  suggestions.style.display = 'block';
}

function hideSuggestions() {
  document.getElementById('search-suggestions').style.display = 'none';
}

/**
 * Setup keyboard shortcuts
 */
function setupKeyboardShortcuts() {
  document.addEventListener('keydown', (e) => {
    // Ctrl/Cmd + Enter to add stock
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
      addStock();
    }
    
    // Escape to close panels
    if (e.key === 'Escape') {
      hideSuggestions();
      document.getElementById('settings-panel').classList.add('hidden');
      document.getElementById('alerts-panel').classList.add('hidden');
    }
    
    // Ctrl/Cmd + R to refresh all stocks
    if ((e.ctrlKey || e.metaKey) && e.key === 'r') {
      e.preventDefault();
      refreshAllStocks();
    }
  });
}

/**
 * Show notification
 */
function showNotification(message, type = 'info') {
  const notification = document.createElement('div');
  notification.className = 'notification';
  notification.textContent = message;
  
  // Color based on type
  const colors = {
    success: 'var(--success-color)',
    error: 'var(--danger-color)',
    warning: 'var(--warning-color)',
    info: 'var(--primary-color)'
  };
  
  notification.style.backgroundColor = colors[type] || colors.info;
  
  document.body.appendChild(notification);
  
  // Play sound if enabled
  if (localStorage.getItem('soundNotifications') === 'true') {
    playNotificationSound();
  }
  
  // Remove after 3 seconds
  setTimeout(() => {
    notification.remove();
  }, 3000);
}

/**
 * Play notification sound
 */
function playNotificationSound() {
  // Create a simple beep sound using Web Audio API
  try {
    const audioContext = new (window.AudioContext || window.webkitAudioContext)();
    const oscillator = audioContext.createOscillator();
    const gainNode = audioContext.createGain();
    
    oscillator.connect(gainNode);
    gainNode.connect(audioContext.destination);
    
    oscillator.frequency.value = 800;
    oscillator.type = 'sine';
    
    gainNode.gain.setValueAtTime(0.1, audioContext.currentTime);
    gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 0.1);
    
    oscillator.start(audioContext.currentTime);
    oscillator.stop(audioContext.currentTime + 0.1);
  } catch (error) {
    console.log('Audio not supported');
  }
}

/**
 * Setup additional event listeners
 */
document.addEventListener('DOMContentLoaded', () => {
  // Settings toggle
  document.getElementById('settings-toggle').addEventListener('click', () => {
    const panel = document.getElementById('settings-panel');
    panel.classList.toggle('hidden');
  });
  
  // Clear all button
  document.getElementById('clear-all').addEventListener('click', () => {
    if (confirm('Are you sure you want to remove all stocks from your watchlist?')) {
      watchlist = [];
      localStorage.setItem(WATCHLIST_KEY, JSON.stringify(watchlist));
      document.getElementById('watchlist').innerHTML = '';
      stockDataMap = {};
      updatePortfolioSummary();
      showNotification('Watchlist cleared', 'info');
    }
  });
  
  // Export JSON
  document.getElementById('export-json').addEventListener('click', () => {
    const data = {
      watchlist,
      stockData: stockDataMap,
      exportDate: new Date().toISOString()
    };
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    downloadFile(blob, 'watchlist.json');
    showNotification('JSON exported successfully!', 'success');
  });
  
  // Import data
  document.getElementById('import-data').addEventListener('click', () => {
    document.getElementById('import-file').click();
  });
  
  document.getElementById('import-file').addEventListener('change', handleFileImport);
  
  // Sound notifications toggle
  document.getElementById('sound-notifications').addEventListener('change', (e) => {
    localStorage.setItem('soundNotifications', e.target.checked);
  });
  
  // Load sound setting
  document.getElementById('sound-notifications').checked = 
    localStorage.getItem('soundNotifications') === 'true';
});

/**
 * Download file helper
 */
function downloadFile(blob, filename) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.setAttribute('href', url);
  link.setAttribute('download', filename);
  link.style.visibility = 'hidden';
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Handle file import
 */
function handleFileImport(event) {
  const file = event.target.files[0];
  if (!file) return;
  
  const reader = new FileReader();
  reader.onload = (e) => {
    try {
      const data = JSON.parse(e.target.result);
      if (data.watchlist && Array.isArray(data.watchlist)) {
        watchlist = [...new Set([...watchlist, ...data.watchlist])]; // Merge and deduplicate
        localStorage.setItem(WATCHLIST_KEY, JSON.stringify(watchlist));
        
        // Clear current display and reload
        document.getElementById('watchlist').innerHTML = '';
        loadWatchlist();
        
        showNotification('Data imported successfully!', 'success');
      } else {
        throw new Error('Invalid file format');
      }
    } catch (error) {
      showNotification('Error importing file: ' + error.message, 'error');
    }
  };
  reader.readAsText(file);
  
  // Reset file input
  event.target.value = '';
}

/**
 * Price alerts functionality
 */
function checkPriceAlerts(ticker, data) {
  const alerts = priceAlerts[ticker];
  if (!alerts) return;
  
  const price = data.price;
  alerts.forEach((alert, index) => {
    let triggered = false;
    
    if (alert.type === 'above' && price >= alert.price) {
      triggered = true;
    } else if (alert.type === 'below' && price <= alert.price) {
      triggered = true;
    }
    
    if (triggered) {
      showNotification(
        `🚨 Price Alert: ${ticker} is ${alert.type} $${alert.price} (Current: $${price})`,
        'warning'
      );
      
      // Remove triggered alert
      alerts.splice(index, 1);
      if (alerts.length === 0) {
        delete priceAlerts[ticker];
      }
      localStorage.setItem('priceAlerts', JSON.stringify(priceAlerts));
      
      // Update UI
      updateAlertIndicators();
    }
  });
}

function updateAlertIndicators() {
  watchlist.forEach(ticker => {
    const card = document.getElementById(`card-${ticker}`);
    const existing = card.querySelector('.alert-indicator');
    
    if (priceAlerts[ticker] && priceAlerts[ticker].length > 0) {
      if (!existing) {
        const indicator = document.createElement('div');
        indicator.className = 'alert-indicator';
        indicator.textContent = '🔔';
        indicator.title = `${priceAlerts[ticker].length} alert(s) set`;
        card.appendChild(indicator);
      }
    } else if (existing) {
      existing.remove();
    }
  });
}
