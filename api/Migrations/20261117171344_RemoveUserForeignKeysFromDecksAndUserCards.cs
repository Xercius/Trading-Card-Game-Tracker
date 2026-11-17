using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserForeignKeysFromDecksAndUserCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Decks_Users_UserId",
                table: "Decks");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCards_Users_UserId",
                table: "UserCards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserCards",
                table: "UserCards");

            migrationBuilder.DropIndex(
                name: "IX_UserCards_CardPrintingId",
                table: "UserCards");

            migrationBuilder.DropIndex(
                name: "IX_Decks_UserId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "UserCards");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Decks");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserCards",
                table: "UserCards",
                column: "CardPrintingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserCards",
                table: "UserCards");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "UserCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Decks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserCards",
                table: "UserCards",
                columns: new[] { "UserId", "CardPrintingId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCards_CardPrintingId",
                table: "UserCards",
                column: "CardPrintingId");

            migrationBuilder.CreateIndex(
                name: "IX_Decks_UserId",
                table: "Decks",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Decks_Users_UserId",
                table: "Decks",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCards_Users_UserId",
                table: "UserCards",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
