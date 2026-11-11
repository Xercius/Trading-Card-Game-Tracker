using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddCardStageColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "Cards",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Cards
                SET Stage = (
                    SELECT st.Name
                    FROM CardSubTypes cst
                    JOIN SubTypes st ON cst.SubTypeId = st.Id
                    WHERE cst.CardId = Cards.Id
                      AND (st.Name LIKE '%Stage%' OR st.Name = 'Basic' OR st.Name = 'Restored' OR st.Name LIKE 'V%')
                    LIMIT 1
                )
                WHERE Stage IS NULL;
            ");

            migrationBuilder.Sql("UPDATE Cards SET Stage = 'Stage 1' WHERE Stage = 'Stage1';");
            migrationBuilder.Sql("UPDATE Cards SET Stage = 'Stage 2' WHERE Stage = 'Stage2';");

            migrationBuilder.Sql(@"
                UPDATE Cards
                SET Text = Rules, Rules = NULL
                WHERE (Text IS NULL OR Text = '') AND Rules IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Cards");
        }
    }
}
