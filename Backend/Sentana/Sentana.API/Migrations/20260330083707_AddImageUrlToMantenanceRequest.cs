using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentana.API.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUrlToMantenanceRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "MaintenanceRequest",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "MaintenanceRequest");
        }
    }
}
