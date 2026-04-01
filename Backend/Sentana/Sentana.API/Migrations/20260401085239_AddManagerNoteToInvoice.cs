using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentana.API.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerNoteToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagerNote",
                table: "Invoice",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "Contract",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagerNote",
                table: "Invoice");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "Contract");
        }
    }
}
