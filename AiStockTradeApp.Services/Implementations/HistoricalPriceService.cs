using System.Globalization;
using System.Text;
using AiStockTradeApp.DataAccess.Interfaces;
using AiStockTradeApp.Entities;
using AiStockTradeApp.Services.Interfaces;

namespace AiStockTradeApp.Services.Implementations
{
    public class HistoricalPriceService : IHistoricalPriceService
    {
        private readonly IHistoricalPriceRepository _repo;
        public HistoricalPriceService(IHistoricalPriceRepository repo) => _repo = repo;

        public Task<List<HistoricalPrice>> GetAsync(string symbol, DateTime? from = null, DateTime? to = null, int? take = null)
            => _repo.GetAsync(symbol.ToUpperInvariant(), from, to, take);

        public Task UpsertManyAsync(IEnumerable<HistoricalPrice> prices)
            => _repo.UpsertManyAsync(prices);

        public Task DeleteBySymbolAsync(string symbol)
            => _repo.DeleteBySymbolAsync(symbol.ToUpperInvariant());

        public async Task ImportCsvAsync(string symbol, string csvContent, string? sourceName = null)
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("symbol is required");
            if (string.IsNullOrWhiteSpace(csvContent)) return;
            symbol = symbol.ToUpperInvariant();

            var lines = csvContent.Replace("\r\n", "\n").Replace("\r", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Expected headers: Date,Close/Last,Volume,Open,High,Low
            var start = 0;
            if (lines.Length > 0 && lines[0].StartsWith("Date,", StringComparison.OrdinalIgnoreCase)) start = 1;

            var list = new List<HistoricalPrice>();
            for (int i = start; i < lines.Length; i++)
            {
                var cols = ParseCsvLine(lines[i]);
                if (cols.Count < 6) continue;

                if (!TryParseDate(cols[0], out var date)) continue;
                var close = ToDecimal(cols[1]);
                var volume = ToLong(cols[2]);
                var open = ToDecimal(cols[3]);
                var high = ToDecimal(cols[4]);
                var low = ToDecimal(cols[5]);

                list.Add(new HistoricalPrice
                {
                    Symbol = symbol,
                    Date = date,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume,
                    Source = sourceName
                });
            }

            if (list.Count > 0)
            {
                // Upsert in small batches
                const int batch = 500;
                for (int i = 0; i < list.Count; i += batch)
                {
                    var slice = list.Skip(i).Take(Math.Min(batch, list.Count - i));
                    await _repo.UpsertManyAsync(slice);
                }
            }
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int j = 0; j < line.Length; j++)
            {
                var ch = line[j];
                if (ch == '"')
                {
                    if (inQuotes && j + 1 < line.Length && line[j + 1] == '"')
                    {
                        sb.Append('"');
                        j++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        private static bool TryParseDate(string? s, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var formats = new[] { "M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd" };
            return DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date)
                || DateTime.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
        }

        private static decimal ToDecimal(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            s = s.Replace("$", string.Empty).Replace(",", string.Empty).Trim();
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
        }

        private static long ToLong(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0L;
            s = s.Replace(",", string.Empty).Trim();
            return long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0L;
        }
    }
}
