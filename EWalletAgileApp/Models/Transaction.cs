using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class Transaction
{
    public int TransactionId { get; set; }

    [Required]
    public string TransactionCode { get; set; } = string.Empty;

    public int? SenderId { get; set; }

    public User? Sender { get; set; }

    public int? ReceiverId { get; set; }

    public User? Receiver { get; set; }

    [Range(1000, double.MaxValue)]
    public decimal Amount { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "Success";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}