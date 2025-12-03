using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctoDI.Web.Migrations
{
    /// <inheritdoc />
    public partial class MakeSubscriptionIdNullableInProductTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceCategories_Subscriptions_SubscriptionId",
                table: "ServiceCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_Subscriptions_SubscriptionId",
                table: "Units");

            migrationBuilder.RenameColumn(
                name: "SalesTaxRate",
                table: "InvoiceItems",
                newName: "SalesTaxApplicable");

            migrationBuilder.AlterColumn<int>(
                name: "SubscriptionId",
                table: "Units",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "SubscriptionId",
                table: "ServiceCategories",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceCategories_Subscriptions_SubscriptionId",
                table: "ServiceCategories",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "SubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Units_Subscriptions_SubscriptionId",
                table: "Units",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "SubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceCategories_Subscriptions_SubscriptionId",
                table: "ServiceCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Units_Subscriptions_SubscriptionId",
                table: "Units");

            migrationBuilder.RenameColumn(
                name: "SalesTaxApplicable",
                table: "InvoiceItems",
                newName: "SalesTaxRate");

            migrationBuilder.AlterColumn<int>(
                name: "SubscriptionId",
                table: "Units",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SubscriptionId",
                table: "ServiceCategories",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

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
    }
}
