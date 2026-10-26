using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedSwuCatalogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SwuCardPrintings_SwuSets_SwuSetId",
                table: "SwuCardPrintings");

            migrationBuilder.DropForeignKey(
                name: "FK_SwuCards_SwuSets_SwuSetId",
                table: "SwuCards");

            migrationBuilder.DropForeignKey(
                name: "FK_SyncLogs_SwuSets_SwuSetId",
                table: "SyncLogs");

            migrationBuilder.RenameTable(
                name: "SwuSets",
                newName: "Sets");

            migrationBuilder.DropIndex(
                name: "IX_SwuSets_Code",
                table: "Sets");

            migrationBuilder.RenameColumn(
                name: "SwuSetId",
                table: "SyncLogs",
                newName: "SetId");

            migrationBuilder.RenameIndex(
                name: "IX_SyncLogs_SwuSetId_StartedAt",
                table: "SyncLogs",
                newName: "IX_SyncLogs_SetId_StartedAt");

            migrationBuilder.RenameColumn(
                name: "SwuSetId",
                table: "SwuCards",
                newName: "SetId");

            migrationBuilder.RenameIndex(
                name: "IX_SwuCards_SwuSetId_Title_Subtitle",
                table: "SwuCards",
                newName: "IX_SwuCards_SetId_Title_Subtitle");

            migrationBuilder.RenameColumn(
                name: "SwuSetId",
                table: "SwuCardPrintings",
                newName: "SetId");

            migrationBuilder.RenameIndex(
                name: "IX_SwuCardPrintings_SwuSetId_Number",
                table: "SwuCardPrintings",
                newName: "IX_SwuCardPrintings_SetId_Number");

            migrationBuilder.RenameIndex(
                name: "IX_SwuCardPrintings_SwuCardId_SwuSetId_Number_Style",
                table: "SwuCardPrintings",
                newName: "IX_SwuCardPrintings_SwuCardId_SetId_Number_Style");

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
                name: "Artist",
                table: "Cards",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aspects",
                table: "Cards",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BaseStrapiId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardType2",
                table: "Cards",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardUid",
                table: "Cards",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Cost",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Hp",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keywords",
                table: "Cards",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Power",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StrapiId",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "Cards",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Traits",
                table: "Cards",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApiCreatedAt",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApiUpdatedAt",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackImageUrl",
                table: "CardPrintings",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "CardPrintings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RarityId",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetId",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StrapiId",
                table: "CardPrintings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NameNormalized = table.Column<string>(type: "TEXT", nullable: false, computedColumnSql: "LOWER(TRIM([Name]))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Games",
                columns: new[] { "Id", "Name" },
                values: new object[] { 1, "Star Wars Unlimited" });

            migrationBuilder.CreateTable(
                name: "Arenas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arenas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Arenas_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Aspects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aspects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aspects_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CardTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardTypes_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Keywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keywords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Keywords_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rarities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Character = table.Column<string>(type: "TEXT", maxLength: 1, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rarities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rarities_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<int>(
                name: "GameId",
                table: "Sets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "GameId",
                table: "Sets",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE "CardPrintings"
                SET "SetId" = (
                    SELECT "Sets"."Id"
                    FROM "Sets"
                    WHERE "Sets"."GameId" = 1
                      AND "Sets"."Code" = "CardPrintings"."Set" COLLATE NOCASE
                    LIMIT 1
                )
                WHERE EXISTS (
                    SELECT 1
                    FROM "Sets"
                    WHERE "Sets"."GameId" = 1
                      AND "Sets"."Code" = "CardPrintings"."Set" COLLATE NOCASE
                );
                """);

            migrationBuilder.CreateTable(
                name: "Traits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Traits_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CardArenas",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArenaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardArenas", x => new { x.CardId, x.ArenaId });
                    table.ForeignKey(
                        name: "FK_CardArenas_Arenas_ArenaId",
                        column: x => x.ArenaId,
                        principalTable: "Arenas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardArenas_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardAspects",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    AspectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardAspects", x => new { x.CardId, x.AspectId });
                    table.CheckConstraint("CK_CardAspects_Count_Positive", "\"Count\" > 0");
                    table.ForeignKey(
                        name: "FK_CardAspects_Aspects_AspectId",
                        column: x => x.AspectId,
                        principalTable: "Aspects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardAspects_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardCardTypes",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceRole = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardCardTypes", x => new { x.CardId, x.CardTypeId, x.SourceRole });
                    table.CheckConstraint("CK_CardCardTypes_SourceRole", "\"SourceRole\" IN ('Type', 'Type2')");
                    table.ForeignKey(
                        name: "FK_CardCardTypes_CardTypes_CardTypeId",
                        column: x => x.CardTypeId,
                        principalTable: "CardTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardCardTypes_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardKeywords",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    KeywordId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardKeywords", x => new { x.CardId, x.KeywordId });
                    table.ForeignKey(
                        name: "FK_CardKeywords_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardKeywords_Keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "Keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CardTraits",
                columns: table => new
                {
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    TraitId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardTraits", x => new { x.CardId, x.TraitId });
                    table.ForeignKey(
                        name: "FK_CardTraits_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardTraits_Traits_TraitId",
                        column: x => x.TraitId,
                        principalTable: "Traits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_CardUid",
                table: "Cards",
                column: "CardUid",
                unique: true,
                filter: "\"CardUid\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_Game_StrapiId",
                table: "Cards",
                columns: new[] { "Game", "StrapiId" },
                unique: true,
                filter: "\"StrapiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_RarityId",
                table: "CardPrintings",
                column: "RarityId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_SetId",
                table: "CardPrintings",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPrintings_StrapiId",
                table: "CardPrintings",
                column: "StrapiId",
                unique: true,
                filter: "\"StrapiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Arenas_GameId_Name",
                table: "Arenas",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aspects_GameId_Name",
                table: "Aspects",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardArenas_ArenaId",
                table: "CardArenas",
                column: "ArenaId");

            migrationBuilder.CreateIndex(
                name: "IX_CardAspects_AspectId",
                table: "CardAspects",
                column: "AspectId");

            migrationBuilder.CreateIndex(
                name: "IX_CardCardTypes_CardTypeId",
                table: "CardCardTypes",
                column: "CardTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CardKeywords_KeywordId",
                table: "CardKeywords",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_CardTraits_TraitId",
                table: "CardTraits",
                column: "TraitId");

            migrationBuilder.CreateIndex(
                name: "IX_CardTypes_GameId_Name",
                table: "CardTypes",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Games_NameNormalized",
                table: "Games",
                column: "NameNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_GameId_Name",
                table: "Keywords",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rarities_GameId_Name",
                table: "Rarities",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Sets_GameId_Code",
                table: "Sets",
                columns: new[] { "GameId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Traits_GameId_Name",
                table: "Traits",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CardPrintings_Rarities_RarityId",
                table: "CardPrintings",
                column: "RarityId",
                principalTable: "Rarities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CardPrintings_Sets_SetId",
                table: "CardPrintings",
                column: "SetId",
                principalTable: "Sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SwuCardPrintings_Sets_SetId",
                table: "SwuCardPrintings",
                column: "SetId",
                principalTable: "Sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SwuCards_Sets_SetId",
                table: "SwuCards",
                column: "SetId",
                principalTable: "Sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SyncLogs_Sets_SetId",
                table: "SyncLogs",
                column: "SetId",
                principalTable: "Sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sets_Games_GameId",
                table: "Sets");

            migrationBuilder.DropForeignKey(
                name: "FK_CardPrintings_Rarities_RarityId",
                table: "CardPrintings");

            migrationBuilder.DropForeignKey(
                name: "FK_CardPrintings_Sets_SetId",
                table: "CardPrintings");

            migrationBuilder.DropForeignKey(
                name: "FK_SwuCardPrintings_Sets_SetId",
                table: "SwuCardPrintings");

            migrationBuilder.DropForeignKey(
                name: "FK_SwuCards_Sets_SetId",
                table: "SwuCards");

            migrationBuilder.DropForeignKey(
                name: "FK_SyncLogs_Sets_SetId",
                table: "SyncLogs");

            migrationBuilder.DropTable(
                name: "CardArenas");

            migrationBuilder.DropTable(
                name: "CardAspects");

            migrationBuilder.DropTable(
                name: "CardCardTypes");

            migrationBuilder.DropTable(
                name: "CardKeywords");

            migrationBuilder.DropTable(
                name: "CardTraits");

            migrationBuilder.DropTable(
                name: "Rarities");

            migrationBuilder.DropIndex(
                name: "UX_Sets_GameId_Code",
                table: "Sets");

            migrationBuilder.RenameTable(
                name: "Sets",
                newName: "SwuSets");

            migrationBuilder.DropColumn(
                name: "GameId",
                table: "SwuSets");

            migrationBuilder.CreateIndex(
                name: "IX_SwuSets_Code",
                table: "SwuSets",
                column: "Code",
                unique: true);

            migrationBuilder.DropTable(
                name: "Arenas");

            migrationBuilder.DropTable(
                name: "Aspects");

            migrationBuilder.DropTable(
                name: "CardTypes");

            migrationBuilder.DropTable(
                name: "Keywords");

            migrationBuilder.DropTable(
                name: "Traits");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Cards_CardUid",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_Cards_Game_StrapiId",
                table: "Cards");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_RarityId",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_SetId",
                table: "CardPrintings");

            migrationBuilder.DropIndex(
                name: "IX_CardPrintings_StrapiId",
                table: "CardPrintings");

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
                name: "Artist",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Aspects",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BaseStrapiId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CardType2",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CardUid",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Hp",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Power",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "StrapiId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Subtitle",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Traits",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ApiCreatedAt",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "ApiUpdatedAt",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "BackImageUrl",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "RarityId",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "SetId",
                table: "CardPrintings");

            migrationBuilder.DropColumn(
                name: "StrapiId",
                table: "CardPrintings");

            migrationBuilder.RenameColumn(
                name: "SetId",
                table: "SyncLogs",
                newName: "SwuSetId");

            migrationBuilder.RenameIndex(
                name: "IX_SyncLogs_SetId_StartedAt",
                table: "SyncLogs",
                newName: "IX_SyncLogs_SwuSetId_StartedAt");

            migrationBuilder.RenameColumn(
                name: "SetId",
                table: "SwuCards",
                newName: "SwuSetId");

            migrationBuilder.RenameIndex(
                name: "IX_SwuCards_SetId_Title_Subtitle",
                table: "SwuCards",
                newName: "IX_SwuCards_SwuSetId_Title_Subtitle");

            migrationBuilder.RenameColumn(
                name: "SetId",
                table: "SwuCardPrintings",
                newName: "SwuSetId");

            migrationBuilder.RenameIndex(
                name: "IX_SwuCardPrintings_SwuCardId_SetId_Number_Style",
                table: "SwuCardPrintings",
                newName: "IX_SwuCardPrintings_SwuCardId_SwuSetId_Number_Style");

            migrationBuilder.RenameIndex(
                name: "IX_SwuCardPrintings_SetId_Number",
                table: "SwuCardPrintings",
                newName: "IX_SwuCardPrintings_SwuSetId_Number");

            migrationBuilder.AddForeignKey(
                name: "FK_SwuCardPrintings_SwuSets_SwuSetId",
                table: "SwuCardPrintings",
                column: "SwuSetId",
                principalTable: "SwuSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SwuCards_SwuSets_SwuSetId",
                table: "SwuCards",
                column: "SwuSetId",
                principalTable: "SwuSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SyncLogs_SwuSets_SwuSetId",
                table: "SyncLogs",
                column: "SwuSetId",
                principalTable: "SwuSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
