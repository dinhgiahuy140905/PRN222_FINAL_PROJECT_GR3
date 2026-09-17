namespace SalesManagement.Web.Models
{
    public class StoreMember
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int StoreId { get; set; }

        public StoreRole Role { get; set; }

        public User User { get; set; } = null!;

        public Store Store { get; set; } = null!;
    }

    public enum StoreRole
    {
        Staff = 0,
        Owner = 1
    }
}
