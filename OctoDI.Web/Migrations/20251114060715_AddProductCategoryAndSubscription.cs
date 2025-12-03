using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OctoDI.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCategoryAndSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ServiceCategories",
                keyColumn: "ServiceCategoryId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ServiceCategories",
                keyColumn: "ServiceCategoryId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ServiceCategories",
                keyColumn: "ServiceCategoryId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "UnitId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "UnitId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "UnitId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Units",
                keyColumn: "UnitId",
                keyValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "ProductCategoryType",
                table: "Units",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionId",
                table: "Units",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ServiceCategories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "ServiceCategories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ProductCategoryType",
                table: "ServiceCategories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionId",
                table: "ServiceCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProductCategoryType",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionId",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductCategoryType",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ServiceCategories");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "ServiceCategories");

            migrationBuilder.DropColumn(
                name: "ProductCategoryType",
                table: "ServiceCategories");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "ServiceCategories");

            migrationBuilder.DropColumn(
                name: "ProductCategoryType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Products");

            migrationBuilder.InsertData(
                table: "ServiceCategories",
                columns: new[] { "ServiceCategoryId", "Name", "TaxRate" },
                values: new object[,]
                {
                    { 1, "Consulting", 17m },
                    { 2, "Maintenance", 5m },
                    { 3, "Support", 10m }
                });

            migrationBuilder.InsertData(
                table: "Units",
                columns: new[] { "UnitId", "Name" },
                values: new object[,]
                {
                    { 1, "Hour" },
                    { 2, "Session" },
                    { 3, "Day" },
                    { 4, "Per Service" }
                });
        }
    }
}
