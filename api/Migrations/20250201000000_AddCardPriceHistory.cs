using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddCardPriceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardPriceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardPrintingId = table.Column<int>(type: "INTEGER", nullable: false),
                    CapturedAt = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardPriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardPriceHistories_CardPrintings_CardPrintingId",
                        column: x => x.CardPrintingId,
                        principalTable: "CardPrintings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardPriceHistories_CardPrintingId_CapturedAt",
                table: "CardPriceHistories",
                columns: new[] { "CardPrintingId", "CapturedAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardPriceHistories");
        }
    }
}
