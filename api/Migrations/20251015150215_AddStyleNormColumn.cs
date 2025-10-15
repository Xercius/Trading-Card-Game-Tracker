using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddStyleNormColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StyleNorm",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "LOWER(TRIM([Style]))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_StyleNorm",
                table: "CardPrintings",
                column: "StyleNorm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_StyleNorm",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "StyleNorm",
                table: "CardPrintings");
        }
    }
}
