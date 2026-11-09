namespace SalesManagement.Web.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public int? CustomerId { get; set; }
        public int? CreatedByUserId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = "VND";
        public string CustomerName { get; set; } = "Khách lẻ";
        public string? CustomerPhone { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Confirmed;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public Store Store { get; set; } = null!;
        public Customer? Customer { get; set; }
        public User? CreatedByUser { get; set; }
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
        public ICollection<InventoryTransaction> InventoryTransactions { get; set; }
            = new List<InventoryTransaction>();
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal LineTotal { get; set; }

        public Order Order { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }

    public enum OrderStatus
    {
        Confirmed = 0,
        Completed = 1,
        Cancelled = 2
    }

    public enum PaymentStatus
    {
        Unpaid = 0,
        Paid = 1,
        Refunded = 2
    }

    public enum PaymentMethod
    {
        Cash = 0,
        BankTransfer = 1,
        Card = 2
    }
}
