using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSwuTablesAndCardDetailsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SwuCardPrintings");

            migrationBuilder.DropTable(
                name: "SwuCards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_CardUid",
                table: "Cards");

            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "_SwuCardConsolidation" AS
                WITH PrintingPriority AS (
                    SELECT
                        p."CardId",
                        CASE
                            WHEN json_valid(p."DetailsJson") THEN CASE
                                WHEN json_type(p."DetailsJson", '$.variantOfSourceId') IS NOT NULL
                                    OR json_type(p."DetailsJson", '$.variantOfCardUid') IS NOT NULL
                                    OR json_type(p."DetailsJson", '$.reprintOfSourceId') IS NOT NULL
                                    OR json_type(p."DetailsJson", '$.reprintOfCardUid') IS NOT NULL
                                THEN CASE
                                    WHEN CAST(json_extract(p."DetailsJson", '$.variantOfSourceId') AS INTEGER) > 0
                                        OR NULLIF(json_extract(p."DetailsJson", '$.variantOfCardUid'), '') IS NOT NULL
                                        OR CAST(json_extract(p."DetailsJson", '$.reprintOfSourceId') AS INTEGER) > 0
                                        OR NULLIF(json_extract(p."DetailsJson", '$.reprintOfCardUid'), '') IS NOT NULL
                                    THEN 0 ELSE 1
                                END
                                WHEN p."VariantOfId" IS NOT NULL OR p."ReprintOfId" IS NOT NULL THEN 0
                                ELSE 1
                            END
                            WHEN p."VariantOfId" IS NOT NULL OR p."ReprintOfId" IS NOT NULL THEN 0
                            ELSE 1
                        END AS "IsBase",
                        COALESCE(
                            p."StrapiId",
                            CASE WHEN json_valid(p."DetailsJson")
                                THEN CAST(json_extract(p."DetailsJson", '$.sourceId') AS INTEGER)
                            END
                        ) AS "SourceId"
                    FROM "CardPrintings" p
                    JOIN "Cards" c ON c."Id" = p."CardId"
                    WHERE c."GameNorm" = 'star wars unlimited'
                ),
                CardPriority AS (
                    SELECT
                        c."Id",
                        c."GameNorm",
                        c."NameNormalized",
                        COALESCE(MAX(pp."IsBase"), 0) AS "HasBase",
                        MIN(CASE WHEN pp."IsBase" = 1 THEN pp."SourceId" END) AS "FirstBaseSourceId",
                        MIN(pp."SourceId") AS "FirstSourceId"
                    FROM "Cards" c
                    LEFT JOIN PrintingPriority pp ON pp."CardId" = c."Id"
                    WHERE c."GameNorm" = 'star wars unlimited'
                    GROUP BY c."Id", c."GameNorm", c."NameNormalized"
                ),
                RankedCards AS (
                    SELECT
                        "Id",
                        FIRST_VALUE("Id") OVER (
                            PARTITION BY "GameNorm", "NameNormalized"
                            ORDER BY "Id"
                        ) AS "CanonicalCardId",
                        FIRST_VALUE("Id") OVER (
                            PARTITION BY "GameNorm", "NameNormalized"
                            ORDER BY
                                "HasBase" DESC,
                                COALESCE(
                                    CASE WHEN "HasBase" = 1 THEN "FirstBaseSourceId" ELSE "FirstSourceId" END,
                                    2147483647
                                ),
                                "Id"
                        ) AS "MetadataCardId"
                    FROM CardPriority
                )
                SELECT
                    "Id" AS "DuplicateCardId",
                    "CanonicalCardId",
                    "MetadataCardId"
                FROM RankedCards;

                UPDATE "Cards"
                SET (
                    "Name", "CardType", "Description", "CardUid", "Subtitle", "CardType2",
                    "TextStyled", "DeployBox", "DeployBoxStyled", "EpicAction", "EpicActionStyled",
                    "Rules", "RulesStyled", "Arena", "Cost", "Power", "Hp", "Artist", "Aspects",
                    "Traits", "Keywords"
                ) = (
                    SELECT
                        metadata."Name", metadata."CardType", metadata."Description", metadata."CardUid",
                        metadata."Subtitle", metadata."CardType2", metadata."TextStyled", metadata."DeployBox",
                        metadata."DeployBoxStyled", metadata."EpicAction", metadata."EpicActionStyled",
                        metadata."Rules", metadata."RulesStyled", metadata."Arena", metadata."Cost",
                        metadata."Power", metadata."Hp", metadata."Artist", metadata."Aspects",
                        metadata."Traits", metadata."Keywords"
                    FROM "_SwuCardConsolidation" map
                    JOIN "Cards" metadata ON metadata."Id" = map."MetadataCardId"
                    WHERE map."CanonicalCardId" = "Cards"."Id"
                    LIMIT 1
                )
                WHERE "Id" IN (
                    SELECT "CanonicalCardId"
                    FROM "_SwuCardConsolidation"
                    WHERE "CanonicalCardId" <> "MetadataCardId"
                );

                INSERT OR IGNORE INTO "CardAspects" ("CardId", "AspectId", "Count")
                SELECT map."CanonicalCardId", association."AspectId", association."Count"
                FROM "_SwuCardConsolidation" map
                JOIN "CardAspects" association ON association."CardId" = map."MetadataCardId"
                WHERE map."CanonicalCardId" <> map."MetadataCardId";

                INSERT OR IGNORE INTO "CardCardTypes" ("CardId", "CardTypeId", "SourceRole")
                SELECT map."CanonicalCardId", association."CardTypeId", association."SourceRole"
                FROM "_SwuCardConsolidation" map
                JOIN "CardCardTypes" association ON association."CardId" = map."MetadataCardId"
                WHERE map."CanonicalCardId" <> map."MetadataCardId";

                INSERT OR IGNORE INTO "CardTraits" ("CardId", "TraitId")
                SELECT map."CanonicalCardId", association."TraitId"
                FROM "_SwuCardConsolidation" map
                JOIN "CardTraits" association ON association."CardId" = map."MetadataCardId"
                WHERE map."CanonicalCardId" <> map."MetadataCardId";

                INSERT OR IGNORE INTO "CardKeywords" ("CardId", "KeywordId")
                SELECT map."CanonicalCardId", association."KeywordId"
                FROM "_SwuCardConsolidation" map
                JOIN "CardKeywords" association ON association."CardId" = map."MetadataCardId"
                WHERE map."CanonicalCardId" <> map."MetadataCardId";

                INSERT OR IGNORE INTO "CardArenas" ("CardId", "ArenaId")
                SELECT map."CanonicalCardId", association."ArenaId"
                FROM "_SwuCardConsolidation" map
                JOIN "CardArenas" association ON association."CardId" = map."MetadataCardId"
                WHERE map."CanonicalCardId" <> map."MetadataCardId";

                UPDATE "CardPrintings"
                SET "CardId" = (
                    SELECT map."CanonicalCardId"
                    FROM "_SwuCardConsolidation" map
                    WHERE map."DuplicateCardId" = "CardPrintings"."CardId"
                )
                WHERE "CardId" IN (
                    SELECT "DuplicateCardId"
                    FROM "_SwuCardConsolidation"
                    WHERE "DuplicateCardId" <> "CanonicalCardId"
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM "_SwuCardConsolidation" map
                    JOIN "CardPrintings" existing
                        ON existing."CardId" = map."CanonicalCardId"
                        AND existing."Set" = "CardPrintings"."Set"
                        AND existing."Number" = "CardPrintings"."Number"
                        AND existing."Style" = "CardPrintings"."Style"
                    WHERE map."DuplicateCardId" = "CardPrintings"."CardId"
                );

                UPDATE "Cards"
                SET "BaseCardId" = (
                    SELECT map."CanonicalCardId"
                    FROM "_SwuCardConsolidation" map
                    WHERE map."DuplicateCardId" = "Cards"."BaseCardId"
                )
                WHERE "GameNorm" = 'star wars unlimited'
                    AND "BaseCardId" IN (
                        SELECT "DuplicateCardId"
                        FROM "_SwuCardConsolidation"
                        WHERE "DuplicateCardId" <> "CanonicalCardId"
                    );

                UPDATE "Cards"
                SET "BaseCardId" = NULL
                WHERE "GameNorm" = 'star wars unlimited'
                    AND "BaseCardId" = "Id";

                DELETE FROM "Cards"
                WHERE "GameNorm" = 'star wars unlimited'
                    AND "Id" IN (
                        SELECT "DuplicateCardId"
                        FROM "_SwuCardConsolidation"
                        WHERE "DuplicateCardId" <> "CanonicalCardId"
                    )
                    AND NOT EXISTS (
                        SELECT 1 FROM "CardPrintings"
                        WHERE "CardPrintings"."CardId" = "Cards"."Id"
                    )
                    AND NOT EXISTS (
                        SELECT 1 FROM "Cards" related
                        WHERE related."BaseCardId" = "Cards"."Id"
                    );

                DROP TABLE "_SwuCardConsolidation";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardUid",
                table: "Cards",
                column: "CardUid",
                unique: true,
                filter: "\"CardUid\" IS NOT NULL");

            migrationBuilder.CreateTable(
                name: "SwuCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BaseCardId = table.Column<int>(type: "INTEGER", nullable: true),
                    SetId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiCreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ApiUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Arena = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Artist = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Aspects = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    BaseStrapiId = table.Column<int>(type: "INTEGER", nullable: true),
                    CardType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CardUid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Cost = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Health = table.Column<int>(type: "INTEGER", nullable: true),
                    Keywords = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Power = table.Column<int>(type: "INTEGER", nullable: true),
                    StrapiId = table.Column<int>(type: "INTEGER", nullable: false),
                    Subtitle = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Traits = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwuCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SwuCards_Sets_SetId",
                        column: x => x.SetId,
                        principalTable: "Sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwuCards_SwuCards_BaseCardId",
                        column: x => x.BaseCardId,
                        principalTable: "SwuCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SwuCardPrintings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SetId = table.Column<int>(type: "INTEGER", nullable: false),
                    SwuCardId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiCreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ApiUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    BackImageUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StrapiId = table.Column<int>(type: "INTEGER", nullable: false),
                    Style = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwuCardPrintings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SwuCardPrintings_Sets_SetId",
                        column: x => x.SetId,
                        principalTable: "Sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwuCardPrintings_SwuCards_SwuCardId",
                        column: x => x.SwuCardId,
                        principalTable: "SwuCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SwuCardPrintings_SetId_Number",
                table: "SwuCardPrintings",
                columns: new[] { "SetId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_SwuCardPrintings_StrapiId",
                table: "SwuCardPrintings",
                column: "StrapiId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwuCardPrintings_SwuCardId_SetId_Number_Style",
                table: "SwuCardPrintings",
                columns: new[] { "SwuCardId", "SetId", "Number", "Style" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_BaseCardId",
                table: "SwuCards",
                column: "BaseCardId");

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_CardUid",
                table: "SwuCards",
                column: "CardUid",
                unique: true,
                filter: "\"CardUid\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_SetId_Title_Subtitle",
                table: "SwuCards",
                columns: new[] { "SetId", "Title", "Subtitle" });

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_StrapiId",
                table: "SwuCards",
                column: "StrapiId",
                unique: true);
        }
    }
}
