namespace SalesManagement.Web.Models
{
    public class Category
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Store Store { get; set; } = null!;
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
