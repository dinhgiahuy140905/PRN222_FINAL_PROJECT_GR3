using System.ComponentModel.DataAnnotations;

namespace SalesManagement.Web.Models
{
    public class StoreSelectionViewModel
    {
        public int? ActiveStoreId { get; set; }

        public bool CanCreateStore { get; set; }

        public IReadOnlyList<StoreListItemViewModel> Stores { get; set; }
            = Array.Empty<StoreListItemViewModel>();
    }

    public class StaffManagementViewModel
    {
        public string StoreName { get; set; } = string.Empty;

        public IReadOnlyList<StoreMemberListItemViewModel> Members { get; set; }
            = Array.Empty<StoreMemberListItemViewModel>();

        public StaffInviteViewModel Invite { get; set; } = new();
    }

    public class StoreMemberListItemViewModel
    {
        public int MembershipId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public StoreRole Role { get; set; }
    }

    public class StaffInviteViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email nhân viên.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(255)]
        [Display(Name = "Email nhân viên")]
        public string Email { get; set; } = string.Empty;
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
