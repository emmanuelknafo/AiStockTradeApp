using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Interfaces;

namespace AiStockTradeApp.Services.Implementations
{
    public class ListedStockService : IListedStockService
    {
        private readonly IListedStockRepository _repo;
        public ListedStockService(IListedStockRepository repo) => _repo = repo;

        public Task UpsertAsync(ListedStock stock) => _repo.UpsertAsync(stock);
        public Task BulkUpsertAsync(IEnumerable<ListedStock> stocks) => _repo.BulkUpsertAsync(stocks);
        public Task<ListedStock?> GetAsync(string symbol) => _repo.GetBySymbolAsync(symbol.ToUpper());
    public Task<List<ListedStock>> GetAllAsync(int skip = 0, int take = 500) => _repo.GetAllAsync(skip, take);
    public Task<List<ListedStock>> SearchAsync(string? sector, string? industry, string? q, int skip = 0, int take = 500) => _repo.SearchAsync(sector, industry, q, skip, take);
        public Task<int> CountAsync() => _repo.CountAsync();
    public Task<int> SearchCountAsync(string? sector, string? industry, string? q) => _repo.SearchCountAsync(sector, industry, q);
    public Task<List<string>> GetDistinctSectorsAsync() => _repo.GetDistinctSectorsAsync();
    public Task<List<string>> GetDistinctIndustriesAsync() => _repo.GetDistinctIndustriesAsync();
        public Task DeleteAllAsync() => _repo.DeleteAllAsync();
    }
}
