using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileBranchMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Main's original currency migration populated older orders with an empty string.
            // Preserve every nonempty snapshot, even if the store's currency has since changed.
            migrationBuilder.Sql("""
                UPDATE o SET o.CurrencyCode = s.CurrencyCode
                FROM Orders AS o INNER JOIN Stores AS s ON s.Id = o.StoreId
                WHERE o.CurrencyCode IS NULL OR LTRIM(RTRIM(o.CurrencyCode)) = N'';

                UPDATE [Users] SET [SecurityStamp] = CONVERT(nvarchar(36), NEWID())
                WHERE [SecurityStamp] = N'';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // SessionVersion remains in historical migrations only; runtime uses SecurityStamp.
            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);
        }
    }
}
