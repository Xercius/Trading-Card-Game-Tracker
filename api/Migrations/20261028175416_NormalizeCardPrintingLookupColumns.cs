using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeCardPrintingLookupColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "Sets" ("GameId", "Code", "Name", "ReleasedAt", "LastSyncedAt")
                SELECT c."GameId", TRIM(cp."Set"), NULL, NULL, '1970-01-01T00:00:00+00:00'
                FROM "CardPrintings" cp
                JOIN "Cards" c ON c."Id" = cp."CardId"
                WHERE cp."SetId" IS NULL
                  AND c."GameId" IS NOT NULL
                  AND TRIM(cp."Set") <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "Sets" s
                      WHERE s."GameId" = c."GameId"
                        AND s."Code" = TRIM(cp."Set") COLLATE NOCASE)
                GROUP BY c."GameId", TRIM(cp."Set")
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "Rarities" ("GameId", "Name")
                SELECT c."GameId", TRIM(cp."Rarity")
                FROM "CardPrintings" cp
                JOIN "Cards" c ON c."Id" = cp."CardId"
                WHERE cp."RarityId" IS NULL
                  AND c."GameId" IS NOT NULL
                  AND TRIM(cp."Rarity") <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "Rarities" r
                      WHERE r."GameId" = c."GameId"
                        AND r."Name" = TRIM(cp."Rarity") COLLATE NOCASE)
                GROUP BY c."GameId", TRIM(cp."Rarity")
                """);

            migrationBuilder.Sql(
                """
                UPDATE "CardPrintings"
                SET "SetId" = (
                    SELECT s."Id"
                    FROM "Sets" s
                    JOIN "Cards" c ON c."GameId" = s."GameId"
                    WHERE c."Id" = "CardPrintings"."CardId"
                      AND s."Code" = TRIM("CardPrintings"."Set") COLLATE NOCASE
                    LIMIT 1)
                WHERE "SetId" IS NULL
                """);

            migrationBuilder.Sql(
                """
                UPDATE "CardPrintings"
                SET "RarityId" = (
                    SELECT r."Id"
                    FROM "Rarities" r
                    JOIN "Cards" c ON c."GameId" = r."GameId"
                    WHERE c."Id" = "CardPrintings"."CardId"
                      AND r."Name" = TRIM("CardPrintings"."Rarity") COLLATE NOCASE
                    LIMIT 1)
                WHERE "RarityId" IS NULL
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Sets"
                SET "ReleasedAt" = (
                    SELECT MIN(cp."ReleasedAt")
                    FROM "CardPrintings" cp
                    WHERE cp."SetId" = "Sets"."Id"
                      AND cp."ReleasedAt" IS NOT NULL)
                WHERE "ReleasedAt" IS NULL
                  AND EXISTS (
                    SELECT 1
                    FROM "CardPrintings" cp
                    WHERE cp."SetId" = "Sets"."Id"
                      AND cp."ReleasedAt" IS NOT NULL);

                """);

            migrationBuilder.Sql(
                """
                UPDATE "CardPrintings"
                SET "DetailsJson" = CASE
                    WHEN "DetailsJson" IS NULL THEN json_object('sourceId', "StrapiId")
                    WHEN json_valid("DetailsJson") AND json_type("DetailsJson") = 'object'
                        THEN json_set("DetailsJson", '$.sourceId', "StrapiId")
                    ELSE json_object('sourceId', "StrapiId", 'legacyDetails', "DetailsJson")
                END
                WHERE "StrapiId" IS NOT NULL
                """);

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_CardId_Set_Number_TreatmentId",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_RarityNorm",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_Set_Number",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_SetNorm",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_StrapiId",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "RarityNorm",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "SetNorm",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "Rarity",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "Set",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "StrapiId",
                table: "CardPrintings");

            migrationBuilder.CreateIndex(
                name: "UX_CardPrintings",
                table: "CardPrintings",
                columns: new[] { "CardId", "SetId", "Number", "TreatmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CardPrintings",
                table: "CardPrintings");

            migrationBuilder.AddColumn<string>(
                name: "Rarity",
                table: "CardPrintings",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleasedAt",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Set",
                table: "CardPrintings",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StrapiId",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "CardPrintings"
                SET "Set" = COALESCE((SELECT "Code" FROM "Sets" WHERE "Sets"."Id" = "CardPrintings"."SetId"), ''),
                    "Rarity" = COALESCE((SELECT "Name" FROM "Rarities" WHERE "Rarities"."Id" = "CardPrintings"."RarityId"), ''),
                    "ReleasedAt" = COALESCE(
                        (SELECT "ReleasedAt" FROM "Sets" WHERE "Sets"."Id" = "CardPrintings"."SetId"),
                        substr("ApiPublishedAt", 1, 10)),
                    "StrapiId" = CASE WHEN json_valid("DetailsJson")
                        THEN json_extract("DetailsJson", '$.sourceId') END
                """);

            migrationBuilder.AddColumn<string>(
                name: "RarityNorm",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "LOWER(TRIM([Rarity]))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "SetNorm",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "LOWER(TRIM([Set]))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_CardId_Set_Number_TreatmentId",
                table: "CardPrintings",
                columns: new[] { "CardId", "Set", "Number", "TreatmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_RarityNorm",
                table: "CardPrintings",
                column: "RarityNorm");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_Set_Number",
                table: "CardPrintings",
                columns: new[] { "Set", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_SetNorm",
                table: "CardPrintings",
                column: "SetNorm");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_StrapiId",
                table: "CardPrintings",
                column: "StrapiId",
                unique: true,
                filter: "\"StrapiId\" IS NOT NULL");
        }
    }
}
