using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionTrackerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Arena",
                table: "Cards",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AspectDuplicatesJson",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AspectsJson",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Cost",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Hp",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "JasonsCardId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeywordsJson",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Power",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "Cards",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraitsJson",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type2",
                table: "Cards",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Unique",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

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

            migrationBuilder.CreateIndex(
                name: "IX_Cards_JasonsCardId",
                table: "Cards",
                column: "JasonsCardId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_JasonsCardId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Arena",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "AspectDuplicatesJson",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "AspectsJson",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Hp",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "JasonsCardId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "KeywordsJson",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Power",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Subtitle",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "TraitsJson",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Type2",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Unique",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "UpgradeHp",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "UpgradePower",
                table: "Cards");
        }
    }
}
