using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarPath",
                table: "Users",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultStoreId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Stores",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceFooter",
                table: "Stores",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceThankYou",
                table: "Stores",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Cảm ơn quý khách đã mua hàng!");

            migrationBuilder.AddColumn<string>(
                name: "LogoPath",
                table: "Stores",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarPath",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DefaultStoreId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "InvoiceFooter",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "InvoiceThankYou",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "LogoPath",
                table: "Stores");
        }
    }
}
