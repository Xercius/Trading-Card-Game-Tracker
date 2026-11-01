using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddSwccgForceIcons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackDarkSideIcons",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BackLightSideIcons",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DarkSideIcons",
                table: "Cards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LightSideIcons",
                table: "Cards",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackDarkSideIcons",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "BackLightSideIcons",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "DarkSideIcons",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "LightSideIcons",
                table: "Cards");
        }
    }
}
