using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddSwuCardUidFilteredUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SwuCards_CardUid",
                table: "SwuCards");

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_CardUid",
                table: "SwuCards",
                column: "CardUid",
                unique: true,
                filter: "\"CardUid\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SwuCards_CardUid",
                table: "SwuCards");

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_CardUid",
                table: "SwuCards",
                column: "CardUid",
                unique: true);
        }
    }
}
