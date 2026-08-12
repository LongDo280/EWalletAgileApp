using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

// US009 - Quản lý hạn mức ví (Admin chỉnh sửa hạn mức cho một user)
public class WalletLimitViewModel
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;

    [Range(1000, double.MaxValue, ErrorMessage = "Hạn mức mỗi giao dịch phải lớn hơn 0")]
    [Display(Name = "Hạn mức mỗi giao dịch")]
    public decimal TransactionLimit { get; set; }

    [Range(1000, double.MaxValue, ErrorMessage = "Hạn mức mỗi ngày phải lớn hơn 0")]
    [Display(Name = "Hạn mức giao dịch mỗi ngày")]
    public decimal DailyLimit { get; set; }

    [Range(1000, double.MaxValue, ErrorMessage = "Số dư tối đa phải lớn hơn 0")]
    [Display(Name = "Số dư tối đa")]
    public decimal MaximumBalance { get; set; }
}
