using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentana.API.Migrations
{
    /// <inheritdoc />
    public partial class AddContractVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentVersionId",
                table: "Contract",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractVersions",
                columns: table => new
                {
                    VersionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: false),
                    File = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractVersions", x => x.VersionId);
                    table.ForeignKey(
                        name: "FK_ContractVersions_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contract_CurrentVersionId",
                table: "Contract",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractVersions_ContractId",
                table: "ContractVersions",
                column: "ContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contract_ContractVersions_CurrentVersionId",
                table: "Contract",
                column: "CurrentVersionId",
                principalTable: "ContractVersions",
                principalColumn: "VersionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contract_ContractVersions_CurrentVersionId",
                table: "Contract");

            migrationBuilder.DropTable(
                name: "ContractVersions");

            migrationBuilder.DropIndex(
                name: "IX_Contract_CurrentVersionId",
                table: "Contract");

            migrationBuilder.DropColumn(
                name: "CurrentVersionId",
                table: "Contract");
        }
    }
}
