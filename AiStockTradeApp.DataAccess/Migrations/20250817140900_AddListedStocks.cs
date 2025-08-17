using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiStockTradeApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddListedStocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListedStocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LastSale = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetChange = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PercentChange = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MarketCap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpoYear = table.Column<int>(type: "int", nullable: true),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListedStocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListedStocks_Symbol",
                table: "ListedStocks",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListedStocks_Sector",
                table: "ListedStocks",
                column: "Sector");

            migrationBuilder.CreateIndex(
                name: "IX_ListedStocks_Industry",
                table: "ListedStocks",
                column: "Industry");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListedStocks");
        }
    }
}
