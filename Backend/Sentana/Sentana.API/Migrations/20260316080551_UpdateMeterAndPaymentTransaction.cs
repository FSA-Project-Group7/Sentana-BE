using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentana.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMeterAndPaymentTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Xóa cột Code ở 2 bảng Meter
            migrationBuilder.DropColumn(
                name: "Code",
                table: "WaterMeter");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ElectricMeter");

            // 2. Thêm cột lưu vết vào PaymentTransaction
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PaymentTransaction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedBy",
                table: "PaymentTransaction",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
