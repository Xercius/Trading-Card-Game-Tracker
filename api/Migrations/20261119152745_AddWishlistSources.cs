using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlistSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WishlistSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardPrintingId = table.Column<int>(type: "INTEGER", nullable: false),
                    DeckId = table.Column<int>(type: "INTEGER", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishlistSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WishlistSources_CardPrintings_CardPrintingId",
                        column: x => x.CardPrintingId,
                        principalTable: "CardPrintings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WishlistSources_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WishlistSources_DeckId",
                table: "WishlistSources",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "UX_WishlistSources_CardPrintingId_DeckId",
                table: "WishlistSources",
                columns: new[] { "CardPrintingId", "DeckId" },
                unique: true,
                filter: "[DeckId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_WishlistSources_CardPrintingId_General",
                table: "WishlistSources",
                column: "CardPrintingId",
                unique: true,
                filter: "[DeckId] IS NULL");

            migrationBuilder.Sql(
                """
                INSERT INTO WishlistSources (CardPrintingId, DeckId, Quantity, UpdatedAt)
                SELECT CardPrintingId, NULL, QuantityWanted, strftime('%Y-%m-%dT%H:%M:%f+00:00', 'now')
                FROM UserCards
                WHERE QuantityWanted > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WishlistSources");
        }
    }
}
