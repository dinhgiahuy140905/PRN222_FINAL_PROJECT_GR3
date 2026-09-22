using System.ComponentModel.DataAnnotations;
namespace SalesManagement.Web.Models;
public class Customer
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(20), Phone] public string? Phone { get; set; }
    [StringLength(200), EmailAddress] public string? Email { get; set; }
    [StringLength(500)] public string? Note { get; set; }
    public Store Store { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
