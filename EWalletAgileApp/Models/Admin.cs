using System.ComponentModel.DataAnnotations;

namespace EWalletAgileApp.Models;

public class Admin
{
    public int AdminId { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
}
