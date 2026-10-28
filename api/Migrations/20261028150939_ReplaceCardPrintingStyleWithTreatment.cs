using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCardPrintingStyleWithTreatment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Treatments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NameNormalized = table.Column<string>(type: "TEXT", nullable: false, computedColumnSql: "LOWER(TRIM([Name]))", stored: true),
                    IconUrl = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treatments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Treatments_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "Games" ("Name")
                SELECT MIN(TRIM(c."Game"))
                FROM "Cards" c
                WHERE TRIM(c."Game") <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "Games" g
                      WHERE g."NameNormalized" = LOWER(TRIM(c."Game")))
                GROUP BY LOWER(TRIM(c."Game"))
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "Treatments" ("GameId", "Name")
                SELECT g."Id",
                       MIN(CASE WHEN TRIM(cp."Style") = '' THEN 'Standard' ELSE TRIM(cp."Style") END)
                FROM "CardPrintings" cp
                JOIN "Cards" c ON c."Id" = cp."CardId"
                JOIN "Games" g ON g."NameNormalized" = LOWER(TRIM(c."Game"))
                GROUP BY g."Id", LOWER(CASE WHEN TRIM(cp."Style") = '' THEN 'Standard' ELSE TRIM(cp."Style") END)
                """);

            migrationBuilder.AddColumn<int>(
                name: "TreatmentId",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "CardPrintings"
                SET "TreatmentId" = (
                    SELECT t."Id"
                    FROM "Cards" c
                    JOIN "Games" g ON g."NameNormalized" = LOWER(TRIM(c."Game"))
                    JOIN "Treatments" t ON t."GameId" = g."Id"
                        AND t."NameNormalized" = LOWER(TRIM(
                            CASE WHEN TRIM("CardPrintings"."Style") = ''
                                THEN 'Standard' ELSE TRIM("CardPrintings"."Style") END))
                    WHERE c."Id" = "CardPrintings"."CardId")
                """);

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_CardId_Set_Number_Style",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_StyleNorm",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "StyleNorm",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "Style",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "Hyperspace",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "Showcase",
                table: "CardPrintings");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_CardId_Set_Number_TreatmentId",
                table: "CardPrintings",
                columns: new[] { "CardId", "Set", "Number", "TreatmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_TreatmentId",
                table: "CardPrintings",
                column: "TreatmentId");

            migrationBuilder.CreateIndex(
                name: "UX_Treatments_GameId_NameNormalized",
                table: "Treatments",
                columns: new[] { "GameId", "NameNormalized" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CardPrintings_Treatments_TreatmentId",
                table: "CardPrintings",
                column: "TreatmentId",
                principalTable: "Treatments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardPrintings_Treatments_TreatmentId",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_CardId_Set_Number_TreatmentId",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_TreatmentId",
                table: "CardPrintings");

            migrationBuilder.AddColumn<bool>(
                name: "Hyperspace",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Style",
                table: "CardPrintings",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "CardPrintings"
                SET "Style" = COALESCE(
                    (SELECT "Name" FROM "Treatments" WHERE "Treatments"."Id" = "CardPrintings"."TreatmentId"),
                    'Standard')
                """);

            migrationBuilder.AddColumn<bool>(
                name: "Showcase",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "CardPrintings"
                SET "Hyperspace" = CASE
                        WHEN "Style" LIKE '%Hyperspace%' THEN 1 ELSE NULL END,
                    "Showcase" = CASE
                        WHEN "Style" LIKE '%Showcase%' THEN 1 ELSE NULL END
                """);

            migrationBuilder.AddColumn<string>(
                name: "StyleNorm",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "LOWER(TRIM([Style]))",
                stored: true);

            migrationBuilder.DropColumn(
                name: "TreatmentId",
                table: "CardPrintings");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_CardId_Set_Number_Style",
                table: "CardPrintings",
                columns: new[] { "CardId", "Set", "Number", "Style" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_StyleNorm",
                table: "CardPrintings",
                column: "StyleNorm");

            migrationBuilder.DropTable(
                name: "Treatments");
        }
    }
}
