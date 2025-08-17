using AiStockTradeApp.Entities;

namespace AiStockTradeApp.Services.Interfaces
{
    public interface IListedStockService
    {
        Task UpsertAsync(ListedStock stock);
        Task BulkUpsertAsync(IEnumerable<ListedStock> stocks);
        Task<ListedStock?> GetAsync(string symbol);
        Task<List<ListedStock>> GetAllAsync(int skip = 0, int take = 500);
    Task<List<ListedStock>> SearchAsync(string? sector, string? industry, int skip = 0, int take = 500);
        Task<int> CountAsync();
        Task DeleteAllAsync();
    }
}
