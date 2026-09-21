using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [u]
                SET [AccountType] = CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM [StoreMembers] AS [sm]
                        WHERE [sm].[UserId] = [u].[Id] AND [sm].[Role] = N'Staff')
                    AND NOT EXISTS (
                        SELECT 1
                        FROM [StoreMembers] AS [om]
                        WHERE [om].[UserId] = [u].[Id] AND [om].[Role] = N'Owner')
                    THEN N'Staff'
                    ELSE N'Owner'
                END
                FROM [Users] AS [u];
                """);

            migrationBuilder.AlterColumn<string>(
                name: "AccountType",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Users");
        }
    }
}
