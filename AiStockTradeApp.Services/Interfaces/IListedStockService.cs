using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Services.Interfaces
{
    public interface IListedStockService
    {
        Task UpsertAsync(ListedStock stock);
        Task BulkUpsertAsync(IEnumerable<ListedStock> stocks);
        Task<ListedStock?> GetAsync(string symbol);
        Task<List<ListedStock>> GetAllAsync(int skip = 0, int take = 500);
    Task<List<ListedStock>> SearchAsync(string? sector, string? industry, string? q, int skip = 0, int take = 500);
        Task<int> CountAsync();
        Task<int> SearchCountAsync(string? sector, string? industry, string? q);
        Task<List<string>> GetDistinctSectorsAsync();
        Task<List<string>> GetDistinctIndustriesAsync();
        Task DeleteAllAsync();
    }
}
