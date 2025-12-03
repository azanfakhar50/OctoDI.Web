using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctoDI.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixTwoFactorOtpUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TwoFactorOtps_Users_UserId1",
                table: "TwoFactorOtps");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TwoFactorOtps",
                table: "TwoFactorOtps");

            migrationBuilder.DropIndex(
                name: "IX_TwoFactorOtps_UserId1",
                table: "TwoFactorOtps");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "TwoFactorOtps");

            migrationBuilder.RenameTable(
                name: "TwoFactorOtps",
                newName: "TwoFactorOtp");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "TwoFactorOtp",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TwoFactorOtp",
                table: "TwoFactorOtp",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorOtp_UserId",
                table: "TwoFactorOtp",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TwoFactorOtp_Users_UserId",
                table: "TwoFactorOtp",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TwoFactorOtp_Users_UserId",
                table: "TwoFactorOtp");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TwoFactorOtp",
                table: "TwoFactorOtp");

            migrationBuilder.DropIndex(
                name: "IX_TwoFactorOtp_UserId",
                table: "TwoFactorOtp");

            migrationBuilder.RenameTable(
                name: "TwoFactorOtp",
                newName: "TwoFactorOtps");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "TwoFactorOtps",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "UserId1",
                table: "TwoFactorOtps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TwoFactorOtps",
                table: "TwoFactorOtps",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorOtps_UserId1",
                table: "TwoFactorOtps",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_TwoFactorOtps_Users_UserId1",
                table: "TwoFactorOtps",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
