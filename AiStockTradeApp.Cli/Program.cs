using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient; // direct SQL access for user/watchlist commands

namespace AiStockTradeApp.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(cfg =>
        {
            cfg.SetApplicationName("aistock-cli");
            cfg.AddCommand<DownloadHistoricalCommand>("download-historical")
               .WithDescription("Download historical stock data CSV from nasdaq.com")
               .WithExample(new[] { "download-historical", "--symbol", "GOOG", "--dest", "C:/tmp/goog.csv" })
               .WithExample(new[] { "download-historical", "-s", "MSFT", "-d", "./msft.csv" });
            cfg.AddCommand<ImportHistoricalCommand>("import-historical")
                .WithDescription("Import a historical data CSV for a symbol into the API")
                .WithExample(new[] { "import-historical", "--symbol", "AAPL", "--file", "./data/nasdaq.com/HistoricalData_AAPL.csv", "--api", "https://localhost:7043" })
                .WithExample(new[] { "import-historical", "-s", "AAPL", "--file", "./aapl.csv", "--watch" });
                cfg.AddCommand<ImportListedCommand>("import-listed")
                    .WithDescription("Import a screener CSV into the API listed-stocks catalog")
                    .WithExample(new[] { "import-listed", "--file", "./data/nasdaq.com/screener.csv", "--api", "https://localhost:5001" });
                cfg.AddCommand<CheckJobCommand>("check-job")
                    .WithDescription("Check background job status by jobId")
                    .WithExample(new[] { "check-job", "--job", "<GUID>", "--api", "https://localhost:5001" });

            // User & watchlist management (direct DB access) ----------------------------------
            cfg.AddCommand<ListUsersCommand>("list-users")
                .WithDescription("List application users (top N)")
                .WithExample(new[]{"list-users","--top","25"});
            cfg.AddCommand<CreateUserCommand>("create-user")
                .WithDescription("Create an Identity user with password (direct DB insert). USE ONLY IN DEV/TEST.")
                .WithExample(new[]{"create-user","--email","demo@example.com","--password","P@ssw0rd1!"});
            cfg.AddCommand<GetWatchlistCommand>("get-watchlist")
                .WithDescription("Display a user's watchlist items")
                .WithExample(new[]{"get-watchlist","--userId","<USER_GUID>"});
        });
        return await app.RunAsync(args);
    }
}

// -----------------------------------------------------------------------------
// Shared settings for direct DB operations
// -----------------------------------------------------------------------------
public abstract class DbSettingsBase : CommandSettings
{
    [CommandOption("--conn <CONNSTRING>")]
    [Description("SQL Server connection string. If omitted, tries ConnectionStrings__DefaultConnection env var.")]
    public string? ConnectionString { get; init; }

    public string ResolveConnectionString()
    {
        var cs = ConnectionString
                 ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                 ?? "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
        return cs;
    }
}

// -----------------------------------------------------------------------------
// list-users
// -----------------------------------------------------------------------------
public sealed class ListUsersSettings : DbSettingsBase
{
    [CommandOption("--top <N>")]
    [Description("Max rows (default 20)")]
    [DefaultValue(20)]
    public int Top { get; init; } = 20;

    public override ValidationResult Validate()
    {
        if (Top <= 0 || Top > 500) return ValidationResult.Error("--top must be between 1 and 500");
        return ValidationResult.Success();
    }
}

public sealed class ListUsersCommand : AsyncCommand<ListUsersSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListUsersSettings settings)
    {
        try
        {
            var cs = settings.ResolveConnectionString();
            using var conn = new SqlConnection(cs);
            await conn.OpenAsync();

            // Determine user table (supports legacy AspNetUsers or custom Users)
            var userTable = await DbIntrospection.ResolveUserTableAsync(conn);
            var cols = await DbIntrospection.GetColumnSetAsync(conn, userTable);

            // Preferred columns (some may not exist in custom schema)
            string[] preferred = ["Id","Email","UserName","CreatedAt","LastLoginAt"];            
            var selectCols = preferred.Where(c => cols.Contains(c)).ToList();
            if (!selectCols.Contains("Id")) selectCols.Insert(0, "Id");
            var orderCol = selectCols.Contains("CreatedAt") ? "CreatedAt" : "Id";

            var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT TOP (@top) {string.Join(",", selectCols)} FROM {userTable} ORDER BY {orderCol} DESC";
            cmd.Parameters.Add(new SqlParameter("@top", settings.Top));
            var rows = new List<Dictionary<string, object?>>();
            using (var rdr = await cmd.ExecuteReaderAsync())
            {
                while (await rdr.ReadAsync())
                {
                    var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in selectCols)
                    {
                        var ord = rdr.GetOrdinal(c);
                        dict[c] = rdr.IsDBNull(ord) ? null : rdr.GetValue(ord);
                    }
                    rows.Add(dict);
                }
            }
            if (rows.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No users found.[/]");
                return 0;
            }
            var table = new Table();
            table.AddColumn("Id");
            table.AddColumn("Email");
            table.AddColumn("UserName");
            table.AddColumn("CreatedAt (UTC)");
            table.AddColumn("LastLogin (UTC)");
            foreach (var r in rows)
            {
                string Get(string k) => r.TryGetValue(k, out var v) && v!=null ? (v is DateTime dt? dt.ToString("u") : v.ToString() ?? "") : "";
                table.AddRow(Get("Id"), Get("Email"), Get("UserName"), Get("CreatedAt"), Get("LastLoginAt"));
            }
            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

// -----------------------------------------------------------------------------
// create-user (direct insert). WARNING: bypasses normal Identity hashing policy.
// -----------------------------------------------------------------------------
public sealed class CreateUserSettings : DbSettingsBase
{
    [CommandOption("--email <EMAIL>")]
    [Description("Email/username for the user")]
    public string Email { get; init; } = string.Empty;

    [CommandOption("--password <PWD>")]
    [Description("Plaintext password; will store a precomputed hash for P@ssw0rd1! only, otherwise error")]
    public string Password { get; init; } = string.Empty;

    [CommandOption("--id <GUID>")]
    [Description("Optional explicit user ID (GUID). If omitted a GUID is generated.")]
    public string? UserId { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Email)) return ValidationResult.Error("--email required");
        if (string.IsNullOrWhiteSpace(Password)) return ValidationResult.Error("--password required");
        if (!string.Equals(Password, "P@ssw0rd1!", StringComparison.Ordinal))
            return ValidationResult.Error("Only password 'P@ssw0rd1!' supported (pre-hashed). Change code to expand.");
        if (!string.IsNullOrWhiteSpace(UserId) && !Guid.TryParse(UserId, out _))
            return ValidationResult.Error("--id must be GUID");
        return ValidationResult.Success();
    }
}

public sealed class CreateUserCommand : AsyncCommand<CreateUserSettings>
{
    // Precomputed Identity v7+ styled hash for password P@ssw0rd1!
    private const string KnownHash = "AQAAAAIAAYagAAAAEP9bL7MWHf2vzZ5L1GgcFJQQmLk4yL4blvVvK2uWS2h9Jq1wWNxCQtBxMBT7I5ZyOw=="; // example placeholder
    public override async Task<int> ExecuteAsync(CommandContext context, CreateUserSettings settings)
    {
        try
        {
            var cs = settings.ResolveConnectionString();
            using var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            var userId = string.IsNullOrWhiteSpace(settings.UserId) ? Guid.NewGuid().ToString() : settings.UserId!;

            var userTable = await DbIntrospection.ResolveUserTableAsync(conn);
            var cols = await DbIntrospection.GetColumnSetAsync(conn, userTable);

            // Determine normalized email column names
            var normalizedEmailCol = cols.Contains("NormalizedEmail") ? "NormalizedEmail" : cols.Contains("Email") ? "Email" : null;

            var existsCmd = conn.CreateCommand();
            if (normalizedEmailCol != null)
            {
                existsCmd.CommandText = $"SELECT COUNT(1) FROM {userTable} WHERE {normalizedEmailCol}=@e";
                existsCmd.Parameters.Add(new SqlParameter("@e", settings.Email.ToUpperInvariant()));
            }
            else
            {
                existsCmd.CommandText = $"SELECT 0"; // No email column to enforce uniqueness
            }
            var exists = (int)await existsCmd.ExecuteScalarAsync() > 0;
            if (exists)
            {
                AnsiConsole.MarkupLine("[yellow]User already exists.[/]");
                return 0;
            }
            var now = DateTime.UtcNow;
            var insert = conn.CreateCommand();

            // Build insert tailored to detected columns
            // Core required: Id + some username/email + password hash (if column exists)
            var colList = new List<string> {"Id"};
            var paramList = new List<string> {"@id"};
            var parameters = new List<SqlParameter> { new("@id", userId) };

            void Add(string col, string paramName, object value)
            {
                colList.Add(col); paramList.Add(paramName); parameters.Add(new SqlParameter(paramName, value ?? (object)DBNull.Value));
            }

            // Username / Email variants
            if (cols.Contains("UserName")) Add("UserName","@un", settings.Email);
            if (cols.Contains("NormalizedUserName")) Add("NormalizedUserName","@nun", settings.Email.ToUpperInvariant());
            if (cols.Contains("Email")) Add("Email","@em", settings.Email);
            if (cols.Contains("NormalizedEmail")) Add("NormalizedEmail","@nem", settings.Email.ToUpperInvariant());
            if (cols.Contains("EmailConfirmed")) Add("EmailConfirmed","@ec", 0);
            if (cols.Contains("PasswordHash")) Add("PasswordHash","@pwd", KnownHash);
            if (cols.Contains("SecurityStamp")) Add("SecurityStamp","@sec", Guid.NewGuid().ToString());
            if (cols.Contains("ConcurrencyStamp")) Add("ConcurrencyStamp","@cc", Guid.NewGuid().ToString());
            if (cols.Contains("PhoneNumberConfirmed")) Add("PhoneNumberConfirmed","@pnc", 0);
            if (cols.Contains("TwoFactorEnabled")) Add("TwoFactorEnabled","@tfe", 0);
            if (cols.Contains("LockoutEnabled")) Add("LockoutEnabled","@loe", 1);
            if (cols.Contains("AccessFailedCount")) Add("AccessFailedCount","@afc", 0);
            if (cols.Contains("CreatedAt")) Add("CreatedAt","@created", now);
            if (cols.Contains("EnablePriceAlerts")) Add("EnablePriceAlerts","@epa", 1);
            if (cols.Contains("PreferredCulture")) Add("PreferredCulture","@pc", "en");

            insert.CommandText = $"INSERT INTO {userTable} ({string.Join(",", colList)}) VALUES ({string.Join(",", paramList)})";
            insert.Parameters.AddRange(parameters.ToArray());
            var rows = await insert.ExecuteNonQueryAsync();
            if (rows == 1)
            {
                AnsiConsole.MarkupLine($"[green]Created user[/] [cyan]{settings.Email}[/] Id=[yellow]{userId}[/]");
                return 0;
            }
            AnsiConsole.MarkupLine("[red]Insert failed (no rows).[/]");
            return 2;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

// -----------------------------------------------------------------------------
// get-watchlist
// -----------------------------------------------------------------------------
public sealed class GetWatchlistSettings : DbSettingsBase
{
    [CommandOption("--userId <GUID>")]
    [Description("User Id (GUID string)")]
    public string UserId { get; init; } = string.Empty;

    public override ValidationResult Validate()
    {
        if (!Guid.TryParse(UserId, out _)) return ValidationResult.Error("--userId must be GUID");
        return ValidationResult.Success();
    }
}

public sealed class GetWatchlistCommand : AsyncCommand<GetWatchlistSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GetWatchlistSettings settings)
    {
        try
        {
            var cs = settings.ResolveConnectionString();
            using var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            var userTable = await DbIntrospection.ResolveUserTableAsync(conn);
            var cols = await DbIntrospection.GetColumnSetAsync(conn, userTable);

            // Validate user exists (try Email, else UserName)
            var emailColumn = cols.Contains("Email") ? "Email" : cols.Contains("UserName") ? "UserName" : "Id";
            var chk = conn.CreateCommand();
            chk.CommandText = $"SELECT {emailColumn} FROM {userTable} WHERE Id=@id";
            chk.Parameters.Add(new SqlParameter("@id", settings.UserId));
            var emailObj = await chk.ExecuteScalarAsync();
            if (emailObj == null)
            {
                AnsiConsole.MarkupLine("[red]User not found.[/]");
                return 2;
            }
            var email = emailObj as string ?? "(no email)";

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT Id, Symbol, AddedAt, SortOrder, EnableAlerts FROM UserWatchlistItems WHERE UserId=@u ORDER BY SortOrder, AddedAt";
            cmd.Parameters.Add(new SqlParameter("@u", settings.UserId));
            var table = new Table().AddColumns("Id","Symbol","AddedAt (UTC)","Sort","Alerts");
            int count = 0;
            using (var rdr = await cmd.ExecuteReaderAsync())
            {
                while (await rdr.ReadAsync())
                {
                    count++;
                    table.AddRow(rdr.GetInt32(0).ToString(), rdr.GetString(1), rdr.GetDateTime(2).ToString("u"), rdr.GetInt32(3).ToString(), rdr.GetBoolean(4)?"Yes":"No");
                }
            }
            AnsiConsole.MarkupLine($"User: [cyan]{email}[/] ([grey]{settings.UserId}[/])  Items: [green]{count}[/]");
            if (count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No watchlist items.[/]");
                return 0;
            }
            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

// -----------------------------------------------------------------------------
// Introspection helpers
// -----------------------------------------------------------------------------
internal static class DbIntrospection
{
    public static async Task<string> ResolveUserTableAsync(SqlConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = "IF EXISTS (SELECT 1 FROM sys.tables WHERE name='Users') SELECT 'Users' ELSE IF EXISTS (SELECT 1 FROM sys.tables WHERE name='AspNetUsers') SELECT 'AspNetUsers' ELSE SELECT 'Users'";
        var result = (string)await cmd.ExecuteScalarAsync();
        return result;
    }

    public static async Task<HashSet<string>> GetColumnSetAsync(SqlConnection conn, string table)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT c.name FROM sys.columns c INNER JOIN sys.tables t ON c.object_id=t.object_id WHERE t.name=@t";
    cmd.Parameters.Add(new SqlParameter("@t", table));
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync()) set.Add(rdr.GetString(0));
        return set;
    }
}

public sealed class ImportHistoricalSettings : CommandSettings
{
    [CommandOption("-s|--symbol <SYMBOL>")]
    [Description("Stock ticker symbol (e.g., AAPL)")]
    public string Symbol { get; init; } = string.Empty;

    [CommandOption("--file <PATH>")]
    [Description("Path to the historical CSV file to import (Date,Close/Last,Volume,Open,High,Low)")]
    public string FilePath { get; init; } = string.Empty;

    [CommandOption("--api <BASEURL>")]
    [Description("API base URL (e.g., https://localhost:7043)")]
    public string ApiBase { get; init; } = "http://localhost:5000";

    [CommandOption("--watch")] 
    [Description("Poll job status until completion")] 
    public bool Watch { get; init; }

    [CommandOption("--intervalSec <N>")]
    [Description("Polling interval seconds when --watch (default 3)")]
    [DefaultValue(3)]
    public int IntervalSec { get; init; } = 3;

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Symbol))
            return ValidationResult.Error("--symbol is required");
        if (string.IsNullOrWhiteSpace(FilePath) || !System.IO.File.Exists(FilePath))
            return ValidationResult.Error("--file path is required and must exist");
        if (string.IsNullOrWhiteSpace(ApiBase))
            return ValidationResult.Error("--api is required");
        return ValidationResult.Success();
    }
}

public sealed class ImportHistoricalCommand : AsyncCommand<ImportHistoricalSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ImportHistoricalSettings settings)
    {
        try
        {
            var csv = await System.IO.File.ReadAllTextAsync(settings.FilePath);
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(2);
            var symbol = settings.Symbol.Trim().ToUpperInvariant();
            var url = settings.ApiBase.TrimEnd('/') + $"/api/historical-prices/{symbol}/import-csv";
            var content = new StringContent(csv, System.Text.Encoding.UTF8, "text/csv");
            try { content.Headers.Add("X-File-Name", Path.GetFileName(settings.FilePath)); } catch { }
            AnsiConsole.MarkupLine($"[green]POST[/] {url} ({csv.Length} bytes)");
            var resp = await http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                AnsiConsole.MarkupLine("[green]Import accepted and queued.[/]");
                Guid jobId;
                string? statusUrl = null;
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(body);
                    jobId = doc.RootElement.GetProperty("jobId").GetGuid();
                    statusUrl = doc.RootElement.TryGetProperty("location", out var locProp) ? locProp.GetString() : null;
                }
                catch
                {
                    AnsiConsole.WriteLine(body);
                    return 0;
                }

                AnsiConsole.MarkupLine($"JobId: [cyan]{jobId}[/]");
                if (!string.IsNullOrEmpty(statusUrl)) AnsiConsole.MarkupLine($"Status URL: [blue]{statusUrl}[/]");

                if (!settings.Watch)
                {
                    AnsiConsole.MarkupLine("Use: aistock-cli check-job --job <GUID> --api <BASEURL> to track progress.");
                    return 0;
                }

                // Watch mode: poll the shared job status endpoint
                var checkUrl = string.IsNullOrEmpty(statusUrl)
                    ? settings.ApiBase.TrimEnd('/') + $"/api/listed-stocks/import-jobs/{jobId}"
                    : CombineUrl(settings.ApiBase, statusUrl);

                using var pollClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                while (true)
                {
                    try
                    {
                        var sbody = await pollClient.GetStringAsync(checkUrl);
                        var doc = System.Text.Json.JsonDocument.Parse(sbody);
                        var status = doc.RootElement.GetProperty("status").GetString() ?? "";
                        var processed = doc.RootElement.TryGetProperty("processed", out var p) ? p.GetInt32() : 0;
                        var total = doc.RootElement.TryGetProperty("total", out var t) && t.ValueKind != System.Text.Json.JsonValueKind.Null ? t.GetInt32() : 0;
                        var line = total > 0 ? $"{processed}/{total}" : processed.ToString();
                        AnsiConsole.MarkupLine($"Status: [cyan]{status}[/] Progress: [green]{line}[/]");
                        if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)) return 0;
                        if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                        {
                            var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
                            if (!string.IsNullOrEmpty(err)) AnsiConsole.MarkupLine($"[red]{err}[/]");
                            return 2;
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Status check error:[/] {ex.Message}");
                        return 1;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.IntervalSec)));
                }
            }
            else if (!resp.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine($"[red]Import failed:[/] {(int)resp.StatusCode} {resp.ReasonPhrase}");
                AnsiConsole.WriteLine(body);
                return 2;
            }
            AnsiConsole.MarkupLine("[green]Import succeeded.[/]");
            AnsiConsole.WriteLine(body);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static string CombineUrl(string baseUrl, string relativeOrAbsolute)
    {
        if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var abs)) return abs.ToString();
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        if (relativeOrAbsolute.StartsWith('/')) relativeOrAbsolute = relativeOrAbsolute.TrimStart('/');
        return baseUrl + relativeOrAbsolute;
    }
}

public sealed class DownloadHistoricalCommandSettings : CommandSettings
{
    [CommandOption("-s|--symbol <SYMBOL>")]
    [Description("Stock ticker symbol (e.g., GOOG)")]
    public string Symbol { get; init; } = string.Empty;

    [CommandOption("-d|--dest <FILEPATH>")]
    [Description("Destination CSV file path")]
    public string Destination { get; init; } = string.Empty;

    [CommandOption("--headful")] 
    [Description("Run browser non-headless for debugging")] 
    public bool Headful { get; init; }

    [CommandOption("--timeoutSec <SECONDS>")]
    [Description("Overall operation timeout in seconds (default 60)")]
    [DefaultValue(60)]
    public int TimeoutSec { get; init; } = 60;

    [CommandOption("--browser <NAME>")]
    [Description("Browser engine: auto|chromium|firefox|webkit (default: auto)")]
    [DefaultValue("auto")]
    public string Browser { get; init; } = "auto";

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Symbol))
            return ValidationResult.Error("--symbol is required");
        if (string.IsNullOrWhiteSpace(Destination))
            return ValidationResult.Error("--dest is required");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "auto", "chromium", "firefox", "webkit" };
        if (!allowed.Contains(Browser))
            return ValidationResult.Error("--browser must be one of: auto|chromium|firefox|webkit");
        return ValidationResult.Success();
    }
}

public sealed class ImportListedSettings : CommandSettings
{
    [CommandOption("--file <PATH>")]
    [Description("Path to the screener CSV file to import")] 
    public string FilePath { get; init; } = string.Empty;

    [CommandOption("--api <BASEURL>")]
    [Description("API base URL (e.g., https://localhost:5001)")]
    public string ApiBase { get; init; } = "http://localhost:5000";

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || !System.IO.File.Exists(FilePath))
            return ValidationResult.Error("--file path is required and must exist");
        if (string.IsNullOrWhiteSpace(ApiBase))
            return ValidationResult.Error("--api is required");
        return ValidationResult.Success();
    }
}

public sealed class ImportListedCommand : AsyncCommand<ImportListedSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ImportListedSettings settings)
    {
        try
        {
            var csv = await System.IO.File.ReadAllTextAsync(settings.FilePath);
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(2);
            var url = settings.ApiBase.TrimEnd('/') + "/api/listed-stocks/import-csv";
            var content = new StringContent(csv, System.Text.Encoding.UTF8, "text/csv");
            try { content.Headers.Add("X-File-Name", Path.GetFileName(settings.FilePath)); } catch { }
            AnsiConsole.MarkupLine($"[green]POST[/] {url} ({csv.Length} bytes)");
            var resp = await http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                // Background job accepted; parse job info
                AnsiConsole.MarkupLine("[green]Import accepted and queued.[/]");
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(body);
                    var jobId = doc.RootElement.GetProperty("jobId").GetGuid();
                    var location = doc.RootElement.TryGetProperty("location", out var locProp) ? locProp.GetString() : null;
                    AnsiConsole.MarkupLine($"JobId: [cyan]{jobId}[/]");
                    if (!string.IsNullOrEmpty(location)) AnsiConsole.MarkupLine($"Status URL: [blue]{location}[/]");
                    AnsiConsole.MarkupLine("Use: aistock-cli check-job --job <GUID> --api <BASEURL> to track progress.");
                }
                catch
                {
                    AnsiConsole.WriteLine(body);
                }
                return 0;
            }
            else if (!resp.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine($"[red]Import failed:[/] {(int)resp.StatusCode} {resp.ReasonPhrase}");
                AnsiConsole.WriteLine(body);
                return 2;
            }
            AnsiConsole.MarkupLine("[green]Import succeeded.[/]");
            AnsiConsole.WriteLine(body);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}

public sealed class CheckJobSettings : CommandSettings
{
    [CommandOption("--job <GUID>")]
    [Description("Background job ID to check")] 
    public string JobId { get; init; } = string.Empty;

    [CommandOption("--api <BASEURL>")]
    [Description("API base URL (e.g., https://localhost:5001)")]
    public string ApiBase { get; init; } = "http://localhost:5000";

    [CommandOption("--watch")] 
    [Description("Continuously poll until job completes or fails")] 
    public bool Watch { get; init; }

    [CommandOption("--intervalSec <N>")]
    [Description("Polling interval seconds when --watch (default 3)")]
    [DefaultValue(3)]
    public int IntervalSec { get; init; } = 3;

    public override ValidationResult Validate()
    {
        if (!Guid.TryParse(JobId, out _))
            return ValidationResult.Error("--job must be a valid GUID");
        if (string.IsNullOrWhiteSpace(ApiBase))
            return ValidationResult.Error("--api is required");
        return ValidationResult.Success();
    }
}

public sealed class CheckJobCommand : AsyncCommand<CheckJobSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CheckJobSettings settings)
    {
        var url = settings.ApiBase.TrimEnd('/') + $"/api/listed-stocks/import-jobs/{settings.JobId}";
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(30);

        async Task<int> Once()
        {
            try
            {
                var body = await http.GetStringAsync(url);
                var doc = System.Text.Json.JsonDocument.Parse(body);
                var status = doc.RootElement.GetProperty("status").GetString() ?? "";
                var processed = doc.RootElement.TryGetProperty("processed", out var p) ? p.GetInt32() : 0;
                var total = doc.RootElement.TryGetProperty("total", out var t) && t.ValueKind != System.Text.Json.JsonValueKind.Null ? t.GetInt32() : 0;
                var line = total > 0 ? $"{processed}/{total}" : processed.ToString();
                AnsiConsole.MarkupLine($"Status: [cyan]{status}[/] Progress: [green]{line}[/]");
                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)) return 0;
                if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
                    if (!string.IsNullOrEmpty(err)) AnsiConsole.MarkupLine($"[red]{err}[/]");
                    return 2;
                }
                return 3; // still running
            }
            catch (HttpRequestException ex)
            {
                AnsiConsole.MarkupLine($"[red]Request error:[/] {ex.Message}");
                return 1;
            }
        }

        if (!settings.Watch)
        {
            return await Once();
        }

        while (true)
        {
            var code = await Once();
            if (code == 0 || code == 1 || code == 2) return code;
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.IntervalSec)));
        }
    }
}

public sealed class DownloadHistoricalCommand : AsyncCommand<DownloadHistoricalCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DownloadHistoricalCommandSettings settings)
    {
        var symbol = settings.Symbol.Trim().ToLowerInvariant();
        var url = $"https://www.nasdaq.com/market-activity/stocks/{symbol}/historical?page=1&rows_per_page=10&timeline=y10";
        var destPath = Path.GetFullPath(settings.Destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        AnsiConsole.MarkupLine($"[green]Navigating:[/] {url}");
        AnsiConsole.MarkupLine($"[green]Will save to:[/] {destPath}");
    var timeoutMs = Math.Max(10, settings.TimeoutSec) * 1000;

        // Setup Playwright
        using var pw = await Playwright.CreateAsync();
        var chromiumArgs = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" };
        var headlessUA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";
        IBrowser browser;
        IBrowserContext bctx;
        IPage page;

        string browserPref = settings.Browser.Trim().ToLowerInvariant();
        if (settings.Headful)
        {
            // Headful: prefer Chromium for best dev experience
            browser = await pw.Chromium.LaunchAsync(new() { Headless = false, Args = chromiumArgs });
            bctx = await browser.NewContextAsync(new() { AcceptDownloads = true });
            page = await bctx.NewPageAsync();
        }
        else
        {
            switch (browserPref)
            {
                case "chromium":
                    browser = await pw.Chromium.LaunchAsync(new() { Headless = true, Args = chromiumArgs });
                    bctx = await browser.NewContextAsync(new()
                    {
                        AcceptDownloads = true,
                        UserAgent = headlessUA,
                        ViewportSize = new() { Width = 1280, Height = 900 }
                    });
                    page = await bctx.NewPageAsync();
                    break;
                case "firefox":
                    browser = await pw.Firefox.LaunchAsync(new() { Headless = true });
                    bctx = await browser.NewContextAsync(new()
                    {
                        AcceptDownloads = true,
                        UserAgent = headlessUA,
                        ViewportSize = new() { Width = 1280, Height = 900 }
                    });
                    page = await bctx.NewPageAsync();
                    break;
                case "webkit":
                    browser = await pw.Webkit.LaunchAsync(new() { Headless = true });
                    bctx = await browser.NewContextAsync(new()
                    {
                        AcceptDownloads = true,
                        UserAgent = headlessUA,
                        ViewportSize = new() { Width = 1280, Height = 900 }
                    });
                    page = await bctx.NewPageAsync();
                    break;
                default: // auto (Chromium with fallback later)
                    browser = await pw.Chromium.LaunchAsync(new() { Headless = true, Args = chromiumArgs });
                    bctx = await browser.NewContextAsync(new()
                    {
                        AcceptDownloads = true,
                        UserAgent = headlessUA,
                        ViewportSize = new() { Width = 1280, Height = 900 }
                    });
                    page = await bctx.NewPageAsync();
                    break;
            }
        }
        page.SetDefaultTimeout(timeoutMs);

        // Navigate with a more permissive load state (networkidle can hang on analytics)
        try
        {
            await page.GotoAsync(url, new() { Timeout = timeoutMs, WaitUntil = WaitUntilState.DOMContentLoaded });
        }
    catch (PlaywrightException ex) when (!settings.Headful && browserPref == "auto" && ex.Message.Contains("ERR_HTTP2_PROTOCOL_ERROR", StringComparison.OrdinalIgnoreCase))
        {
            // Headless Chromium can hit HTTP/2 issues in some environments; fall back to Firefox headless
            AnsiConsole.MarkupLine("[yellow]Chromium headless hit HTTP/2 error; retrying with Firefox headless...[/]");
            try { await bctx.CloseAsync(); } catch { }
            try { await browser.CloseAsync(); } catch { }

            browser = await pw.Firefox.LaunchAsync(new() { Headless = true });
            bctx = await browser.NewContextAsync(new()
            {
                AcceptDownloads = true,
                UserAgent = headlessUA,
                ViewportSize = new() { Width = 1280, Height = 900 },
            });
            page = await bctx.NewPageAsync();
            page.SetDefaultTimeout(timeoutMs);
            await page.GotoAsync(url, new() { Timeout = timeoutMs, WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        catch (TimeoutException)
        {
            // Best effort: continue; page may have loaded enough.
        }
        // Give a brief moment for late scripts
        await page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 5000 });

    // Attempt to accept cookie banner if present (common variants)
        await page.TryAcceptCookiesAsync(acceptTimeoutMs: 12000);

    // Find the download control with progressive scrolling and retries
    var downloadControl = await PageFinders.FindDownloadControlAsync(page, timeoutMs);

        if (downloadControl is null)
        {
            AnsiConsole.MarkupLine("[red]Could not find a visible download control. The page layout may have changed.[/]");
            AnsiConsole.MarkupLine("[yellow]Tip:[/] Try running with --headful to inspect or update selectors.");
            await browser.CloseAsync();
            return 2;
        }

    await downloadControl.ScrollIntoViewIfNeededAsync();
    try { await downloadControl.HoverAsync(new() { Timeout = 1500 }); } catch { }

        // Strategy 1: Use the browser download event
        try
        {
            var dl = await page.RunAndWaitForDownloadAsync(async () =>
            {
                await downloadControl.ClickAsync(new() { Force = true, Timeout = 8000 });
            }, new() { Timeout = timeoutMs });
            await dl.SaveAsAsync(destPath);
            AnsiConsole.MarkupLine("[green]Download completed (browser event).[/]");
        }
        catch (TimeoutException)
        {
            // Strategy 2: Fallback to capturing the CSV network response
            AnsiConsole.MarkupLine("[yellow]No browser download event detected. Falling back to network capture...[/]");
            await downloadControl.ClickAsync(new() { Force = true, Timeout = 8000 });

            var response = await page.WaitForResponseAsync(r =>
            {
                try
                {
                    var url = r.Url ?? string.Empty;
                    var ct = r.Headers.TryGetValue("content-type", out var v) ? v : string.Empty;
                    return url.Contains(".csv", StringComparison.OrdinalIgnoreCase)
                           || url.Contains("download", StringComparison.OrdinalIgnoreCase)
                           || (!string.IsNullOrEmpty(ct) && ct.Contains("text/csv", StringComparison.OrdinalIgnoreCase));
                }
                catch { return false; }
            }, new() { Timeout = timeoutMs / 2 });

            await response.FinishedAsync();
            var body = await response.BodyAsync();
            await File.WriteAllBytesAsync(destPath, body);
            AnsiConsole.MarkupLine("[green]Download completed (network capture).[/]");
        }

    await browser.CloseAsync();
        return 0;
    }
}

internal static class DownloadLocators
{
    public static IEnumerable<Func<IPage, ILocator>> BuildCandidates()
    {
        yield return p => p.GetByRole(AriaRole.Button, new() { Name = "Download historical data" });
        yield return p => p.GetByRole(AriaRole.Link,   new() { Name = "Download historical data" });
        yield return p => p.GetByRole(AriaRole.Button, new() { NameRegex = new("(?i)download.*(historical|csv)") });
        yield return p => p.GetByRole(AriaRole.Link,   new() { NameRegex = new("(?i)download.*(historical|csv)") });
        yield return p => p.GetByText(new Regex("Download\\s+(historical|csv)", RegexOptions.IgnoreCase));
        yield return p => p.Locator(":has-text('Download historical data')");
        yield return p => p.Locator(":has-text('Download CSV')");
        yield return p => p.Locator("a[download], button[download]");
        yield return p => p.Locator("[aria-label*='Download' i]");
        yield return p => p.Locator("[data-testid*='download' i]");
        yield return p => p.Locator("a[href*='.csv'], a[href*='download']");
    }
}

internal static class PageFinders
{
    public static async Task<ILocator?> FindDownloadControlAsync(IPage page, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        int attempt = 0;
        // Try a few full-page scroll passes to trigger lazy content
        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            AnsiConsole.MarkupLine($"[grey]Scanning for download control (attempt {attempt})...[/]");

            foreach (var factory in DownloadLocators.BuildCandidates())
            {
                var loc = factory(page).First;
                try
                {
                    await loc.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 1200 });
                    if (await loc.IsVisibleAsync())
                    {
                        return loc;
                    }
                }
                catch { /* try next candidate */ }
            }

            // Scroll down in steps to reveal lazily rendered controls
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    await page.EvaluateAsync("window.scrollBy(0, 600)");
                    await page.WaitForTimeoutAsync(200);
                }
                // Scroll back to top to catch sticky header buttons
                await page.EvaluateAsync("window.scrollTo(0, 0)");
            }
            catch { }

            await page.WaitForTimeoutAsync(400);
        }

        return null;
    }
}

internal static class PageExtensions
{
    public static async Task TryAcceptCookiesAsync(this IPage page, int acceptTimeoutMs = 7000)
    {
        try
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, acceptTimeoutMs));
            // Ensure banner is in view (often at bottom)
            try { await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)"); } catch { }

            static IEnumerable<Func<IFrame, ILocator>> CandidateLocators(IPage page)
            {
                // Locators built from a given frame; main page will be included in Frames enumeration too
                yield return f => f.Locator("#onetrust-accept-btn-handler");
                yield return f => f.GetByRole(AriaRole.Button, new() { NameRegex = new("(?i)accept.*cookies") });
                yield return f => f.GetByText("Accept all cookies", new() { Exact = false });
                yield return f => f.Locator("button:has-text('Accept all cookies')");
                yield return f => f.Locator("button:has-text('I Accept')");
                yield return f => f.Locator("button:has-text('Accept')");
                // Some sites use shadow containers; Playwright still surfaces buttons when accessible
            }

            while (DateTime.UtcNow < deadline)
            {
                var frames = page.Frames;
                foreach (var frame in frames)
                {
                    foreach (var make in CandidateLocators(page))
                    {
                        try
                        {
                            var loc = make(frame);
                            await loc.ScrollIntoViewIfNeededAsync();
                            await loc.ClickAsync(new() { Timeout = 800, Force = true });
                            // brief settle
                            await page.WaitForTimeoutAsync(300);
                            return; // Success
                        }
                        catch { /* try next */ }
                    }
                }
                // small delay before retrying
                await page.WaitForTimeoutAsync(400);
            }
        }
        catch
        {
            // Swallow any unexpected issues; cookie consent is best-effort.
        }
    }
}
