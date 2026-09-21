using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SalesManagement.Web.Models
{
    public class ProductIndexViewModel
    {
        public IReadOnlyList<Product> Products { get; set; } = Array.Empty<Product>();
        public IReadOnlyList<SelectListItem> Categories { get; set; } = Array.Empty<SelectListItem>();
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public string Status { get; set; } = "all";
        public int TotalProducts { get; set; }
        public int LowStockProducts { get; set; }
        public int OutOfStockProducts { get; set; }
        public int InactiveProducts { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int FilteredCount { get; set; }
        public int PageCount => Math.Max(1, (int)Math.Ceiling(FilteredCount / (double)PageSize));
        public int FirstItem => FilteredCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
        public int LastItem => Math.Min(Page * PageSize, FilteredCount);
    }

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
