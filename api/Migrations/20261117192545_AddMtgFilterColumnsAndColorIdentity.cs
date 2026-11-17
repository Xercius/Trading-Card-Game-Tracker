using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddMtgFilterColumnsAndColorIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Loyalty",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Toughness",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CardColorIdentities",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    AspectId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardColorIdentities", x => new { x.CardId, x.AspectId });
                    table.ForeignKey(
                        name: "FK_CardColorIdentities_Aspects_AspectId",
                        column: x => x.AspectId,
                        principalTable: "Aspects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardColorIdentities_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Cost",
                table: "Cards",
                column: "Cost");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Loyalty",
                table: "Cards",
                column: "Loyalty");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Power",
                table: "Cards",
                column: "Power");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Toughness",
                table: "Cards",
                column: "Toughness");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_UpgradeHp",
                table: "Cards",
                column: "UpgradeHp");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_UpgradePower",
                table: "Cards",
                column: "UpgradePower");

            migrationBuilder.CreateIndex(
                name: "IX_CardColorIdentities_AspectId",
                table: "CardColorIdentities",
                column: "AspectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardColorIdentities");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Cost",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Loyalty",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Power",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Toughness",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_UpgradeHp",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_UpgradePower",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Loyalty",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Toughness",
                table: "Cards");
        }
    }
}
