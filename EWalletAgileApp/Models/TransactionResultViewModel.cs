namespace EWalletAgileApp.Models;

public class TransactionResultViewModel
{
    public Transaction Transaction { get; set; } = null!;

    public Bill? Bill { get; set; }
}