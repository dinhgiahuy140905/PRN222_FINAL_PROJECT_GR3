using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyCodeToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both branch histories retain their IDs, but the column is added only once.
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Orders', N'CurrencyCode') IS NULL
                    ALTER TABLE [Orders] ADD [CurrencyCode] nvarchar(max) NOT NULL DEFAULT N'';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // AddOrderCurrencySnapshot now owns the column earlier in the merged history.
        }
    }
}
