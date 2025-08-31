// AI Stock Tracker MVC JavaScript
// Frontend functionality for the MVC stock tracker application

class StockTracker {
    constructor() {
        this.config = window.stockTrackerConfig || {};
        this.autoRefreshInterval = null;
        this.isAutoRefreshEnabled = this.config.autoRefresh || false;
        this.currentTheme = this.config.theme || 'light';
        this.charts = new Map(); // Store chart instances
        
        this.init();
    }

    init() {
        try {
            this.setupEventListeners();
            this.initializeTheme();
            this.initializeAutoRefresh();
            this.initializeSearchSuggestions();
            this.setupKeyboardShortcuts();
            this.loadSettings();
            
            // Initialize charts after everything else is ready
            setTimeout(() => {
                console.log('Delayed chart initialization...');
                this.initializeCharts();
            }, 100);
        } catch (error) {
            console.error('Error during StockTracker initialization:', error);
            // Continue without charts if there's an initialization error
            this.showNotification('Application initialized with limited functionality', 'info');
        }
    }

    setupEventListeners() {
        // Add stock button
        document.getElementById('add-button')?.addEventListener('click', () => this.addStock());
        
        // Remove stock buttons
        document.querySelectorAll('.remove-button').forEach(btn => {
            btn.addEventListener('click', (e) => this.removeStock(e.target.dataset.ticker));
        });
        
        // Clear all button
        document.getElementById('clear-all')?.addEventListener('click', () => this.clearWatchlist());
        
        // Settings toggle
        document.getElementById('settings-toggle')?.addEventListener('click', () => this.toggleSettings());
        
        // Alerts toggle
        document.getElementById('alerts-toggle')?.addEventListener('click', () => this.toggleAlerts());
        
        // Theme toggle
        document.getElementById('theme-toggle')?.addEventListener('click', () => this.toggleTheme());
        
        // Auto-refresh toggle
        document.getElementById('auto-refresh-toggle')?.addEventListener('click', () => this.toggleAutoRefresh());
        
        // Settings checkboxes
        document.getElementById('auto-refresh-checkbox')?.addEventListener('change', (e) => {
            this.isAutoRefreshEnabled = e.target.checked;
            this.saveSettings();
            if (this.isAutoRefreshEnabled) {
                this.startAutoRefresh();
            } else {
                this.stopAutoRefresh();
            }
            this.updateRefreshToggleButton();
        });
        
        document.getElementById('refresh-interval')?.addEventListener('change', (e) => {
            this.config.refreshInterval = parseInt(e.target.value);
            this.saveSettings();
            if (this.isAutoRefreshEnabled) {
                this.stopAutoRefresh();
                this.startAutoRefresh();
            }
        });
        
        // Sound notifications
        document.getElementById('sound-notifications')?.addEventListener('change', (e) => {
            this.config.soundNotifications = e.target.checked;
            this.saveSettings();
        });
        
        // Show charts
        document.getElementById('show-charts')?.addEventListener('change', (e) => {
            this.config.showCharts = e.target.checked;
            this.saveSettings();
            // Reload page to apply chart visibility
            window.location.reload();
        });
        
        // Import data
        document.getElementById('import-data')?.addEventListener('click', () => {
            document.getElementById('import-file')?.click();
        });
        
        document.getElementById('import-file')?.addEventListener('change', (e) => this.handleFileImport(e));
        
        // Double-click on stock cards to set alerts
        document.querySelectorAll('.stock-card').forEach(card => {
            card.addEventListener('dblclick', () => {
                const symbol = card.id.replace('card-', '');
                this.showSetAlertDialog(symbol);
            });
        });
    }

    async addStock() {
        const input = document.getElementById('ticker-input');
        const symbol = input.value.trim().toUpperCase();
        
        if (!symbol) {
            this.showNotification('Please enter a stock symbol', 'error');
            return;
        }
        
        try {
            const response = await fetch('/Stock/AddStock', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': this.getAntiForgeryToken()
                },
                body: JSON.stringify({ symbol: symbol })
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.showNotification(result.message, 'success');
                input.value = '';
                this.hideSuggestions();
                // Reload page to show new stock
                setTimeout(() => window.location.reload(), 1000);
            } else {
                this.showNotification(result.message, 'error');
            }
        } catch (error) {
            console.error('Error adding stock:', error);
            this.showNotification('Error adding stock', 'error');
        }
    }

    async removeStock(symbol) {
        if (!symbol) return;
        
        try {
            const response = await fetch(`/Stock/RemoveStock?symbol=${encodeURIComponent(symbol)}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': this.getAntiForgeryToken()
                }
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.showNotification(result.message, 'success');
                // Remove card from DOM and destroy chart
                const card = document.getElementById(`card-${symbol}`);
                if (card) {
                    this.destroyChart(symbol);
                    card.remove();
                }
                this.updatePortfolioDisplay();
            } else {
                this.showNotification(result.message, 'error');
            }
        } catch (error) {
            console.error('Error removing stock:', error);
            this.showNotification('Error removing stock', 'error');
        }
    }

    async clearWatchlist() {
        if (!confirm('Are you sure you want to remove all stocks from your watchlist?')) {
            return;
        }
        
        try {
            const response = await fetch('/Stock/ClearWatchlist', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': this.getAntiForgeryToken()
                }
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.showNotification(result.message, 'info');
                // Destroy all charts before reload
                this.destroyAllCharts();
                // Reload page to clear watchlist
                setTimeout(() => window.location.reload(), 1000);
            } else {
                this.showNotification(result.message, 'error');
            }
        } catch (error) {
            console.error('Error clearing watchlist:', error);
            this.showNotification('Error clearing watchlist', 'error');
        }
    }

    initializeTheme() {
        document.documentElement.setAttribute('data-theme', this.currentTheme);
        const themeToggle = document.getElementById('theme-toggle');
        if (themeToggle) {
            themeToggle.textContent = this.currentTheme === 'dark' ? '☀️' : '🌙';
        }
    }

    toggleTheme() {
        this.currentTheme = this.currentTheme === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', this.currentTheme);
        const themeToggle = document.getElementById('theme-toggle');
        if (themeToggle) {
            themeToggle.textContent = this.currentTheme === 'dark' ? '☀️' : '🌙';
        }
        this.config.theme = this.currentTheme;
        this.saveSettings();
        this.showNotification(`Switched to ${this.currentTheme} theme`, 'info');
    }

    initializeAutoRefresh() {
        this.updateRefreshToggleButton();
        if (this.isAutoRefreshEnabled) {
            this.startAutoRefresh();
        }
    }

    toggleAutoRefresh() {
        this.isAutoRefreshEnabled = !this.isAutoRefreshEnabled;
        const checkbox = document.getElementById('auto-refresh-checkbox');
        if (checkbox) {
            checkbox.checked = this.isAutoRefreshEnabled;
        }
        
        if (this.isAutoRefreshEnabled) {
            this.startAutoRefresh();
        } else {
            this.stopAutoRefresh();
        }
        this.updateRefreshToggleButton();
        this.saveSettings();
    }

    startAutoRefresh() {
        const interval = this.config.refreshInterval || 30000;
        this.autoRefreshInterval = setInterval(() => {
            this.refreshAllStocks();
        }, interval);
        this.showNotification('Auto-refresh enabled', 'success');
    }

    stopAutoRefresh() {
        if (this.autoRefreshInterval) {
            clearInterval(this.autoRefreshInterval);
            this.autoRefreshInterval = null;
        }
        this.showNotification('Auto-refresh disabled', 'info');
    }

    updateRefreshToggleButton() {
        const button = document.getElementById('auto-refresh-toggle');
        if (button) {
            button.textContent = this.isAutoRefreshEnabled ? '⏸️' : '▶️';
            button.classList.toggle('active', this.isAutoRefreshEnabled);
        }
    }

    async refreshAllStocks() {
        try {
            const response = await fetch('/Stock/RefreshAll');
            const result = await response.json();
            
            if (result.success) {
                console.log('All stocks refreshed');
                // Update the display with new data
                if (result.watchlist && result.portfolio) {
                    this.updateWatchlistDisplay(result.watchlist);
                    this.updatePortfolioDisplay(result.portfolio);
                }
            } else {
                console.error('Error refreshing stocks:', result.message);
            }
        } catch (error) {
            console.error('Error refreshing stocks:', error);
        }
    }

    initializeSearchSuggestions() {
        const input = document.getElementById('ticker-input');
        if (!input) return;
        
        input.addEventListener('input', async (e) => {
            const value = e.target.value.trim();
            if (value.length > 0) {
                try {
                    const response = await fetch(`/Stock/GetSuggestions?query=${encodeURIComponent(value)}`);
                    const suggestions = await response.json();
                    
                    if (suggestions.length > 0) {
                        this.showSuggestions(suggestions);
                    } else {
                        this.hideSuggestions();
                    }
                } catch (error) {
                    console.error('Error getting suggestions:', error);
                    this.hideSuggestions();
                }
            } else {
                this.hideSuggestions();
            }
        });
        
        input.addEventListener('blur', () => {
            // Delay hiding to allow clicking on suggestions
            setTimeout(() => this.hideSuggestions(), 200);
        });
    }

    showSuggestions(suggestions) {
        const container = document.getElementById('search-suggestions');
        if (!container) return;
        
        container.innerHTML = '';
        
        suggestions.forEach(symbol => {
            const item = document.createElement('div');
            item.className = 'suggestion-item';
            item.textContent = symbol;
            item.addEventListener('click', () => {
                document.getElementById('ticker-input').value = symbol;
                this.addStock();
            });
            container.appendChild(item);
        });
        
        container.style.display = 'block';
    }

    hideSuggestions() {
        const container = document.getElementById('search-suggestions');
        if (container) {
            container.style.display = 'none';
        }
    }

    setupKeyboardShortcuts() {
        document.addEventListener('keydown', (e) => {
            // Ctrl/Cmd + Enter to add stock
            if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                this.addStock();
            }
            
            // Escape to close panels
            if (e.key === 'Escape') {
                this.hideSuggestions();
                this.hidePanel('settings-panel');
                this.hidePanel('alerts-panel');
            }
            
            // Ctrl/Cmd + R to refresh all stocks
            if ((e.ctrlKey || e.metaKey) && e.key === 'r') {
                e.preventDefault();
                this.refreshAllStocks();
            }
        });
    }

    toggleSettings() {
        this.togglePanel('settings-panel');
    }

    toggleAlerts() {
        this.togglePanel('alerts-panel');
    }

    togglePanel(panelId) {
        const panel = document.getElementById(panelId);
        if (panel) {
            panel.classList.toggle('hidden');
        }
    }

    hidePanel(panelId) {
        const panel = document.getElementById(panelId);
        if (panel) {
            panel.classList.add('hidden');
        }
    }

    showSetAlertDialog(symbol) {
        const stockCard = document.getElementById(`card-${symbol}`);
        if (!stockCard) return;
        
        const priceElement = stockCard.querySelector('.price span');
        const currentPrice = priceElement ? priceElement.textContent.replace('$', '') : 'Loading...';
        
        const targetPrice = prompt(
            `Set price alert for ${symbol}. Current price: $${currentPrice}\n\nEnter target price:`
        );
        
        if (targetPrice && !isNaN(targetPrice)) {
            this.setAlert(symbol, parseFloat(targetPrice));
        }
    }

    async setAlert(symbol, targetPrice) {
        try {
            const response = await fetch('/Stock/SetAlert', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': this.getAntiForgeryToken()
                },
                body: JSON.stringify({ symbol: symbol, targetPrice: targetPrice })
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.showNotification(result.message, 'success');
                // Add alert indicator to stock card
                this.addAlertIndicator(symbol);
            } else {
                this.showNotification(result.message, 'error');
            }
        } catch (error) {
            console.error('Error setting alert:', error);
            this.showNotification('Error setting price alert', 'error');
        }
    }

    addAlertIndicator(symbol) {
        const card = document.getElementById(`card-${symbol}`);
        if (!card || card.querySelector('.alert-indicator')) return;
        
        const indicator = document.createElement('div');
        indicator.className = 'alert-indicator';
        indicator.textContent = '🔔';
        indicator.title = 'Price alert set';
        card.appendChild(indicator);
    }

    async handleFileImport(event) {
        const file = event.target.files[0];
        if (!file) return;
        
        try {
            const formData = new FormData();
            formData.append('file', file);
            
            const response = await fetch('/Stock/ImportData', {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': this.getAntiForgeryToken()
                },
                body: formData
            });
            
            const result = await response.json();
            
            if (result.success) {
                this.showNotification(result.message, 'success');
                // Reload page to show imported data
                setTimeout(() => window.location.reload(), 1000);
            } else {
                this.showNotification(result.message, 'error');
            }
        } catch (error) {
            console.error('Error importing file:', error);
            this.showNotification('Error importing file', 'error');
        }
        
        // Reset file input
        event.target.value = '';
    }

    updateWatchlistDisplay(watchlist) {
        // Update individual stock cards with new data
        watchlist.forEach(item => {
            if (item.stockData) {
                this.updateStockCard(item.symbol, item.stockData);
                // Update chart if charts are enabled
                if (this.config.showCharts) {
                    this.updateChart(item.symbol);
                }
            }
        });
    }

    updateStockCard(symbol, stockData) {
        const card = document.getElementById(`card-${symbol}`);
        if (!card) return;
        
        const priceSpan = card.querySelector('.price span');
        const changeSpan = card.querySelector('.change span');
        const percentSpan = card.querySelector('.percent span');
        const analysisDiv = card.querySelector('.ai-analysis');
        const recommendDiv = card.querySelector('.ai-recommend');
        
        if (priceSpan) priceSpan.textContent = `$${stockData.price.toFixed(2)}`;
        if (changeSpan) changeSpan.textContent = `${stockData.change >= 0 ? '+' : ''}${stockData.change.toFixed(2)}`;
        if (percentSpan) percentSpan.textContent = stockData.percentChange;
        
        // Update classes for positive/negative changes
        const changeP = card.querySelector('.change');
        const percentP = card.querySelector('.percent');
        if (changeP) {
            changeP.className = stockData.change >= 0 ? 'change positive' : 'change negative';
        }
        if (percentP) {
            percentP.className = stockData.change >= 0 ? 'percent positive' : 'percent negative';
        }
        
        if (analysisDiv && stockData.aiAnalysis) {
            analysisDiv.innerHTML = `<strong>Analysis:</strong> ${stockData.aiAnalysis}`;
        }
        if (recommendDiv && stockData.recommendation) {
            recommendDiv.innerHTML = `<strong>Recommendation:</strong> ${stockData.recommendation} - ${stockData.recommendationReason || ''}`;
        }
    }

    updatePortfolioDisplay(portfolio) {
        if (!portfolio) return;
        
        const totalValueSpan = document.getElementById('total-value');
        const totalChangeSpan = document.getElementById('total-change');
        const stockCountSpan = document.getElementById('stock-count');
        
        if (totalValueSpan) totalValueSpan.textContent = portfolio.totalValue.toFixed(2);
        if (stockCountSpan) stockCountSpan.textContent = portfolio.stockCount.toString();
        
        if (totalChangeSpan) {
            const prefix = portfolio.totalChange >= 0 ? '+' : '';
            totalChangeSpan.textContent = `${prefix}$${portfolio.totalChange.toFixed(2)} (${portfolio.totalChangePercent.toFixed(2)}%)`;
            totalChangeSpan.className = portfolio.totalChange >= 0 ? 'stat-value positive' : 'stat-value negative';
        }
    }

    showNotification(message, type = 'info') {
        const notification = document.createElement('div');
        notification.className = `notification ${type}`;
        notification.textContent = message;
        
        document.body.appendChild(notification);
        
        // Play sound if enabled
        if (this.config.soundNotifications) {
            this.playNotificationSound();
        }
        
        // Remove after 3 seconds
        setTimeout(() => {
            notification.remove();
        }, 3000);
    }

    playNotificationSound() {
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

    getAntiForgeryToken() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        return token ? token.value : '';
    }

    saveSettings() {
        localStorage.setItem('stockTracker_settings', JSON.stringify({
            autoRefresh: this.isAutoRefreshEnabled,
            refreshInterval: this.config.refreshInterval,
            soundNotifications: this.config.soundNotifications,
            showCharts: this.config.showCharts,
            theme: this.currentTheme
        }));
    }

    loadSettings() {
        try {
            const saved = localStorage.getItem('stockTracker_settings');
            if (saved) {
                const settings = JSON.parse(saved);
                this.isAutoRefreshEnabled = settings.autoRefresh ?? false;
                this.config.refreshInterval = settings.refreshInterval ?? this.config.refreshInterval;
                this.config.soundNotifications = settings.soundNotifications ?? this.config.soundNotifications;
                this.config.showCharts = settings.showCharts ?? this.config.showCharts;
                this.currentTheme = settings.theme ?? this.currentTheme;
                
                // Update UI elements
                const autoRefreshCheckbox = document.getElementById('auto-refresh-checkbox');
                if (autoRefreshCheckbox) autoRefreshCheckbox.checked = this.isAutoRefreshEnabled;
                
                const refreshInterval = document.getElementById('refresh-interval');
                if (refreshInterval) refreshInterval.value = this.config.refreshInterval;
                
                const soundNotifications = document.getElementById('sound-notifications');
                if (soundNotifications) soundNotifications.checked = this.config.soundNotifications;
                
                const showCharts = document.getElementById('show-charts');
                if (showCharts) showCharts.checked = this.config.showCharts;
            }
        } catch (error) {
            console.error('Error loading settings:', error);
        }
    }

    // Chart Management Methods
    initializeCharts() {
        try {
            console.log('initializeCharts called - config.showCharts:', this.config.showCharts);
            console.log('Current config:', this.config);
            
            if (!this.config.showCharts) {
                console.log('Charts disabled in config');
                return;
            }
            
            // Check if Chart.js is available
            if (typeof Chart === 'undefined') {
                console.warn('Chart.js not loaded - charts will not be displayed');
                return;
            }
            
            console.log('Chart.js is available, looking for stock cards...');
            
            // Initialize charts for all existing stock cards
            const stockCards = document.querySelectorAll('.stock-card');
            console.log('Found', stockCards.length, 'stock cards');
            
            stockCards.forEach(card => {
                try {
                    const symbol = card.id.replace('card-', '');
                    const canvas = card.querySelector(`canvas[id="chart-${symbol}"]`);
                    console.log(`Card ${symbol}: canvas found =`, !!canvas, canvas);
                    
                    if (canvas && !this.charts.has(symbol)) {
                        console.log(`Creating chart for ${symbol}`);
                        this.createChart(symbol, canvas);
                    } else if (this.charts.has(symbol)) {
                        console.log(`Chart for ${symbol} already exists`);
                    } else if (!canvas) {
                        console.log(`No canvas found for ${symbol} - ShowCharts might be disabled or canvas not rendered`);
                    }
                } catch (error) {
                    console.error(`Error initializing chart for card:`, error);
                    // Continue with other charts
                }
            });
        } catch (error) {
            console.error('Error in initializeCharts:', error);
            // Charts failed to initialize, but app should continue working
        }
    }
    
    // Method to manually trigger chart initialization (for debugging)
    forceInitializeCharts() {
        console.log('Force initializing charts...');
        this.charts.clear();
        this.initializeCharts();
    }

    async createChart(symbol, canvas) {
        try {
            console.log(`Creating chart for ${symbol}...`);
            
            // Check if Chart.js is available
            if (typeof Chart === 'undefined') {
                console.warn('Chart.js not available - skipping chart creation for', symbol);
                return;
            }

            const chartData = await this.fetchChartData(symbol);
            console.log(`Chart data for ${symbol}:`, chartData.length, 'points');
            
            if (chartData.length === 0) {
                console.warn(`No chart data available for ${symbol}`);
                return;
            }

            const ctx = canvas.getContext('2d');
            
            // Destroy existing chart if it exists
            if (this.charts.has(symbol)) {
                this.charts.get(symbol).destroy();
            }

            const chart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: chartData.map(point => {
                        const date = new Date(point.date);
                        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
                    }),
                    datasets: [{
                        label: symbol,
                        data: chartData.map(point => point.price),
                        borderColor: this.getChartColor(symbol),
                        backgroundColor: this.getChartColor(symbol, 0.1),
                        borderWidth: 2,
                        fill: true,
                        tension: 0.4,
                        pointRadius: 0,
                        pointHoverRadius: 4
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            mode: 'index',
                            intersect: false,
                            callbacks: {
                                label: function(context) {
                                    return `$${context.parsed.y.toFixed(2)}`;
                                }
                            }
                        }
                    },
                    scales: {
                        x: {
                            display: false
                        },
                        y: {
                            display: false,
                            beginAtZero: false
                        }
                    },
                    interaction: {
                        intersect: false,
                        mode: 'index'
                    },
                    elements: {
                        point: {
                            radius: 0
                        }
                    }
                }
            });

            this.charts.set(symbol, chart);
        } catch (error) {
            console.error(`Error creating chart for ${symbol}:`, error);
            // Show fallback text when chart fails
            const canvas = document.getElementById(`chart-${symbol}`);
            if (canvas) {
                const container = canvas.parentElement;
                if (container) {
                    container.innerHTML = '<div class="mini-chart-placeholder">📈 Chart unavailable</div>';
                }
            }
        }
    }

    async fetchChartData(symbol, days = 30) {
        try {
            console.log(`Fetching chart data for ${symbol}...`);
            const response = await fetch(`/Stock/GetChartData?symbol=${encodeURIComponent(symbol)}&days=${days}`);
            const result = await response.json();
            
            console.log(`Chart data response for ${symbol}:`, result);
            
            if (result.success) {
                console.log(`Successfully fetched ${result.data.length} data points for ${symbol}`);
                return result.data;
            } else {
                console.error(`Error fetching chart data for ${symbol}:`, result.message);
                return [];
            }
        } catch (error) {
            console.error(`Error fetching chart data for ${symbol}:`, error);
            return [];
        }
    }

    getChartColor(symbol, alpha = 1) {
        // Generate consistent colors based on symbol
        const colors = [
            `rgba(54, 162, 235, ${alpha})`,   // Blue
            `rgba(255, 99, 132, ${alpha})`,   // Red
            `rgba(75, 192, 192, ${alpha})`,   // Teal
            `rgba(255, 206, 86, ${alpha})`,   // Yellow
            `rgba(153, 102, 255, ${alpha})`,  // Purple
            `rgba(255, 159, 64, ${alpha})`,   // Orange
            `rgba(199, 199, 199, ${alpha})`,  // Grey
            `rgba(83, 102, 255, ${alpha})`    // Indigo
        ];
        
        const hash = symbol.split('').reduce((a, b) => {
            a = ((a << 5) - a) + b.charCodeAt(0);
            return a & a;
        }, 0);
        
        return colors[Math.abs(hash) % colors.length];
    }

    updateChart(symbol) {
        if (!this.charts.has(symbol)) return;
        
        // Refresh chart data
        this.fetchChartData(symbol).then(chartData => {
            if (chartData.length === 0) return;
            
            const chart = this.charts.get(symbol);
            chart.data.labels = chartData.map(point => {
                const date = new Date(point.date);
                return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
            });
            chart.data.datasets[0].data = chartData.map(point => point.price);
            chart.update('none'); // Update without animation
        });
    }

    destroyChart(symbol) {
        if (this.charts.has(symbol)) {
            this.charts.get(symbol).destroy();
            this.charts.delete(symbol);
        }
    }

    destroyAllCharts() {
        this.charts.forEach((chart, symbol) => {
            chart.destroy();
        });
        this.charts.clear();
    }
}

// Initialize the app when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    console.log('DOM loaded, initializing StockTracker...');
    const stockTracker = new StockTracker();
    
    // Make it globally available for debugging
    window.stockTracker = stockTracker;
    
    console.log('StockTracker initialized, available as window.stockTracker');
});
