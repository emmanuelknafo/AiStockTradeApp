using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace AiStockTradeApp.Controllers
{
    public class ListedStocksController : Controller
    {
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly string _primaryBaseUrl;
    private readonly string _fallbackBaseUrl;

        public ListedStocksController(IHttpClientFactory factory, IConfiguration config)
        {
            _config = config;
            _http = factory.CreateClient();
            _primaryBaseUrl = _config["StockApi:BaseUrl"] ?? "https://localhost:5001";
            _fallbackBaseUrl = _config["StockApi:HttpBaseUrl"] ?? "http://localhost:5256";
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Data(string? q, string? sector, string? industry, int page = 1, int pageSize = 50)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 || pageSize > 200 ? 50 : pageSize;
            var skip = (page - 1) * pageSize;
            var rel = $"/api/listed-stocks/search?q={Uri.EscapeDataString(q ?? string.Empty)}&sector={Uri.EscapeDataString(sector ?? string.Empty)}&industry={Uri.EscapeDataString(industry ?? string.Empty)}&skip={skip}&take={pageSize}";
            var result = await GetWithFallbackAsync<SearchResponse>(rel) ?? new SearchResponse { total = 0, items = new List<SearchItem>() };
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> Facets()
        {
            var sectors = await GetWithFallbackAsync<List<string>>("/api/listed-stocks/facets/sectors") ?? new();
            var industries = await GetWithFallbackAsync<List<string>>("/api/listed-stocks/facets/industries") ?? new();
            return Json(new { sectors, industries });
        }

        private async Task<T?> GetWithFallbackAsync<T>(string relative)
        {
            var url = _primaryBaseUrl.TrimEnd('/') + relative;
            try
            {
                return await _http.GetFromJsonAsync<T>(url);
            }
            catch
            {
                var fallback = _fallbackBaseUrl.TrimEnd('/') + relative;
                try { return await _http.GetFromJsonAsync<T>(fallback); } catch { return default; }
            }
        }

        public class SearchResponse { public int total { get; set; } public List<SearchItem> items { get; set; } = new(); }
        public class SearchItem { public string symbol { get; set; } = string.Empty; public string name { get; set; } = string.Empty; public string? sector { get; set; } public string? industry { get; set; } public decimal lastSale { get; set; } public decimal percentChange { get; set; } public decimal marketCap { get; set; } }
    }
}
