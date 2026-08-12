using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại")]
    public string Account { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
