using EWalletAgileApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EWalletAgileApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<User>()
            .Property(u => u.Balance)
            .HasPrecision(18, 2);

        modelBuilder.Entity<User>()
            .Property(u => u.TransactionLimit)
            .HasPrecision(18, 2);

        modelBuilder.Entity<User>()
            .Property(u => u.DailyLimit)
            .HasPrecision(18, 2);

        modelBuilder.Entity<User>()
            .Property(u => u.MaximumBalance)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Amount)
            .HasPrecision(18, 2);
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Phone)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Sender)
            .WithMany(u => u.SentTransactions)
            .HasForeignKey(t => t.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Receiver)
            .WithMany(u => u.ReceivedTransactions)
            .HasForeignKey(t => t.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Bill>()
    .Property(b => b.Amount)
    .HasPrecision(18, 2);

        modelBuilder.Entity<Bill>()
            .HasOne(b => b.User)
            .WithMany(u => u.Bills)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Transaction>()
    .HasIndex(t => t.TransactionCode)
    .IsUnique();

        modelBuilder.Entity<Bill>()
            .Property(b => b.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Bill>()
            .HasOne(b => b.User)
            .WithMany(u => u.Bills)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        // US010/US011 - Liên kết / hủy liên kết tài khoản ngân hàng
        modelBuilder.Entity<BankAccount>()
            .HasOne(b => b.User)
            .WithMany(u => u.BankAccounts)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mật khẩu mẫu bên dưới đã được băm SHA-256 + salt (xem Helpers/PasswordHelper.cs), KHÔNG còn plain-text.
        // user1@gmail.com mật khẩu gốc: 123456 | user2@gmail.com mật khẩu gốc: 123456 | admin@ewallet.com mật khẩu gốc: admin@123
        modelBuilder.Entity<User>().HasData(new User
        {
            UserId = 1,
            FullName = "Nguyễn Văn A",
            Phone = "0900000001",
            Email = "user1@gmail.com",
            Password = "U03/hiz8j9IXqUH/pNgwOwKNfP8LDSby5gTEyBJFlg0=",
            PasswordSalt = "seed-salt-u1-9f3a",
            FailedLoginCount = 0,
            LockedUntil = null,
            PinCode = "1234",
            Balance = 1000000,
            Status = "Active",
            WalletStatus = "Active",
            Role = "User",
            CreatedAt = new DateTime(2026, 1, 1)
        }, new User
        {
            UserId = 2,
            FullName = "Trần Thị B",
            Phone = "0900000002",
            Email = "user2@gmail.com",
            Password = "u1VN9x4wx6xzhnMlfhTMtmHAM3dVwXd8SKbWMadWELo=",
            PasswordSalt = "seed-salt-u2-7c1e",
            FailedLoginCount = 0,
            LockedUntil = null,
            PinCode = "1234",
            Balance = 500000,
            Status = "Active",
            WalletStatus = "Active",
            Role = "User",
            CreatedAt = new DateTime(2026, 1, 1)
        }, new User
        {
            // US005 - Tài khoản có vai trò Admin để quản trị hệ thống (Quản lý vai trò, Trạng thái ví, Hạn mức...)
            UserId = 3,
            FullName = "Quản trị viên hệ thống",
            Phone = "0900000099",
            Email = "admin@ewallet.com",
            Password = "Yrrw/UD5C2bN3Y6OGIZN1D2+JkEMK3t/jJ9umtZXus4=",
            PasswordSalt = "seed-salt-admin-2b8d",
            FailedLoginCount = 0,
            LockedUntil = null,
            PinCode = "9999",
            Balance = 0,
            Status = "Active",
            WalletStatus = "Active",
            Role = "Admin",
            CreatedAt = new DateTime(2026, 1, 1)
        });

        modelBuilder.Entity<Admin>().HasData(new Admin
        {
            AdminId = 1,
            Username = "admin",
            Password = "123456",
            FullName = "Quản trị viên",
            Role = "Admin"
        });
        modelBuilder.Entity<Bill>().HasData(
    new Bill
    {
        BillId = 1,
        UserId = 1,
        BillType = "Điện",
        CustomerCode = "EVN123456",
        CustomerName = "Nguyễn Văn A",
        Amount = 350000,
        BillingPeriod = "08/2026",
        Status = "Unpaid",
        PaidAt = null,
        TransactionId = null
    },
    new Bill
    {
        BillId = 2,
        UserId = 1,
        BillType = "Internet",
        CustomerCode = "NET123456",
        CustomerName = "Nguyễn Văn A",
        Amount = 250000,
        BillingPeriod = "08/2026",
        Status = "Unpaid",
        PaidAt = null,
        TransactionId = null
    },
    new Bill
    {
        BillId = 3,
        UserId = 2,
        BillType = "Nước",
        CustomerCode = "WATER123456",
        CustomerName = "Nguyễn Văn A",
        Amount = 180000,
        BillingPeriod = "08/2026",
        Status = "Unpaid",
        PaidAt = null,
        TransactionId = null
    }
);
    }
}
