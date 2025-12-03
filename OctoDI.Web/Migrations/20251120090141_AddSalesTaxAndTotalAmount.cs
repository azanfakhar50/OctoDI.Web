using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctoDI.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesTaxAndTotalAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTwoFactorEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesTaxAmount",
                table: "InvoiceItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "InvoiceItems",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTwoFactorEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SalesTaxAmount",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "InvoiceItems");
        }
    }
}
