using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AuditSchemaHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Users_UserId",
                table: "Decks");

            migrationBuilder.RenameIndex(
                name: "IX_Traits_GameId_Name",
                table: "Traits",
                newName: "UX_Traits_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_SubTypes_GameId_Name",
                table: "SubTypes",
                newName: "UX_SubTypes_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Rarities_GameId_Name",
                table: "Rarities",
                newName: "UX_Rarities_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Keywords_GameId_Name",
                table: "Keywords",
                newName: "UX_Keywords_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_ImportSyncHistories_ImporterKey_SetCode",
                table: "ImportSyncHistories",
                newName: "UX_ImportSyncHistories_ImporterKey_SetCode");

            migrationBuilder.RenameIndex(
                name: "IX_DeckCards_DeckId_CardPrintingId",
                table: "DeckCards",
                newName: "UX_DeckCards_DeckId_CardPrintingId");

            migrationBuilder.RenameIndex(
                name: "IX_CardTypes_GameId_Name",
                table: "CardTypes",
                newName: "UX_CardTypes_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "UX_CardPrintings",
                table: "CardPrintings",
                newName: "UX_CardPrintings_CardId_SetId_Number_TreatmentId");

            migrationBuilder.RenameIndex(
                name: "IX_CardPriceHistories_CardPrintingId_CapturedAt",
                table: "CardPriceHistories",
                newName: "UX_CardPriceHistories_CardPrintingId_CapturedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Aspects_GameId_Name",
                table: "Aspects",
                newName: "UX_Aspects_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Arenas_GameId_Name",
                table: "Arenas",
                newName: "UX_Arenas_GameId_Name");

            migrationBuilder.AddColumn<int>(
                name: "GameId",
                table: "Decks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Cards\" SET \"GameId\" = 1 WHERE \"GameId\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "GameId",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Decks_GameId",
                table: "Decks",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Games_GameId",
                table: "Decks",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Users_UserId",
                table: "Decks",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Games_GameId",
                table: "Decks");

            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Users_UserId",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "UX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Decks_GameId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "GameId",
                table: "Decks");

            migrationBuilder.RenameIndex(
                name: "UX_Traits_GameId_Name",
                table: "Traits",
                newName: "IX_Traits_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "UX_SubTypes_GameId_Name",
                table: "SubTypes",
                newName: "IX_SubTypes_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "UX_Rarities_GameId_Name",
                table: "Rarities",
                newName: "IX_Rarities_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "UX_Keywords_GameId_Name",
                table: "Keywords",
                newName: "IX_Keywords_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "UX_ImportSyncHistories_ImporterKey_SetCode",
                table: "ImportSyncHistories",
                newName: "IX_ImportSyncHistories_ImporterKey_SetCode");

            migrationBuilder.RenameIndex(
                name: "UX_DeckCards_DeckId_CardPrintingId",
                table: "DeckCards",
                newName: "IX_DeckCards_DeckId_CardPrintingId");

            migrationBuilder.RenameIndex(
                name: "UX_CardTypes_GameId_Name",
                table: "CardTypes",
                newName: "IX_CardTypes_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "UX_CardPrintings_CardId_SetId_Number_TreatmentId",
                table: "CardPrintings",
                newName: "UX_CardPrintings");

            migrationBuilder.RenameIndex(
                name: "UX_CardPriceHistories_CardPrintingId_CapturedAt",
                table: "CardPriceHistories",
                newName: "IX_CardPriceHistories_CardPrintingId_CapturedAt");

            migrationBuilder.RenameIndex(
                name: "UX_Aspects_GameId_Name",
                table: "Aspects",
                newName: "IX_Aspects_GameId_Name");

            migrationBuilder.RenameIndex(
                name: "UX_Arenas_GameId_Name",
                table: "Arenas",
                newName: "IX_Arenas_GameId_Name");

            migrationBuilder.AlterColumn<int>(
                name: "GameId",
                table: "Cards",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Users_UserId",
                table: "Decks",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
