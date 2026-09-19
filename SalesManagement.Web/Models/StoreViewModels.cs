using System.ComponentModel.DataAnnotations;

namespace SalesManagement.Web.Models
{
    public class StoreSelectionViewModel
    {
        public int? ActiveStoreId { get; set; }

        public IReadOnlyList<StoreListItemViewModel> Stores { get; set; }
            = Array.Empty<StoreListItemViewModel>();
    }

    public class StoreListItemViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string Role { get; set; } = string.Empty;
    }

    public class CreateStoreViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên cửa hàng.")]
        [StringLength(150, ErrorMessage = "Tên cửa hàng không được vượt quá 150 ký tự.")]
        [Display(Name = "Tên cửa hàng")]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(20)]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }
    }
}
