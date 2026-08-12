using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class Bill
{
    public int BillId { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    [Required]
    public string BillType { get; set; } = string.Empty;

    [Required]
    public string CustomerCode { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    [Range(1000, double.MaxValue)]
    public decimal Amount { get; set; }

    public string BillingPeriod { get; set; } = string.Empty;

    public string Status { get; set; } = "Unpaid";

    public DateTime? PaidAt { get; set; }

    public int? TransactionId { get; set; }
}