using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiStockTradeApp.DataAccess
{
    // Design-time factory so `dotnet ef` can create the DbContext without the web projects or docker-compose
    public class StockDataContextFactory : IDesignTimeDbContextFactory<StockDataContext>
    {
        public StockDataContext CreateDbContext(string[] args)
        {
            // Prefer environment variable (works well in CI): ConnectionStrings__DefaultConnection
            var cs = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                     ?? Environment.GetEnvironmentVariable("DefaultConnection")
                     ?? "Server=.;Database=StockTraderDb;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";

            var optionsBuilder = new DbContextOptionsBuilder<StockDataContext>();
            optionsBuilder.UseSqlServer(cs, sql =>
            {
                // Ensure migrations are generated in the DataAccess assembly
                sql.MigrationsAssembly(typeof(StockDataContext).Assembly.FullName);
                sql.EnableRetryOnFailure();
            });

            return new StockDataContext(optionsBuilder.Options);
        }
    }
}
