using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentana.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedImageToMR : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FixedImageUrl",
                table: "MaintenanceRequest",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FixedImageUrl",
                table: "MaintenanceRequest");
        }
    }
}
