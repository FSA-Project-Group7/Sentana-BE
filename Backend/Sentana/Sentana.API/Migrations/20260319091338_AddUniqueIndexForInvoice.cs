using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentana.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexForInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropIndex(
            //    name: "IX_Invoice_ApartmentId",
            //    table: "Invoice");

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_ApartmentId_BillingMonth_BillingYear",
                table: "Invoice",
                columns: new[] { "ApartmentId", "BillingMonth", "BillingYear" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoice_ApartmentId_BillingMonth_BillingYear",
                table: "Invoice");

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_ApartmentId",
                table: "Invoice",
                column: "ApartmentId");
        }
    }
}
