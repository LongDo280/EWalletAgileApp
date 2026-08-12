using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class TransferViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại người nhận")]
    public string ReceiverPhone { get; set; } = string.Empty;

    [Range(1000, double.MaxValue, ErrorMessage = "Số tiền phải từ 1.000đ trở lên")]
    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã PIN")]
    public string PinCode { get; set; } = string.Empty;
}
