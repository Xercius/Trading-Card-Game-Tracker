using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class UserCardRestrictDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCards_CardPrintings_CardPrintingId",
                table: "UserCards");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCards_CardPrintings_CardPrintingId",
                table: "UserCards",
                column: "CardPrintingId",
                principalTable: "CardPrintings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCards_CardPrintings_CardPrintingId",
                table: "UserCards");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCards_CardPrintings_CardPrintingId",
                table: "UserCards",
                column: "CardPrintingId",
                principalTable: "CardPrintings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
