using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class User
{
    public int UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [Phone]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã PIN")]
    [StringLength(6, MinimumLength = 4, ErrorMessage = "Mã PIN từ 4 đến 6 số")]
    public string PinCode { get; set; } = string.Empty;

    public decimal Balance { get; set; } = 0;

    [Display(Name = "Hạn mức mỗi giao dịch")]
    public decimal TransactionLimit { get; set; } = 20000000;

    [Display(Name = "Hạn mức giao dịch mỗi ngày")]
    public decimal DailyLimit { get; set; } = 50000000;

    [Display(Name = "Số dư tối đa")]
    public decimal MaximumBalance { get; set; } = 100000000;
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Transaction> SentTransactions { get; set; } = new List<Transaction>();
    public ICollection<Transaction> ReceivedTransactions { get; set; } = new List<Transaction>();
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
    public string PasswordSalt { get; set; } = string.Empty;
    public int FailedLoginCount { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    public string WalletStatus { get; set; } = "Active";
    public string? WalletStatusNote { get; set; }
    public DateTime? WalletStatusUpdatedAt { get; set; }
    public string Role { get; set; } = "User";
    public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
}
