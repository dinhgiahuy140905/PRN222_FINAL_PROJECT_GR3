namespace SalesManagement.Web.Models
{
    public class Store
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string? Phone { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<StoreMember> StoreMembers { get; set; } = new List<StoreMember>();

        public ICollection<Category> Categories { get; set; } = new List<Category>();

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
