using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

// US010 - Liên kết tài khoản ngân hàng / US011 - Hủy liên kết ngân hàng
public class BankAccount
{
    public int BankAccountId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngân hàng")]
    [Display(Name = "Ngân hàng")]
    public string BankName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số tài khoản")]
    [Display(Name = "Số tài khoản")]
    public string AccountNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên chủ tài khoản")]
    [Display(Name = "Tên chủ tài khoản")]
    public string AccountHolderName { get; set; } = string.Empty;

    // Linked: đang liên kết, Unlinked: đã hủy liên kết (giữ lại lịch sử thay vì xóa cứng)
    public string Status { get; set; } = "Linked";

    public DateTime LinkedAt { get; set; } = DateTime.Now;
    public DateTime? UnlinkedAt { get; set; }
}