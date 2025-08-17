using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiStockTradeApp.DataAccess.Repositories
{
    public class ListedStockRepository : IListedStockRepository
    {
        private readonly StockDataContext _db;
        public ListedStockRepository(StockDataContext db) => _db = db;

        public async Task<ListedStock?> GetBySymbolAsync(string symbol)
        {
            return await _db.ListedStocks.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Symbol == symbol);
        }

        public async Task UpsertAsync(ListedStock stock)
        {
            var existing = await _db.ListedStocks.FirstOrDefaultAsync(s => s.Symbol == stock.Symbol);
            if (existing == null)
            {
                _db.ListedStocks.Add(stock);
            }
            else
            {
                existing.Name = stock.Name;
                existing.LastSale = stock.LastSale;
                existing.NetChange = stock.NetChange;
                existing.PercentChange = stock.PercentChange;
                existing.MarketCap = stock.MarketCap;
                existing.Country = stock.Country;
                existing.IpoYear = stock.IpoYear;
                existing.Volume = stock.Volume;
                existing.Sector = stock.Sector;
                existing.Industry = stock.Industry;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
        }

        public async Task BulkUpsertAsync(IEnumerable<ListedStock> stocks)
        {
            foreach (var s in stocks)
            {
                await UpsertAsync(s);
            }
        }

        public async Task<List<ListedStock>> GetAllAsync(int skip = 0, int take = 500)
        {
            return await _db.ListedStocks.AsNoTracking()
                .OrderBy(s => s.Symbol)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> CountAsync() => await _db.ListedStocks.CountAsync();

        public async Task DeleteAllAsync()
        {
            _db.ListedStocks.RemoveRange(_db.ListedStocks);
            await _db.SaveChangesAsync();
        }

        public async Task<List<ListedStock>> SearchAsync(string? sector, string? industry, int skip = 0, int take = 500)
        {
            var qry = _db.ListedStocks.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(sector))
            {
                var s = sector.Trim();
                qry = qry.Where(x => x.Sector != null && x.Sector == s);
            }
            if (!string.IsNullOrWhiteSpace(industry))
            {
                var i = industry.Trim();
                qry = qry.Where(x => x.Industry != null && x.Industry == i);
            }
            return await qry.OrderBy(x => x.Symbol).Skip(Math.Max(0, skip)).Take(take <= 0 || take > 2000 ? 500 : take).ToListAsync();
        }
    }
}
