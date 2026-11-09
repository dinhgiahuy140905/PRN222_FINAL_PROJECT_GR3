using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class GuardNonnegativeInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItems_Quantity_Nonnegative",
                table: "InventoryItems",
                sql: "[Quantity] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItems_Quantity_Nonnegative",
                table: "InventoryItems");
        }
    }
}
