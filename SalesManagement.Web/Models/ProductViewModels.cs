using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SalesManagement.Web.Models
{
    public class ProductFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm.")]
        [StringLength(150)]
        [Display(Name = "Tên sản phẩm")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập SKU.")]
        [StringLength(50)]
        [Display(Name = "SKU")]
        public string Sku { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Mã vạch")]
        public string? Barcode { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "Đơn vị tính")]
        public string Unit { get; set; } = "Sản phẩm";

        [Range(0, 999999999999)]
        [Display(Name = "Giá nhập")]
        public decimal CostPrice { get; set; }

        [Range(0, 999999999999)]
        [Display(Name = "Giá bán")]
        public decimal SalePrice { get; set; }

        [Display(Name = "Danh mục")]
        public int? CategoryId { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Tồn kho ban đầu")]
        public int OpeningStock { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Ngưỡng cảnh báo")]
        public int ReorderLevel { get; set; }

        public IReadOnlyList<SelectListItem> Categories { get; set; }
            = Array.Empty<SelectListItem>();
    }

    public class StockAdjustmentViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int CurrentQuantity { get; set; }

        [Required]
        [Range(-1000000, 1000000)]
        [Display(Name = "Số lượng thay đổi")]
        public int QuantityChange { get; set; }

        [StringLength(255)]
        [Display(Name = "Lý do điều chỉnh")]
        public string? Note { get; set; }
    }

    public class CategoryFormViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
        [StringLength(100)]
        [Display(Name = "Tên danh mục")]
        public string Name { get; set; } = string.Empty;
    }
}
