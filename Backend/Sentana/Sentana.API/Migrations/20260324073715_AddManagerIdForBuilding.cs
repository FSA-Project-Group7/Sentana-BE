using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentana.API.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerIdForBuilding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ManagerId",
                table: "Building",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Building_ManagerId",
                table: "Building",
                column: "ManagerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Building_Account_ManagerId",
                table: "Building",
                column: "ManagerId",
                principalTable: "Account",
                principalColumn: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Building_Account_ManagerId",
                table: "Building");

            migrationBuilder.DropIndex(
                name: "IX_Building_ManagerId",
                table: "Building");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Building");
        }
    }
}
