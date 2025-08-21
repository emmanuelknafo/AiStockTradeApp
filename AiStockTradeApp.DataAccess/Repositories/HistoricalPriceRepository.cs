using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiStockTradeApp.DataAccess.Repositories
{
    public class HistoricalPriceRepository : IHistoricalPriceRepository
    {
        private readonly StockDataContext _db;
        public HistoricalPriceRepository(StockDataContext db) => _db = db;

        public async Task UpsertAsync(HistoricalPrice price)
        {
            var existing = await _db.HistoricalPrices
                .FirstOrDefaultAsync(p => p.Symbol == price.Symbol && p.Date == price.Date);
            if (existing == null)
            {
                _db.HistoricalPrices.Add(price);
            }
            else
            {
                existing.Open = price.Open;
                existing.High = price.High;
                existing.Low = price.Low;
                existing.Close = price.Close;
                existing.Volume = price.Volume;
                existing.Source = price.Source;
                existing.ImportedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
        }

        public async Task UpsertManyAsync(IEnumerable<HistoricalPrice> prices)
        {
            foreach (var p in prices)
            {
                await UpsertAsync(p);
            }
        }

        public async Task<List<HistoricalPrice>> GetAsync(string symbol, DateTime? from = null, DateTime? to = null, int? take = null)
        {
            var q = _db.HistoricalPrices.AsNoTracking().Where(p => p.Symbol == symbol);
            if (from.HasValue) q = q.Where(p => p.Date >= from.Value);
            if (to.HasValue) q = q.Where(p => p.Date <= to.Value);
            q = q.OrderByDescending(p => p.Date);
            if (take.HasValue && take.Value > 0) q = q.Take(take.Value);
            return await q.ToListAsync();
        }

        public async Task<long> CountAsync(string? symbol = null)
        {
            var q = _db.HistoricalPrices.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(symbol))
                q = q.Where(p => p.Symbol == symbol);
            return await q.LongCountAsync();
        }

        public async Task DeleteBySymbolAsync(string symbol)
        {
            var items = _db.HistoricalPrices.Where(p => p.Symbol == symbol);
            _db.HistoricalPrices.RemoveRange(items);
            await _db.SaveChangesAsync();
        }
    }
}
