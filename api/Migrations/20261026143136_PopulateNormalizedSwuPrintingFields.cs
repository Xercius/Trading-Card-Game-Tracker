using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class PopulateNormalizedSwuPrintingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeployBox",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeployBoxStyled",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EpicAction",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EpicActionStyled",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rules",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RulesStyled",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextStyled",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApiPublishedAt",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ArtBackHorizontal",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ArtFrontHorizontal",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Hyperspace",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFoil",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReprintOfId",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Showcase",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VariantOfId",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_ReprintOfId",
                table: "CardPrintings",
                column: "ReprintOfId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_VariantOfId",
                table: "CardPrintings",
                column: "VariantOfId");

            migrationBuilder.AddForeignKey(
                name: "FK_CardPrintings_CardPrintings_ReprintOfId",
                table: "CardPrintings",
                column: "ReprintOfId",
                principalTable: "CardPrintings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CardPrintings_CardPrintings_VariantOfId",
                table: "CardPrintings",
                column: "VariantOfId",
                principalTable: "CardPrintings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardPrintings_CardPrintings_ReprintOfId",
                table: "CardPrintings");

            migrationBuilder.DropForeignKey(
                name: "FK_CardPrintings_CardPrintings_VariantOfId",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_ReprintOfId",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_VariantOfId",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "DeployBox",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "DeployBoxStyled",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "EpicAction",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "EpicActionStyled",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Rules",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "RulesStyled",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "TextStyled",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ApiPublishedAt",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "ArtBackHorizontal",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "ArtFrontHorizontal",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "Hyperspace",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "IsFoil",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "ReprintOfId",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "Showcase",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "VariantOfId",
                table: "CardPrintings");
        }
    }
}
