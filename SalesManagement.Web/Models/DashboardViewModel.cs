namespace SalesManagement.Web.Models
{
    public class DashboardViewModel
    {
        public string StoreName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int TotalStock { get; set; }
        public int LowStockCount { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
        public int CustomerCount { get; set; }
        public IReadOnlyList<InventoryActivityViewModel> RecentInventoryActivities { get; set; }
            = Array.Empty<InventoryActivityViewModel>();
        public IReadOnlyList<DashboardOrderViewModel> RecentOrders { get; set; }
            = Array.Empty<DashboardOrderViewModel>();

        public bool IsNewStore => ProductCount == 0 && OrderCount == 0 && CustomerCount == 0;
    }

    public class DashboardOrderViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InventoryActivityViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int QuantityChange { get; set; }
        public int QuantityAfter { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
