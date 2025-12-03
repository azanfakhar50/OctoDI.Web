using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctoDI.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGlobalSaleRateModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GlobalSaleRates_Products_ProductId",
                table: "GlobalSaleRates");

            migrationBuilder.DropIndex(
                name: "IX_GlobalSaleRates_ProductId",
                table: "GlobalSaleRates");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "GlobalSaleRates");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "GlobalSaleRates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "HSCode",
                table: "GlobalSaleRates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceCategoryId",
                table: "GlobalSaleRates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnitId",
                table: "GlobalSaleRates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_GlobalSaleRates_ServiceCategoryId",
                table: "GlobalSaleRates",
                column: "ServiceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalSaleRates_UnitId",
                table: "GlobalSaleRates",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_GlobalSaleRates_ServiceCategories_ServiceCategoryId",
                table: "GlobalSaleRates",
                column: "ServiceCategoryId",
                principalTable: "ServiceCategories",
                principalColumn: "ServiceCategoryId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GlobalSaleRates_Units_UnitId",
                table: "GlobalSaleRates",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "UnitId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GlobalSaleRates_ServiceCategories_ServiceCategoryId",
                table: "GlobalSaleRates");

            migrationBuilder.DropForeignKey(
                name: "FK_GlobalSaleRates_Units_UnitId",
                table: "GlobalSaleRates");

            migrationBuilder.DropIndex(
                name: "IX_GlobalSaleRates_ServiceCategoryId",
                table: "GlobalSaleRates");

            migrationBuilder.DropIndex(
                name: "IX_GlobalSaleRates_UnitId",
                table: "GlobalSaleRates");

            migrationBuilder.DropColumn(
                name: "HSCode",
                table: "GlobalSaleRates");

            migrationBuilder.DropColumn(
                name: "ServiceCategoryId",
                table: "GlobalSaleRates");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "GlobalSaleRates");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "GlobalSaleRates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "GlobalSaleRates",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlobalSaleRates_ProductId",
                table: "GlobalSaleRates",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_GlobalSaleRates_Products_ProductId",
                table: "GlobalSaleRates",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "ProductId");
        }
    }
}
