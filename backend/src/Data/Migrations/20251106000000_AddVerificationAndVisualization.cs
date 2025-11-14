using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationAndVisualization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "verification_status",
                table: "messages",
                type: "varchar(20)",
                nullable: false,
                defaultValue: "Unsigned");

            migrationBuilder.AddColumn<string>(
                name: "visualization_url",
                table: "messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "verification_status",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "visualization_url",
                table: "messages");
        }
    }
}

