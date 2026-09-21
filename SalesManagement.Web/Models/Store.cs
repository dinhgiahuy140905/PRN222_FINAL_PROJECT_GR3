namespace SalesManagement.Web.Models
{
    public class Store
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string? Phone { get; set; }

        public string? LogoPath { get; set; }

        public string CurrencyCode { get; set; } = "VND";

        public string? InvoiceFooter { get; set; }

        public string InvoiceThankYou { get; set; } = "Cảm ơn quý khách đã mua hàng!";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<StoreMember> StoreMembers { get; set; } = new List<StoreMember>();

        public ICollection<Category> Categories { get; set; } = new List<Category>();

        public ICollection<Product> Products { get; set; } = new List<Product>();

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
