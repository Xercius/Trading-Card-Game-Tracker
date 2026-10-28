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

            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "_CardConsolidation" AS
                SELECT
                    c."Id" AS "DuplicateCardId",
                    MIN(c."Id") OVER (PARTITION BY c."GameId", c."Name") AS "CanonicalCardId"
                FROM "Cards" c
                WHERE c."GameId" IS NOT NULL;

                CREATE TEMP TABLE "_PrintingConsolidation" AS
                WITH RankedPrintings AS (
                    SELECT
                        p."Id",
                        FIRST_VALUE(p."Id") OVER (
                            PARTITION BY map."CanonicalCardId", p."Set", p."Number", p."TreatmentId"
                            ORDER BY
                                (SELECT COUNT(*) FROM "UserCards" uc WHERE uc."CardPrintingId" = p."Id") +
                                (SELECT COUNT(*) FROM "DeckCards" dc WHERE dc."CardPrintingId" = p."Id") DESC,
                                p."Id"
                        ) AS "CanonicalPrintingId"
                    FROM "CardPrintings" p
                    JOIN "_CardConsolidation" map ON map."DuplicateCardId" = p."CardId"
                    WHERE p."TreatmentId" IS NOT NULL
                )
                SELECT "Id" AS "DuplicatePrintingId", "CanonicalPrintingId"
                FROM RankedPrintings
                WHERE "Id" <> "CanonicalPrintingId";

                INSERT INTO "UserCards"
                    ("UserId", "CardPrintingId", "QuantityOwned", "QuantityWanted", "QuantityProxyOwned")
                SELECT
                    entry."UserId", map."CanonicalPrintingId",
                    entry."QuantityOwned", entry."QuantityWanted", entry."QuantityProxyOwned"
                FROM "_PrintingConsolidation" map
                JOIN "UserCards" entry ON entry."CardPrintingId" = map."DuplicatePrintingId"
                WHERE 1
                ON CONFLICT ("UserId", "CardPrintingId") DO UPDATE SET
                    "QuantityOwned" = "UserCards"."QuantityOwned" + excluded."QuantityOwned",
                    "QuantityWanted" = "UserCards"."QuantityWanted" + excluded."QuantityWanted",
                    "QuantityProxyOwned" = "UserCards"."QuantityProxyOwned" + excluded."QuantityProxyOwned";

                INSERT INTO "DeckCards"
                    ("DeckId", "CardPrintingId", "QuantityInDeck", "QuantityIdea", "QuantityProxy", "QuantityAcquire")
                SELECT
                    entry."DeckId", map."CanonicalPrintingId",
                    entry."QuantityInDeck", entry."QuantityIdea", entry."QuantityProxy", entry."QuantityAcquire"
                FROM "_PrintingConsolidation" map
                JOIN "DeckCards" entry ON entry."CardPrintingId" = map."DuplicatePrintingId"
                WHERE 1
                ON CONFLICT ("DeckId", "CardPrintingId") DO UPDATE SET
                    "QuantityInDeck" = "DeckCards"."QuantityInDeck" + excluded."QuantityInDeck",
                    "QuantityIdea" = "DeckCards"."QuantityIdea" + excluded."QuantityIdea",
                    "QuantityProxy" = "DeckCards"."QuantityProxy" + excluded."QuantityProxy",
                    "QuantityAcquire" = "DeckCards"."QuantityAcquire" + excluded."QuantityAcquire";

                DELETE FROM "CardPriceHistories"
                WHERE "CardPrintingId" IN (SELECT "DuplicatePrintingId" FROM "_PrintingConsolidation")
                  AND EXISTS (
                      SELECT 1
                      FROM "_PrintingConsolidation" map
                      JOIN "CardPriceHistories" existing
                        ON existing."CardPrintingId" = map."CanonicalPrintingId"
                       AND existing."CapturedAt" = "CardPriceHistories"."CapturedAt"
                      WHERE map."DuplicatePrintingId" = "CardPriceHistories"."CardPrintingId");
                UPDATE "CardPriceHistories"
                SET "CardPrintingId" = (
                    SELECT map."CanonicalPrintingId"
                    FROM "_PrintingConsolidation" map
                    WHERE map."DuplicatePrintingId" = "CardPriceHistories"."CardPrintingId"
                )
                WHERE "CardPrintingId" IN (SELECT "DuplicatePrintingId" FROM "_PrintingConsolidation");

                DELETE FROM "UserCards"
                WHERE "CardPrintingId" IN (SELECT "DuplicatePrintingId" FROM "_PrintingConsolidation");
                DELETE FROM "DeckCards"
                WHERE "CardPrintingId" IN (SELECT "DuplicatePrintingId" FROM "_PrintingConsolidation");

                UPDATE "CardPrintings"
                SET "VariantOfId" = (
                    SELECT map."CanonicalPrintingId"
                    FROM "_PrintingConsolidation" map
                    WHERE map."DuplicatePrintingId" = "CardPrintings"."VariantOfId"
                )
                WHERE "VariantOfId" IN (SELECT "DuplicatePrintingId" FROM "_PrintingConsolidation");
                UPDATE "CardPrintings"
                SET "ReprintOfId" = (
                    SELECT map."CanonicalPrintingId"
                    FROM "_PrintingConsolidation" map
                    WHERE map."DuplicatePrintingId" = "CardPrintings"."ReprintOfId"
                )
                WHERE "ReprintOfId" IN (SELECT "DuplicatePrintingId" FROM "_PrintingConsolidation");

                DELETE FROM "CardPrintings"
                WHERE "Id" IN (SELECT "DuplicatePrintingId" FROM "_PrintingConsolidation");
                UPDATE "CardPrintings"
                SET "CardId" = (
                    SELECT map."CanonicalCardId"
                    FROM "_CardConsolidation" map
                    WHERE map."DuplicateCardId" = "CardPrintings"."CardId"
                )
                WHERE "CardId" IN (
                    SELECT "DuplicateCardId" FROM "_CardConsolidation"
                    WHERE "DuplicateCardId" <> "CanonicalCardId"
                );

                INSERT INTO "CardCardTypes" ("CardId", "CardTypeId", "SourceRole")
                SELECT map."CanonicalCardId", association."CardTypeId", association."SourceRole"
                FROM "_CardConsolidation" map
                JOIN "CardCardTypes" association ON association."CardId" = map."DuplicateCardId"
                WHERE map."DuplicateCardId" <> map."CanonicalCardId"
                ON CONFLICT DO NOTHING;

                INSERT INTO "CardTraits" ("CardId", "TraitId")
                SELECT map."CanonicalCardId", association."TraitId"
                FROM "_CardConsolidation" map
                JOIN "CardTraits" association ON association."CardId" = map."DuplicateCardId"
                WHERE map."DuplicateCardId" <> map."CanonicalCardId"
                ON CONFLICT DO NOTHING;

                INSERT INTO "CardKeywords" ("CardId", "KeywordId")
                SELECT map."CanonicalCardId", association."KeywordId"
                FROM "_CardConsolidation" map
                JOIN "CardKeywords" association ON association."CardId" = map."DuplicateCardId"
                WHERE map."DuplicateCardId" <> map."CanonicalCardId"
                ON CONFLICT DO NOTHING;

                INSERT INTO "CardArenas" ("CardId", "ArenaId")
                SELECT map."CanonicalCardId", association."ArenaId"
                FROM "_CardConsolidation" map
                JOIN "CardArenas" association ON association."CardId" = map."DuplicateCardId"
                WHERE map."DuplicateCardId" <> map."CanonicalCardId"
                ON CONFLICT DO NOTHING;

                INSERT INTO "CardAspects" ("CardId", "AspectId", "Count")
                SELECT map."CanonicalCardId", association."AspectId", association."Count"
                FROM "_CardConsolidation" map
                JOIN "CardAspects" association ON association."CardId" = map."DuplicateCardId"
                WHERE map."DuplicateCardId" <> map."CanonicalCardId"
                ON CONFLICT ("CardId", "AspectId")
                DO UPDATE SET "Count" = "CardAspects"."Count" + excluded."Count";

                DELETE FROM "CardCardTypes"
                WHERE "CardId" IN (SELECT "DuplicateCardId" FROM "_CardConsolidation"
                    WHERE "DuplicateCardId" <> "CanonicalCardId");
                DELETE FROM "CardTraits"
                WHERE "CardId" IN (SELECT "DuplicateCardId" FROM "_CardConsolidation"
                    WHERE "DuplicateCardId" <> "CanonicalCardId");
                DELETE FROM "CardKeywords"
                WHERE "CardId" IN (SELECT "DuplicateCardId" FROM "_CardConsolidation"
                    WHERE "DuplicateCardId" <> "CanonicalCardId");
                DELETE FROM "CardArenas"
                WHERE "CardId" IN (SELECT "DuplicateCardId" FROM "_CardConsolidation"
                    WHERE "DuplicateCardId" <> "CanonicalCardId");
                DELETE FROM "CardAspects"
                WHERE "CardId" IN (SELECT "DuplicateCardId" FROM "_CardConsolidation"
                    WHERE "DuplicateCardId" <> "CanonicalCardId");
                DELETE FROM "Cards"
                WHERE "Id" IN (SELECT "DuplicateCardId" FROM "_CardConsolidation"
                    WHERE "DuplicateCardId" <> "CanonicalCardId");

                DROP TABLE "_CardConsolidation";
                DROP TABLE "_PrintingConsolidation";
                """);

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

            migrationBuilder.Sql(
                """
                DROP INDEX "IX_Cards_NameNormalized";
                PRAGMA legacy_alter_table = ON;
                ALTER TABLE "Cards" RENAME TO "Cards_Normalized";
                PRAGMA legacy_alter_table = OFF;

                CREATE TABLE "Cards" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Cards" PRIMARY KEY AUTOINCREMENT,
                    "GameId" INTEGER NULL,
                    "ApiCreatedAt" TEXT NULL,
                    "ApiUpdatedAt" TEXT NULL,
                    "Arena" TEXT NULL,
                    "Aspects" TEXT NULL,
                    "BaseCardId" INTEGER NULL,
                    "BaseStrapiId" INTEGER NULL,
                    "CardType" TEXT NOT NULL,
                    "CardType2" TEXT NULL,
                    "CardUid" TEXT NULL,
                    "Cost" INTEGER NULL,
                    "DeployBox" TEXT NULL,
                    "DeployBoxStyled" TEXT NULL,
                    "DetailsJson" TEXT NULL,
                    "EpicAction" TEXT NULL,
                    "EpicActionStyled" TEXT NULL,
                    "Game" TEXT NOT NULL,
                    "GameNorm" TEXT GENERATED ALWAYS AS (LOWER(TRIM("Game"))) STORED,
                    "Hp" INTEGER NULL,
                    "Keywords" TEXT NULL,
                    "LastSyncedAt" TEXT NULL,
                    "Name" TEXT NOT NULL,
                    "NameNormalized" TEXT GENERATED ALWAYS AS (UPPER(LTRIM(RTRIM("Name")))) STORED,
                    "Power" INTEGER NULL,
                    "Rules" TEXT NULL,
                    "RulesStyled" TEXT NULL,
                    "StrapiId" INTEGER NULL,
                    "Subtitle" TEXT NULL,
                    "Text" TEXT NULL,
                    "TextStyled" TEXT NULL,
                    "Traits" TEXT NULL,
                    "UpgradeHp" INTEGER NULL,
                    "UpgradePower" INTEGER NULL
                );

                INSERT INTO "Cards" (
                    "Id", "GameId", "ApiCreatedAt", "ApiUpdatedAt", "Arena", "Aspects",
                    "BaseCardId", "BaseStrapiId", "CardType", "CardType2", "CardUid", "Cost",
                    "DeployBox", "DeployBoxStyled", "DetailsJson", "EpicAction", "EpicActionStyled",
                    "Game", "Hp", "Keywords", "LastSyncedAt", "Name", "Power", "Rules", "RulesStyled",
                    "StrapiId", "Subtitle", "Text", "TextStyled", "Traits", "UpgradeHp", "UpgradePower"
                )
                SELECT
                    "Id", "GameId", "ApiCreatedAt", "ApiUpdatedAt", "Arena", "Aspects",
                    "BaseCardId", "BaseStrapiId", "CardType", "CardType2", "CardUid", "Cost",
                    "DeployBox", "DeployBoxStyled", "DetailsJson", "EpicAction", "EpicActionStyled",
                    "Game", "Hp", "Keywords", "LastSyncedAt", "Name", "Power", "Rules", "RulesStyled",
                    "StrapiId", "Subtitle", "Text", "TextStyled", "Traits", "UpgradeHp", "UpgradePower"
                FROM "Cards_Normalized";

                DROP TABLE "Cards_Normalized";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_NameNormalized",
                table: "Cards",
                column: "NameNormalized");

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
