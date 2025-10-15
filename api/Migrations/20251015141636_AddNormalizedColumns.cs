using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GameNorm",
                table: "Cards",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "LOWER(TRIM([Game]))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "RarityNorm",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "LOWER(TRIM([Rarity]))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "SetNorm",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "LOWER(TRIM([Set]))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_GameNorm",
                table: "Cards",
                column: "GameNorm");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_RarityNorm",
                table: "CardPrintings",
                column: "RarityNorm");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_SetNorm",
                table: "CardPrintings",
                column: "SetNorm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_GameNorm",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_RarityNorm",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_SetNorm",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "GameNorm",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "RarityNorm",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "SetNorm",
                table: "CardPrintings");
        }
    }
}
