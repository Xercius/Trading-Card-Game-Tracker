using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddImportSyncHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportSyncHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImporterKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SetCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSyncHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportSyncHistories_ImporterKey_SetCode",
                table: "ImportSyncHistories",
                columns: new[] { "ImporterKey", "SetCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportSyncHistories");
        }
    }
}
