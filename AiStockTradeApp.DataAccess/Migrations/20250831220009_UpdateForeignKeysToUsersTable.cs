using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiStockTradeApp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateForeignKeysToUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPriceAlerts_ApplicationUser_UserId",
                table: "UserPriceAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_UserWatchlistItems_ApplicationUser_UserId",
                table: "UserWatchlistItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_UserPriceAlerts_ApplicationUser_UserId",
                table: "UserPriceAlerts",
                column: "UserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserWatchlistItems_ApplicationUser_UserId",
                table: "UserWatchlistItems",
                column: "UserId",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
