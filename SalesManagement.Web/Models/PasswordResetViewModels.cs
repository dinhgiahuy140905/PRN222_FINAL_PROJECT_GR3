using System.ComponentModel.DataAnnotations;

namespace SalesManagement.Web.Models;

public sealed class ForgotPasswordViewModel
{
    [Required, EmailAddress, StringLength(255)]
    [Display(Name = "Email đăng ký")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required, StringLength(4096)]
    public string Token { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password)), DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
