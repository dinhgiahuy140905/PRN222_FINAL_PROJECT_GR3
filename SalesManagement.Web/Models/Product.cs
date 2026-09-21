using System.ComponentModel.DataAnnotations;

namespace SalesManagement.Web.Models
{
    public class Product
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public int? CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Unit { get; set; } = "Sản phẩm";
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public Store Store { get; set; } = null!;
        public Category? Category { get; set; }
        public InventoryItem Inventory { get; set; } = null!;
        public ICollection<InventoryTransaction> InventoryTransactions { get; set; }
            = new List<InventoryTransaction>();
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    public class InventoryItem
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public int ReorderLevel { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Store Store { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }

    public class InventoryTransaction
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public int ProductId { get; set; }
        public int? UserId { get; set; }
        public int? OrderId { get; set; }
        public InventoryTransactionType Type { get; set; }
        public int QuantityChange { get; set; }
        public int QuantityAfter { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Store Store { get; set; } = null!;
        public Product Product { get; set; } = null!;
        public User? User { get; set; }
        public Order? Order { get; set; }
    }

    public enum InventoryTransactionType
    {
        OpeningStock = 0,
        Purchase = 1,
        Sale = 2,
        Adjustment = 3,
        Return = 4
    }
}
