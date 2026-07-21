using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddSwuCardPrintingSyncLogModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SwuCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StrapiId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardUid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Subtitle = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CardType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Arena = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Cost = table.Column<int>(type: "INTEGER", nullable: true),
                    Power = table.Column<int>(type: "INTEGER", nullable: true),
                    Health = table.Column<int>(type: "INTEGER", nullable: true),
                    Artist = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Aspects = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Traits = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Keywords = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    SwuSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseCardId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApiCreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ApiUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwuCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SwuCards_SwuCards_BaseCardId",
                        column: x => x.BaseCardId,
                        principalTable: "SwuCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SwuCards_SwuSets_SwuSetId",
                        column: x => x.SwuSetId,
                        principalTable: "SwuSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SyncLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SwuSetId = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsIncremental = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedSince = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CardsReturned = table.Column<int>(type: "INTEGER", nullable: false),
                    CardsUpserted = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncLogs_SwuSets_SwuSetId",
                        column: x => x.SwuSetId,
                        principalTable: "SwuSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SwuCardPrintings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StrapiId = table.Column<int>(type: "INTEGER", nullable: false),
                    SwuCardId = table.Column<int>(type: "INTEGER", nullable: false),
                    SwuSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Style = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    BackImageUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ApiCreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ApiUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwuCardPrintings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SwuCardPrintings_SwuCards_SwuCardId",
                        column: x => x.SwuCardId,
                        principalTable: "SwuCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SwuCardPrintings_SwuSets_SwuSetId",
                        column: x => x.SwuSetId,
                        principalTable: "SwuSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SwuCardPrintings_StrapiId",
                table: "SwuCardPrintings",
                column: "StrapiId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwuCardPrintings_SwuCardId_SwuSetId_Number_Style",
                table: "SwuCardPrintings",
                columns: new[] { "SwuCardId", "SwuSetId", "Number", "Style" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwuCardPrintings_SwuSetId_Number",
                table: "SwuCardPrintings",
                columns: new[] { "SwuSetId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_BaseCardId",
                table: "SwuCards",
                column: "BaseCardId");

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_CardUid",
                table: "SwuCards",
                column: "CardUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_StrapiId",
                table: "SwuCards",
                column: "StrapiId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwuCards_SwuSetId_Title_Subtitle",
                table: "SwuCards",
                columns: new[] { "SwuSetId", "Title", "Subtitle" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_SwuSetId_StartedAt",
                table: "SyncLogs",
                columns: new[] { "SwuSetId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SwuCardPrintings");

            migrationBuilder.DropTable(
                name: "SyncLogs");

            migrationBuilder.DropTable(
                name: "SwuCards");
        }
    }
}
