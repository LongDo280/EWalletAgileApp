using EWalletAgileApp.Data;
using EWalletAgileApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EWalletAgileApp.Controllers;

public class WalletController : Controller
{
    private readonly AppDbContext _context;

    public WalletController(AppDbContext context)
    {
        _context = context;
    }

    private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");
    private string GenerateTransactionCode()
    {
        return "EW" +
               Guid.NewGuid()
                   .ToString("N")
                   .Substring(0, 12)
                   .ToUpper();
    }
    private IActionResult? CheckLogin()
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Account");
        return null;

    }
    private async Task<User?> GetCurrentUserAsync()
    {
        if (CurrentUserId == null)
            return null;

        return await _context.Users.FindAsync(CurrentUserId.Value);
    }


    private bool IsWalletActive(User user)
    {
        return user.WalletStatus == "Active";
    }

    private void EnsureWalletIsActive(User user)
    {
        if (!IsWalletActive(user))
        {
            throw new InvalidOperationException("Ví của bạn đang bị khóa. Vui lòng liên hệ hỗ trợ.");
        }
    }
    private async Task<decimal> GetTodayTransactionTotalAsync(int userId)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        return await _context.Transactions
            .Where(t =>
                t.SenderId == userId &&
                t.Status == "Success" &&
                t.CreatedAt >= today &&
                t.CreatedAt < tomorrow)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
    }
    private async Task<(bool IsValid, string Message)> ValidateTransactionLimitAsync(
    User user,
    decimal amount)
    {
        if (amount < 1000)
            return (false, "Số tiền giao dịch tối thiểu là 1.000đ.");

        if (amount > user.TransactionLimit)
            return (
                false,
                $"Số tiền không được vượt quá hạn mức {user.TransactionLimit:N0}đ mỗi giao dịch."
            );

        var usedToday = await GetTodayTransactionTotalAsync(user.UserId);

        if (usedToday + amount > user.DailyLimit)
            return (
                false,
                $"Bạn đã sử dụng {usedToday:N0}đ hôm nay. Hạn mức ngày là {user.DailyLimit:N0}đ."
            );

        return (true, string.Empty);
    }
    public async Task<IActionResult> Index()
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var user = await _context.Users.FindAsync(CurrentUserId);
        var transactions = await _context.Transactions
            .Where(t => t.SenderId == CurrentUserId || t.ReceiverId == CurrentUserId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .ToListAsync();

        ViewBag.Transactions = transactions;
        return View(user);
    }

    public async Task<IActionResult> Deposit()
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var user = await GetCurrentUserAsync();

        if (user == null)
            return RedirectToAction("Login", "Account");

        var bankAccounts = await _context.BankAccounts
            .Where(b =>
                b.UserId == user.UserId &&
                b.Status == "Linked")
            .OrderByDescending(b => b.LinkedAt)
            .ToListAsync();

        ViewBag.BankAccounts = bankAccounts;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(decimal amount, int bankAccountId)
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var user = await GetCurrentUserAsync();

        if (user == null)
            return RedirectToAction("Login", "Account");

        if (!IsWalletActive(user))
        {
            ModelState.AddModelError(
                "",
                "Ví của bạn đang bị khóa."
            );

            return await Deposit();
        }

        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(b =>
                b.BankAccountId == bankAccountId &&
                b.UserId == user.UserId &&
                b.Status == "Linked");

        if (bankAccount == null)
        {
            ModelState.AddModelError(
                "",
                "Tài khoản ngân hàng không hợp lệ hoặc chưa được liên kết."
            );

            return await Deposit();
        }

        var validation = await ValidateTransactionLimitAsync(user, amount);

        if (!validation.IsValid)
        {
            ModelState.AddModelError("", validation.Message);
            return await Deposit();
        }

        if (user.Balance + amount > user.MaximumBalance)
        {
            ModelState.AddModelError(
                "",
                $"Số dư ví sau khi nạp không được vượt quá {user.MaximumBalance:N0}đ."
            );

            return await Deposit();
        }

        using var dbTransaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            user.Balance += amount;

            _context.Transactions.Add(new Transaction
            {
                TransactionCode = GenerateTransactionCode(),
                ReceiverId = user.UserId,
                Amount = amount,
                Type = "Deposit",
                Description =
                    $"Nạp tiền từ {bankAccount.BankName} - ****{bankAccount.AccountNumber[^4..]}",
                Status = "Success",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();

            ModelState.AddModelError(
                "",
                "Có lỗi xảy ra trong quá trình nạp tiền."
            );

            return await Deposit();
        }

        TempData["Success"] =
            $"Nạp thành công {amount:N0}đ từ {bankAccount.BankName}.";

        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Withdraw()
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var user = await GetCurrentUserAsync();

        if (user == null)
            return RedirectToAction("Login", "Account");

        var bankAccounts = await _context.BankAccounts
            .Where(b =>
                b.UserId == user.UserId &&
                b.Status == "Linked")
            .OrderByDescending(b => b.LinkedAt)
            .ToListAsync();

        ViewBag.BankAccounts = bankAccounts;

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(
        decimal amount,
        string pinCode,
        int bankAccountId)
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var user = await GetCurrentUserAsync();

        if (user == null)
            return RedirectToAction("Login", "Account");

        if (!IsWalletActive(user))
        {
            ModelState.AddModelError(
                "",
                "Ví của bạn đang bị khóa."
            );

            return await Withdraw();
        }

        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(b =>
                b.BankAccountId == bankAccountId &&
                b.UserId == user.UserId &&
                b.Status == "Linked");

        if (bankAccount == null)
        {
            ModelState.AddModelError(
                "",
                "Tài khoản ngân hàng không hợp lệ."
            );

            return await Withdraw();
        }

        if (user.PinCode != pinCode)
        {
            ModelState.AddModelError("", "Sai mã PIN.");
            return await Withdraw();
        }

        var validation =
            await ValidateTransactionLimitAsync(user, amount);

        if (!validation.IsValid)
        {
            ModelState.AddModelError("", validation.Message);
            return await Withdraw();
        }

        if (user.Balance < amount)
        {
            ModelState.AddModelError(
                "",
                "Số dư ví không đủ."
            );

            return await Withdraw();
        }

        using var dbTransaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            user.Balance -= amount;

            _context.Transactions.Add(new Transaction
            {
                TransactionCode = GenerateTransactionCode(),
                SenderId = user.UserId,
                Amount = amount,
                Type = "Withdraw",
                Description =
                    $"Rút tiền về {bankAccount.BankName} - ****{bankAccount.AccountNumber[^4..]}",
                Status = "Success",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();

            ModelState.AddModelError(
                "",
                "Có lỗi xảy ra trong quá trình rút tiền."
            );

            return await Withdraw();
        }

        TempData["Success"] =
            $"Rút thành công {amount:N0}đ về {bankAccount.BankName}.";

        return RedirectToAction("Index");
    }

    public IActionResult Transfer() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(
    TransferViewModel model)
    {
        var redirect = CheckLogin();

        if (redirect != null)
            return redirect;

        if (!ModelState.IsValid)
            return View(model);

        var sender = await GetCurrentUserAsync();

        if (sender == null)
            return RedirectToAction("Login", "Account");

        if (!IsWalletActive(sender))
        {
            ModelState.AddModelError(
                "",
                "Ví của bạn đang bị khóa."
            );

            return View(model);
        }

        var receiver = await _context.Users
            .FirstOrDefaultAsync(u =>
                u.Phone == model.ReceiverPhone);

        if (receiver == null)
        {
            ModelState.AddModelError(
                nameof(model.ReceiverPhone),
                "Không tìm thấy người nhận."
            );

            return View(model);
        }

        if (!IsWalletActive(receiver))
        {
            ModelState.AddModelError(
                "",
                "Ví người nhận đang bị khóa."
            );

            return View(model);
        }

        if (sender.UserId == receiver.UserId)
        {
            ModelState.AddModelError(
                "",
                "Không thể chuyển tiền cho chính mình."
            );

            return View(model);
        }

        if (sender.PinCode != model.PinCode)
        {
            ModelState.AddModelError(
                nameof(model.PinCode),
                "Sai mã PIN."
            );

            return View(model);
        }

        var validation =
            await ValidateTransactionLimitAsync(
                sender,
                model.Amount);

        if (!validation.IsValid)
        {
            ModelState.AddModelError(
                "",
                validation.Message
            );

            return View(model);
        }

        if (sender.Balance < model.Amount)
        {
            ModelState.AddModelError(
                "",
                "Số dư không đủ để thực hiện giao dịch."
            );

            return View(model);
        }

        using var dbTransaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            sender.Balance -= model.Amount;
            receiver.Balance += model.Amount;

            _context.Transactions.Add(new Transaction
            {
                TransactionCode = GenerateTransactionCode(),
                SenderId = sender.UserId,
                ReceiverId = receiver.UserId,
                Amount = model.Amount,
                Type = "Transfer",
                Description = string.IsNullOrWhiteSpace(model.Description)
                    ? $"Chuyển tiền cho {receiver.FullName}"
                    : model.Description,
                Status = "Success",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();

            ModelState.AddModelError(
                "",
                "Giao dịch thất bại. Vui lòng thử lại."
            );

            return View(model);
        }

        TempData["Success"] =
            $"Đã chuyển {model.Amount:N0}đ cho {receiver.FullName}.";

        return RedirectToAction("Index");
    }

    public async Task<IActionResult> PayBill()
    {
        var redirect = CheckLogin();
        if (redirect != null)
            return redirect;

        var user = await GetCurrentUserAsync();

        if (user == null)
            return RedirectToAction("Login", "Account");

        if (!IsWalletActive(user))
        {
            TempData["Error"] = "Ví của bạn đang bị khóa.";
            return RedirectToAction("Index");
        }

        return View(new PayBillViewModel());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LookupBill(
    string billType,
    string customerCode)
    {
        var redirect = CheckLogin();

        if (redirect != null)
            return redirect;

        var user = await GetCurrentUserAsync();

        if (user == null)
            return RedirectToAction("Login", "Account");

        if (!IsWalletActive(user))
        {
            ModelState.AddModelError(
                "",
                "Ví của bạn đang bị khóa."
            );

            return View("PayBill", new PayBillViewModel());
        }

        if (string.IsNullOrWhiteSpace(billType))
        {
            ModelState.AddModelError(
                "",
                "Vui lòng chọn loại hóa đơn."
            );

            return View(
                "PayBill",
                new PayBillViewModel
                {
                    BillType = billType,
                    CustomerCode = customerCode
                });
        }

        if (string.IsNullOrWhiteSpace(customerCode))
        {
            ModelState.AddModelError(
                "",
                "Vui lòng nhập mã khách hàng."
            );

            return View(
                "PayBill",
                new PayBillViewModel
                {
                    BillType = billType,
                    CustomerCode = customerCode
                });
        }

        // Tìm hóa đơn theo mã khách hàng + loại hóa đơn
        var bill = await _context.Bills
            .Where(b =>
                b.BillType == billType &&
                b.CustomerCode == customerCode)
            .OrderByDescending(b => b.BillId)
            .FirstOrDefaultAsync();

        // Không tồn tại hóa đơn
        if (bill == null)
        {
            ModelState.AddModelError(
                "",
                "Không tìm thấy hóa đơn với thông tin này."
            );

            return View(
                "PayBill",
                new PayBillViewModel
                {
                    BillType = billType,
                    CustomerCode = customerCode
                });
        }

        // Hóa đơn đã thanh toán
        if (bill.Status == "Paid")
        {
            ModelState.AddModelError(
                "",
                $"Hóa đơn này đã được thanh toán vào " +
                $"{bill.PaidAt:dd/MM/yyyy HH:mm}."
            );

            return View(
                "PayBill",
                new PayBillViewModel
                {
                    BillType = billType,
                    CustomerCode = customerCode
                });
        }

        // Hóa đơn chưa thanh toán
        var model = new PayBillViewModel
        {
            BillId = bill.BillId,
            BillType = bill.BillType,
            CustomerCode = bill.CustomerCode,
            CustomerName = bill.CustomerName,
            Amount = bill.Amount,
            BillingPeriod = bill.BillingPeriod
        };

        return View("PayBill", model);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayBill(
    PayBillViewModel model)
    {
        var redirect = CheckLogin();

        if (redirect != null)
            return redirect;

        var user = await GetCurrentUserAsync();

        if (user == null)
            return RedirectToAction("Login", "Account");

        if (!IsWalletActive(user))
        {
            ModelState.AddModelError(
                "",
                "Ví của bạn đang bị khóa."
            );

            return View("PayBill", model);
        }

        if (string.IsNullOrWhiteSpace(model.PinCode))
        {
            ModelState.AddModelError(
                nameof(model.PinCode),
                "Vui lòng nhập mã PIN."
            );

            return View("PayBill", model);
        }

        if (user.PinCode != model.PinCode)
        {
            ModelState.AddModelError(
                nameof(model.PinCode),
                "Sai mã PIN."
            );

            return View("PayBill", model);
        }

        var bill = await _context.Bills
            .FirstOrDefaultAsync(b =>
                b.BillId == model.BillId &&
                b.UserId == user.UserId &&
                b.Status == "Unpaid");

        if (bill == null)
        {
            ModelState.AddModelError(
                "",
                "Hóa đơn không tồn tại hoặc đã được thanh toán."
            );

            return View(
                "PayBill",
                new PayBillViewModel
                {
                    BillType = model.BillType,
                    CustomerCode = model.CustomerCode
                });
        }

        // Lấy số tiền trực tiếp từ Database
        var amount = bill.Amount;

        if (amount < 1000)
        {
            ModelState.AddModelError(
                "",
                "Số tiền hóa đơn không hợp lệ."
            );

            return View("PayBill", model);
        }

        if (user.Balance < amount)
        {
            ModelState.AddModelError(
                "",
                $"Số dư không đủ. Bạn cần {amount:N0}đ."
            );

            return View("PayBill", model);
        }

        if (amount > user.TransactionLimit)
        {
            ModelState.AddModelError(
                "",
                $"Hóa đơn vượt quá hạn mức " +
                $"{user.TransactionLimit:N0}đ mỗi giao dịch."
            );

            return View("PayBill", model);
        }

        var todayTotal =
            await GetTodayTransactionTotalAsync(user.UserId);

        if (todayTotal + amount > user.DailyLimit)
        {
            ModelState.AddModelError(
                "",
                $"Giao dịch vượt quá hạn mức ngày " +
                $"{user.DailyLimit:N0}đ."
            );

            return View("PayBill", model);
        }

        // =====================================================
        // DATABASE TRANSACTION
        // =====================================================

        using var dbTransaction =
            await _context.Database.BeginTransactionAsync();

        // Khai báo bên ngoài try để có thể dùng sau try
        Transaction transaction;

        try
        {
            // Trừ tiền
            user.Balance -= amount;

            // Tạo giao dịch
            transaction = new Transaction
            {
                TransactionCode =
                    GenerateTransactionCode(),

                SenderId = user.UserId,

                ReceiverId = null,

                Amount = amount,

                Type = "Bill",

                Description =
                    $"Thanh toán {bill.BillType} - " +
                    $"{bill.CustomerCode}",

                Status = "Success",

                CreatedAt = DateTime.Now
            };

            _context.Transactions.Add(transaction);

            // Cập nhật hóa đơn
            bill.Status = "Paid";

            bill.PaidAt = DateTime.Now;

            // Lưu lần 1 để EF sinh TransactionId
            await _context.SaveChangesAsync();

            // Liên kết Bill với Transaction
            bill.TransactionId =
                transaction.TransactionId;

            // Lưu lần 2
            await _context.SaveChangesAsync();

            // Commit
            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();

            ModelState.AddModelError(
                "",
                "Thanh toán thất bại. Vui lòng thử lại."
            );

            return View("PayBill", model);
        }

        // =====================================================
        // THANH TOÁN THÀNH CÔNG
        // =====================================================

        return RedirectToAction(
            "PaymentSuccess",
            new
            {
                id = transaction.TransactionId
            });
    }
    public async Task<IActionResult> History()
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var transactions = await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Receiver)
            .Where(t => t.SenderId == CurrentUserId || t.ReceiverId == CurrentUserId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View(transactions);
    }
    public async Task<IActionResult> Limits()
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var usedToday = await GetTodayTransactionTotalAsync(user.UserId);
        ViewBag.UsedToday = usedToday;
        return View(user);
    }
    public async Task<IActionResult> BankAccounts()
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var accounts = await _context.BankAccounts
            .Where(b => b.UserId == CurrentUserId)
            .OrderByDescending(b => b.LinkedAt)
            .ToListAsync();

        return View(accounts);
    }

    public IActionResult LinkBank() => View();

    [HttpPost]
    public async Task<IActionResult> LinkBank(BankAccountViewModel model)
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;
        if (!ModelState.IsValid) return View(model);

        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        if (user.PinCode != model.PinCode)
        {
            ModelState.AddModelError("", "Sai mã PIN");
            return View(model);
        }

        bool alreadyLinked = await _context.BankAccounts.AnyAsync(b =>
            b.UserId == user.UserId &&
            b.BankName == model.BankName &&
            b.AccountNumber == model.AccountNumber &&
            b.Status == "Linked");

        if (alreadyLinked)
        {
            ModelState.AddModelError("", "Tài khoản ngân hàng này đã được liên kết trước đó");
            return View(model);
        }

        _context.BankAccounts.Add(new BankAccount
        {
            UserId = user.UserId,
            BankName = model.BankName,
            AccountNumber = model.AccountNumber,
            AccountHolderName = model.AccountHolderName,
            Status = "Linked",
            LinkedAt = DateTime.Now
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = "Liên kết tài khoản ngân hàng thành công";
        return RedirectToAction("BankAccounts");
    }

    [HttpPost]
    public async Task<IActionResult> UnlinkBank(int id)
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var account = await _context.BankAccounts
            .FirstOrDefaultAsync(b => b.BankAccountId == id && b.UserId == CurrentUserId);

        if (account == null)
        {
            TempData["Success"] = "Không tìm thấy liên kết ngân hàng";
            return RedirectToAction("BankAccounts");
        }

        account.Status = "Unlinked";
        account.UnlinkedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã hủy liên kết tài khoản ngân hàng";
        return RedirectToAction("BankAccounts");
    }
    public async Task<IActionResult> PaymentSuccess(int id)
    {
        var redirect = CheckLogin();

        if (redirect != null)
            return redirect;

        var user = await GetCurrentUserAsync();

        if (user == null)
            return RedirectToAction("Login", "Account");

        var transaction = await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Receiver)
            .FirstOrDefaultAsync(t =>
                t.TransactionId == id &&
                (t.SenderId == user.UserId ||
                 t.ReceiverId == user.UserId));

        if (transaction == null)
        {
            return NotFound();
        }

        var bill = await _context.Bills
            .FirstOrDefaultAsync(b =>
                b.TransactionId == transaction.TransactionId);

        var model = new TransactionResultViewModel
        {
            Transaction = transaction,
            Bill = bill
        };

        return View(model);
    }
}
