using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddValueHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValueHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeType = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeId = table.Column<int>(type: "INTEGER", nullable: false),
                    PriceCents = table.Column<long>(type: "INTEGER", nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValueHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValueHistories_ScopeType_ScopeId_AsOfUtc",
                table: "ValueHistories",
                columns: new[] { "ScopeType", "ScopeId", "AsOfUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValueHistories");
        }
    }
}
