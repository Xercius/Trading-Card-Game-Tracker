using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddSwccgCardFieldsAndLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Cards_GameId_Name",
                table: "Cards");

            migrationBuilder.AddColumn<int>(
                name: "Ability",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Armor",
                table: "Cards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackText",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackTitle",
                table: "Cards",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Deploy",
                table: "Cards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Destiny",
                table: "Cards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtraCardDataJson",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ferocity",
                table: "Cards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Forfeit",
                table: "Cards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Hyperspeed",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Landspeed",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lore",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Maneuver",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Parsec",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Politics",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Side",
                table: "Cards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uniqueness",
                table: "Cards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_GameId_Name",
                table: "Cards",
                columns: new[] { "GameId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_GameId_Name",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Ability",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Armor",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BackText",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BackTitle",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Deploy",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Destiny",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ExtraCardDataJson",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Ferocity",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Forfeit",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Hyperspeed",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Landspeed",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Lore",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Maneuver",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Parsec",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Politics",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Uniqueness",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "UX_Cards_GameId_Name",
                table: "Cards",
                columns: new[] { "GameId", "Name" },
                unique: true);
        }
    }
}
