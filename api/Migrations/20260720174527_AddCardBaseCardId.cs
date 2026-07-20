using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddCardBaseCardId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BaseCardId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_BaseCardId",
                table: "Cards",
                column: "BaseCardId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Cards_BaseCardId",
                table: "Cards",
                column: "BaseCardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Cards_BaseCardId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_BaseCardId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BaseCardId",
                table: "Cards");
        }
    }
}
