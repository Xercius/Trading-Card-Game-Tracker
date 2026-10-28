using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeCardsGameAndAssociations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<int>(
                name: "GameId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "GameId" = (
                    SELECT g."Id"
                    FROM "Games" g
                    WHERE g."NameNormalized" = LOWER(TRIM("Cards"."Game")))
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "CardTypes" ("GameId", "Name")
                SELECT c."GameId", TRIM(c."CardType")
                FROM "Cards" c
                WHERE c."GameId" IS NOT NULL
                  AND TRIM(COALESCE(c."CardType", '')) <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "CardTypes" t
                      WHERE t."GameId" = c."GameId"
                        AND LOWER(TRIM(t."Name")) = LOWER(TRIM(c."CardType")))
                GROUP BY c."GameId", LOWER(TRIM(c."CardType"))
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "CardCardTypes" ("CardId", "CardTypeId", "SourceRole")
                SELECT c."Id", t."Id", 'Type'
                FROM "Cards" c
                JOIN "CardTypes" t ON t."GameId" = c."GameId"
                    AND LOWER(TRIM(t."Name")) = LOWER(TRIM(c."CardType"))
                WHERE TRIM(COALESCE(c."CardType", '')) <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "CardCardTypes" ct
                      WHERE ct."CardId" = c."Id" AND ct."SourceRole" = 'Type')
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "CardTypes" ("GameId", "Name")
                SELECT c."GameId", TRIM(c."CardType2")
                FROM "Cards" c
                WHERE c."GameId" IS NOT NULL
                  AND TRIM(COALESCE(c."CardType2", '')) <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "CardTypes" t
                      WHERE t."GameId" = c."GameId"
                        AND LOWER(TRIM(t."Name")) = LOWER(TRIM(c."CardType2")))
                GROUP BY c."GameId", LOWER(TRIM(c."CardType2"))
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "CardCardTypes" ("CardId", "CardTypeId", "SourceRole")
                SELECT c."Id", t."Id", 'Type2'
                FROM "Cards" c
                JOIN "CardTypes" t ON t."GameId" = c."GameId"
                    AND LOWER(TRIM(t."Name")) = LOWER(TRIM(c."CardType2"))
                WHERE TRIM(COALESCE(c."CardType2", '')) <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM "CardCardTypes" ct
                      WHERE ct."CardId" = c."Id" AND ct."SourceRole" = 'Type2')
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Cards_BaseCardId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_BaseCardId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Game_Name",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Game_StrapiId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_GameNorm",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "GameNorm",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ApiCreatedAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ApiUpdatedAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Arena",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Aspects",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BaseCardId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BaseStrapiId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CardType",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CardType2",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "DetailsJson",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Game",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Traits",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "StrapiId",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "UX_Cards_GameId_Name",
                table: "Cards",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Games_GameId",
                table: "Cards",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Games_GameId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "UX_Cards_GameId_Name",
                table: "Cards");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApiCreatedAt",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApiUpdatedAt",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Arena",
                table: "Cards",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aspects",
                table: "Cards",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseCardId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseStrapiId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardType",
                table: "Cards",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CardType2",
                table: "Cards",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Game",
                table: "Cards",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "Game" = COALESCE(
                    (SELECT "Name" FROM "Games" WHERE "Games"."Id" = "Cards"."GameId"),
                    '')
                """);

            migrationBuilder.AddColumn<int>(
                name: "StrapiId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keywords",
                table: "Cards",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Traits",
                table: "Cards",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "CardType" = COALESCE((
                        SELECT t."Name"
                        FROM "CardCardTypes" ct
                        JOIN "CardTypes" t ON t."Id" = ct."CardTypeId"
                        WHERE ct."CardId" = "Cards"."Id" AND ct."SourceRole" = 'Type'
                        ORDER BY ct."CardTypeId" LIMIT 1), ''),
                    "CardType2" = (
                        SELECT t."Name"
                        FROM "CardCardTypes" ct
                        JOIN "CardTypes" t ON t."Id" = ct."CardTypeId"
                        WHERE ct."CardId" = "Cards"."Id" AND ct."SourceRole" = 'Type2'
                        ORDER BY ct."CardTypeId" LIMIT 1)
                """);

            migrationBuilder.AddColumn<string>(
                name: "GameNorm",
                table: "Cards",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "LOWER(TRIM([Game]))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_BaseCardId",
                table: "Cards",
                column: "BaseCardId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Game_Name",
                table: "Cards",
                columns: new[] { "Game", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Game_StrapiId",
                table: "Cards",
                columns: new[] { "Game", "StrapiId" },
                unique: true,
                filter: "\"StrapiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_GameNorm",
                table: "Cards",
                column: "GameNorm");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Cards_BaseCardId",
                table: "Cards",
                column: "BaseCardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropColumn(
                name: "GameId",
                table: "Cards");
        }
    }
}
