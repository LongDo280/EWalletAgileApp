using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class Fee
{
    public int FeeId { get; set; }

    [Required]
    public string TransactionType { get; set; } = string.Empty;

    [Required]
    public string FeeType { get; set; } = "Percent";

    public decimal Value { get; set; } = 0;

    public decimal MaxFee { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}