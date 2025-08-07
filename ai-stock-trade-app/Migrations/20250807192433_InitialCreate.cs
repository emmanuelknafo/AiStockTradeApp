using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_stock_trade_app.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Change = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PercentChange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    AIAnalysis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RecommendationReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChartDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CachedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CacheDuration = table.Column<long>(type: "bigint", nullable: false, defaultValue: 9000000000L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockData", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockData_Symbol",
                table: "StockData",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_StockData_Symbol_CachedAt",
                table: "StockData",
                columns: new[] { "Symbol", "CachedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockData");
        }
    }
}
