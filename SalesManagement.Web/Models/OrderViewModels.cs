using System.ComponentModel.DataAnnotations;

namespace SalesManagement.Web.Models
{
    public class CreateOrderViewModel
    {
        public string CurrencyCode { get; set; } = "VND";
        public int? CustomerId { get; set; }
        public bool SaveCustomer { get; set; }
        public IReadOnlyList<Customer> Customers { get; set; } = Array.Empty<Customer>();

        [StringLength(100)]
        [Display(Name = "Tên khách hàng")]
        public string? CustomerName { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(20)]
        [Display(Name = "Số điện thoại")]
        public string? CustomerPhone { get; set; }

        [Range(0, 999999999999)]
        [Display(Name = "Giảm giá")]
        [MoneyPrecision]
        public decimal Discount { get; set; }

        [Display(Name = "Phương thức thanh toán")]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string? Note { get; set; }

        public List<CreateOrderItemViewModel> Items { get; set; } = new()
        {
            new CreateOrderItemViewModel()
        };

        public IReadOnlyList<OrderProductOptionViewModel> Products { get; set; }
            = Array.Empty<OrderProductOptionViewModel>();
    }

    public class CreateOrderItemViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn sản phẩm.")]
        public int ProductId { get; set; }

        [Range(1, 100000, ErrorMessage = "Số lượng phải lớn hơn 0.")]
        public int Quantity { get; set; } = 1;
    }

    public class OrderProductOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal SalePrice { get; set; }
        public int Stock { get; set; }
    }

    public class OrderIndexViewModel
    {
        public PaymentStatus? PaymentStatus { get; set; }
        public string? Search { get; set; }
        public OrderStatus? Status { get; set; }
        public IReadOnlyList<Order> Orders { get; set; } = Array.Empty<Order>();
    }
}
