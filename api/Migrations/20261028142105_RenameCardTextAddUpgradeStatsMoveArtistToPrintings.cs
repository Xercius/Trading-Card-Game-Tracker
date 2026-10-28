using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class RenameCardTextAddUpgradeStatsMoveArtistToPrintings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Cards",
                newName: "Text");

            migrationBuilder.AddColumn<int>(
                name: "UpgradeHp",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpgradePower",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Artist",
                table: "CardPrintings",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "CardPrintings"
                SET "Artist" = (SELECT "Artist" FROM "Cards" WHERE "Cards"."Id" = "CardPrintings"."CardId")
                """);

            migrationBuilder.DropColumn(
                name: "Artist",
                table: "Cards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpgradeHp",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "UpgradePower",
                table: "Cards");

            migrationBuilder.RenameColumn(
                name: "Text",
                table: "Cards",
                newName: "Description");

            migrationBuilder.AddColumn<string>(
                name: "Artist",
                table: "Cards",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "Artist" = (SELECT MIN("Artist") FROM "CardPrintings" WHERE "CardPrintings"."CardId" = "Cards"."Id")
                """);

            migrationBuilder.DropColumn(
                name: "Artist",
                table: "CardPrintings");
        }
    }
}
