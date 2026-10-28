using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class BackfillSwuCardLastSyncedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Cards"
                SET "LastSyncedAt" = (
                    SELECT MAX(printing."LastSyncedAt")
                    FROM "CardPrintings" AS printing
                    WHERE printing."CardId" = "Cards"."Id"
                )
                WHERE "LastSyncedAt" IS NULL
                    AND "GameNorm" = 'star wars unlimited'
                    AND EXISTS (
                        SELECT 1
                        FROM "CardPrintings" AS printing
                        WHERE printing."CardId" = "Cards"."Id"
                            AND printing."LastSyncedAt" IS NOT NULL
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
