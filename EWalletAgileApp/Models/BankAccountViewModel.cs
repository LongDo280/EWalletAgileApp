using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

// US010 - Liên kết tài khoản ngân hàng
public class BankAccountViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn ngân hàng")]
    [Display(Name = "Ngân hàng")]
    public string BankName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số tài khoản")]
    [Display(Name = "Số tài khoản")]
    public string AccountNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên chủ tài khoản")]
    [Display(Name = "Tên chủ tài khoản")]
    public string AccountHolderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã PIN để xác nhận")]
    [DataType(DataType.Password)]
    [Display(Name = "Mã PIN")]
    public string PinCode { get; set; } = string.Empty;
}
