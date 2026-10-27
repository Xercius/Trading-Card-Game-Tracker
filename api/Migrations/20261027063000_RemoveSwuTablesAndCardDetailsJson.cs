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
                UPDATE "CardPrintings"
                SET "CardId" = (
                    SELECT MIN(c2."Id")
                    FROM "Cards" c1
                    JOIN "Cards" c2 ON c2."GameNorm" = c1."GameNorm" AND c2."NameNormalized" = c1."NameNormalized"
                    WHERE c1."Id" = "CardPrintings"."CardId"
                )
                WHERE EXISTS (
                    SELECT 1
                    FROM "Cards" c1
                    JOIN "Cards" c2 ON c2."GameNorm" = c1."GameNorm" AND c2."NameNormalized" = c1."NameNormalized"
                    WHERE c1."Id" = "CardPrintings"."CardId" AND c2."Id" < c1."Id"
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM "Cards" c1
                    JOIN "Cards" c2 ON c2."GameNorm" = c1."GameNorm" AND c2."NameNormalized" = c1."NameNormalized"
                    JOIN "CardPrintings" cp2 ON cp2."CardId" = c2."Id"
                        AND cp2."Set" = "CardPrintings"."Set"
                        AND cp2."Number" = "CardPrintings"."Number"
                        AND cp2."Style" = "CardPrintings"."Style"
                    WHERE c1."Id" = "CardPrintings"."CardId" AND c2."Id" < c1."Id"
                );

                UPDATE "Cards"
                SET "BaseCardId" = (
                    SELECT MIN(c2."Id")
                    FROM "Cards" c1
                    JOIN "Cards" c2 ON c2."GameNorm" = c1."GameNorm" AND c2."NameNormalized" = c1."NameNormalized"
                    WHERE c1."Id" = "Cards"."BaseCardId"
                )
                WHERE "BaseCardId" IS NOT NULL;

                UPDATE "Cards"
                SET "BaseCardId" = NULL
                WHERE "BaseCardId" = "Id";

                DELETE FROM "Cards"
                WHERE "Id" NOT IN (
                    SELECT MIN("Id")
                    FROM "Cards"
                    GROUP BY "GameNorm", "NameNormalized"
                );
                """);

            migrationBuilder.DropColumn(
                name: "DetailsJson",
                table: "Cards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                table: "Cards",
                type: "TEXT",
                nullable: true);

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
