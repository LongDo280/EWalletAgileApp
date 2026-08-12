using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại")]
    public string Account { get; set; } = string.Empty;
}