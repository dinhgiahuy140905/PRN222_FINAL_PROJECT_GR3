namespace SalesManagement.Web.Models
{
    public class User
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string? AvatarPath { get; set; }

        public string PasswordHash { get; set; } = string.Empty;

        public int? DefaultStoreId { get; set; }

        public DateTime? PasswordChangedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<StoreMember> StoreMembers { get; set; } = new List<StoreMember>();

        public ICollection<InventoryTransaction> InventoryTransactions { get; set; }
            = new List<InventoryTransaction>();

        public ICollection<Order> CreatedOrders { get; set; } = new List<Order>();
    }
}
