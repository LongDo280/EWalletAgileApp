using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class PayBillViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn loại hóa đơn")]
    public string BillType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã khách hàng")]
    public string CustomerCode { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public decimal Amount { get; set; }

    public string? BillingPeriod { get; set; }

    public int? BillId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã PIN")]
    public string PinCode { get; set; } = string.Empty;
}