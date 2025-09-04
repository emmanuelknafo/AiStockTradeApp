using System;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace AiStockTradeApp.SeleniumTests.Infrastructure;

/// <summary>
/// Simple data seeder for Selenium tests. Bypasses API by inserting directly into the database
/// using lightweight ADO.NET (no project references to DataAccess/Entities required). Assumes the
/// same connection string used by the running app (DefaultConnection) is available via environment
/// variable ConnectionStrings__DefaultConnection or falls back to local dev defaults.
/// </summary>
public static class TestDataSeeder
{
    private static string ResolveConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        // Fallback to common local defaults (update if repo changes)
        return "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
    }

    /// <summary>
    /// Ensures a user exists (Identity user must already exist outside this helper). For now this just seeds watchlist items.
    /// </summary>
    /// <param name="userId">Identity User ID (string)</param>
    /// <param name="symbols">Symbols to ensure on watchlist</param>
    public static void EnsureWatchlist(string userId, params string[] symbols)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId required", nameof(userId));
        if (symbols == null || symbols.Length == 0) return;

        var conn = ResolveConnectionString();
        using var sql = new SqlConnection(conn);
        sql.Open();
        foreach (var sym in symbols.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var check = new SqlCommand("SELECT 1 FROM UserWatchlistItems WHERE UserId=@u AND Symbol=@s", sql);
            check.Parameters.AddWithValue("@u", userId);
            check.Parameters.AddWithValue("@s", sym);
            var exists = check.ExecuteScalar() != null;
            if (!exists)
            {
                var ins = new SqlCommand("INSERT INTO UserWatchlistItems (UserId, Symbol, AddedAt, SortOrder, EnableAlerts) VALUES (@u,@s,GETUTCDATE(),0,1)", sql);
                ins.Parameters.AddWithValue("@u", userId);
                ins.Parameters.AddWithValue("@s", sym.ToUpperInvariant());
                ins.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Clears all watchlist items for given user.
    /// </summary>
    public static void ClearWatchlist(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId required", nameof(userId));
    var conn = ResolveConnectionString();
    using var sql = new SqlConnection(conn);
    sql.Open();
    var del = new SqlCommand("DELETE FROM UserWatchlistItems WHERE UserId=@u", sql);
    del.Parameters.AddWithValue("@u", userId);
    del.ExecuteNonQuery();
    }
}