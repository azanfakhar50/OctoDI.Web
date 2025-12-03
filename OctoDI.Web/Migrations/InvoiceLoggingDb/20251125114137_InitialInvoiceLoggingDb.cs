using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctoDI.Web.Migrations.InvoiceLoggingDb
{
    /// <inheritdoc />
    public partial class InitialInvoiceLoggingDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceLogs",
                columns: table => new
                {
                    InvoiceLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubscriptionId = table.Column<int>(type: "int", nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLogs", x => x.InvoiceLogId);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceRequestLogs",
                columns: table => new
                {
                    InvoiceRequestLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceLogId = table.Column<int>(type: "int", nullable: false),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRequestLogs", x => x.InvoiceRequestLogId);
                    table.ForeignKey(
                        name: "FK_InvoiceRequestLogs_InvoiceLogs_InvoiceLogId",
                        column: x => x.InvoiceLogId,
                        principalTable: "InvoiceLogs",
                        principalColumn: "InvoiceLogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceResponseLogs",
                columns: table => new
                {
                    InvoiceResponseLogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceLogId = table.Column<int>(type: "int", nullable: false),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceResponseLogs", x => x.InvoiceResponseLogId);
                    table.ForeignKey(
                        name: "FK_InvoiceResponseLogs_InvoiceLogs_InvoiceLogId",
                        column: x => x.InvoiceLogId,
                        principalTable: "InvoiceLogs",
                        principalColumn: "InvoiceLogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRequestLogs_InvoiceLogId",
                table: "InvoiceRequestLogs",
                column: "InvoiceLogId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceResponseLogs_InvoiceLogId",
                table: "InvoiceResponseLogs",
                column: "InvoiceLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceRequestLogs");

            migrationBuilder.DropTable(
                name: "InvoiceResponseLogs");

            migrationBuilder.DropTable(
                name: "InvoiceLogs");
        }
    }
}
