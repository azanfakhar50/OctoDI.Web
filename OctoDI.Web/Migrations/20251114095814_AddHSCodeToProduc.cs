using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctoDI.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddHSCodeToProduc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductCategoryType",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "ProductCategoryType",
                table: "ServiceCategories");

            migrationBuilder.DropColumn(
                name: "ProductCategoryType",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "HSCode",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_SubscriptionId",
                table: "Units",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategories_SubscriptionId",
                table: "ServiceCategories",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SubscriptionId",
                table: "Products",
                column: "SubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Subscriptions_SubscriptionId",
                table: "Products",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "SubscriptionId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceCategories_Subscriptions_SubscriptionId",
                table: "ServiceCategories",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "SubscriptionId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Units_Subscriptions_SubscriptionId",
                table: "Units",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "SubscriptionId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Subscriptions_SubscriptionId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceCategories_Subscriptions_SubscriptionId",
                table: "ServiceCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_Subscriptions_SubscriptionId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Units_SubscriptionId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_ServiceCategories_SubscriptionId",
                table: "ServiceCategories");

            migrationBuilder.DropIndex(
                name: "IX_Products_SubscriptionId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "HSCode",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "ProductCategoryType",
                table: "Units",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductCategoryType",
                table: "ServiceCategories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductCategoryType",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
