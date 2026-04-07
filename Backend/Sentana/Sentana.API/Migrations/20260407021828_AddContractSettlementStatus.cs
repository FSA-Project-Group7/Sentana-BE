using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentana.API.Migrations
{
    /// <inheritdoc />
    public partial class AddContractSettlementStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SettledAt",
                table: "Contract",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SettlementStatus",
                table: "Contract",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SettledAt",
                table: "Contract");

            migrationBuilder.DropColumn(
                name: "SettlementStatus",
                table: "Contract");
        }
    }
}
