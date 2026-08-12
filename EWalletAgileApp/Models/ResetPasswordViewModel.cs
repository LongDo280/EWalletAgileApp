using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class ResetPasswordViewModel
{
    [Required]
    public string Account { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm 6 số")]
    [Display(Name = "Mã OTP")]
    public string Otp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới")]
    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu mới")]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;
}