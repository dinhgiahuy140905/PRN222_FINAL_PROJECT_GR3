using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SalesManagement.Web.Models
{
    public class SettingsPageViewModel
    {
        public string ActiveSection { get; set; } = "profile";
        public bool IsOwner { get; set; }
        public ProfileSettingsViewModel Profile { get; set; } = new();
        public StoreSettingsViewModel? Store { get; set; }
        public SecuritySettingsViewModel Security { get; set; } = new();
        public int? DefaultStoreId { get; set; }
        public IReadOnlyList<SelectListItem> AvailableStores { get; set; }
            = Array.Empty<SelectListItem>();
    }

    public class ProfileSettingsViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(100)]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(20)]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        public string? AvatarPath { get; set; }

        [Display(Name = "Ảnh đại diện")]
        public IFormFile? Avatar { get; set; }
    }

    public class StoreSettingsViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên cửa hàng.")]
        [StringLength(150)]
        [Display(Name = "Tên cửa hàng")]
        public string Name { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(20)]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [StringLength(255)]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Required]
        [StringLength(10)]
        [RegularExpression("^(VND|USD|EUR)$", ErrorMessage = "Đơn vị tiền tệ không được hỗ trợ.")]
        [Display(Name = "Đơn vị tiền tệ")]
        public string CurrencyCode { get; set; } = "VND";

        [StringLength(300)]
        [Display(Name = "Nội dung cuối hóa đơn")]
        public string? InvoiceFooter { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Lời cảm ơn trên hóa đơn")]
        public string InvoiceThankYou { get; set; } = "Cảm ơn quý khách đã mua hàng!";

        public string? LogoPath { get; set; }

        [Display(Name = "Logo cửa hàng")]
        public IFormFile? Logo { get; set; }
    }

    public class SecuritySettingsViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu mới")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public DateTime? PasswordChangedAt { get; set; }
    }
}
