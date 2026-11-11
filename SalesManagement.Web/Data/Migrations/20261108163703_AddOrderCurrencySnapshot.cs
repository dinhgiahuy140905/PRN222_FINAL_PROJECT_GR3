using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCurrencySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Main may already have added this column under a different migration ID.
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Orders', N'CurrencyCode') IS NULL
                    ALTER TABLE [Orders] ADD [CurrencyCode] nvarchar(10) NULL;
                """);

            // Earlier currency changes cannot be reconstructed without historical data.
            migrationBuilder.Sql("""
                UPDATE o SET o.CurrencyCode = s.CurrencyCode
                FROM Orders AS o INNER JOIN Stores AS s ON s.Id = o.StoreId
                WHERE o.CurrencyCode IS NULL OR LTRIM(RTRIM(o.CurrencyCode)) = N'';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Orders");
        }
    }
}
