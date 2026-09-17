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

        var months = Enumerable.Range(0, 6)
            .Select(i => DateTime.Today.AddMonths(-5 + i))
            .Select(d => new DateTime(d.Year, d.Month, 1))
            .ToList();

        var spendingTransactions = await _context.Transactions
            .Where(t =>
                t.SenderId == CurrentUserId &&
                t.Status == "Success" &&
                (t.Type == "Withdraw" || t.Type == "Transfer" || t.Type == "Bill") &&
                t.CreatedAt >= months[0])
            .ToListAsync();

        var chartLabels = months
            .Select(m => $"Th{m.Month:00}/{m.Year}")
            .ToList();

        var chartValues = months
            .Select(m => spendingTransactions
                .Where(t => t.CreatedAt.Year == m.Year && t.CreatedAt.Month == m.Month)
                .Sum(t => t.Amount))
            .ToList();

        ViewBag.SpendingChartLabels = chartLabels;
        ViewBag.SpendingChartValues = chartValues;

        var currentMonth = months[months.Count - 1];

        ViewBag.MonthlyExpense = chartValues[chartValues.Count - 1];

        ViewBag.MonthlyTransactionCount = await _context.Transactions
            .Where(t =>
                (t.SenderId == CurrentUserId || t.ReceiverId == CurrentUserId) &&
                t.Status == "Success" &&
                t.CreatedAt.Year == currentMonth.Year &&
                t.CreatedAt.Month == currentMonth.Month)
            .CountAsync();

        ViewBag.LinkedBankCount = await _context.BankAccounts
            .Where(b => b.UserId == CurrentUserId && b.Status == "Linked")
            .CountAsync();

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
        ViewBag.TransactionLimit = user.TransactionLimit;   
        ViewBag.DailyLimit = user.DailyLimit;               
        ViewBag.CurrentBalance = user.Balance;
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
            ModelState.AddModelError("", "Ví của bạn đang bị khóa.");
            return await Deposit();
        }

        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(b =>
                b.BankAccountId == bankAccountId &&
                b.UserId == user.UserId &&
                b.Status == "Linked");

        if (bankAccount == null)
        {
            ModelState.AddModelError("", "Tài khoản ngân hàng không hợp lệ hoặc chưa được liên kết.");
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
            ModelState.AddModelError("", $"Số dư ví sau khi nạp không được vượt quá {user.MaximumBalance:N0}đ.");
            return await Deposit();
        }

        // US016 - Áp dụng phí nạp tiền (nếu Admin có cấu hình)
        var fee = await CalculateFeeAsync("Deposit", amount);
        var netCredit = amount - fee;

        using var dbTransaction = await _context.Database.BeginTransactionAsync();

        try
        {
            user.Balance += netCredit;

            _context.Transactions.Add(new Transaction
            {
                TransactionCode = GenerateTransactionCode(),
                ReceiverId = user.UserId,
                Amount = amount,
                FeeAmount = fee,
                Type = "Deposit",
                Description =
                    $"Nạp tiền từ {bankAccount.BankName} - ****{bankAccount.AccountNumber[^4..]}" +
                    (fee > 0 ? $" (phí {fee:N0}đ)" : ""),
                Status = "Success",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            ModelState.AddModelError("", "Có lỗi xảy ra trong quá trình nạp tiền.");
            return await Deposit();
        }

        TempData["Success"] = fee > 0
            ? $"Nạp thành công {netCredit:N0}đ vào ví (đã trừ phí {fee:N0}đ) từ {bankAccount.BankName}."
            : $"Nạp thành công {amount:N0}đ từ {bankAccount.BankName}.";

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

    // BƯỚC 1: Xử lý thông tin rút tiền (Không kiểm tra mã PIN ở đây)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(decimal amount, int bankAccountId)
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        if (!IsWalletActive(user))
        {
            ModelState.AddModelError("", "Ví của bạn đang bị khóa.");
            return await Withdraw();
        }

        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(b =>
                b.BankAccountId == bankAccountId &&
                b.UserId == user.UserId &&
                b.Status == "Linked");

        if (bankAccount == null)
        {
            ModelState.AddModelError("", "Tài khoản ngân hàng không hợp lệ.");
            return await Withdraw();
        }

        var validation = await ValidateTransactionLimitAsync(user, amount);
        if (!validation.IsValid)
        {
            ModelState.AddModelError("", validation.Message);
            return await Withdraw();
        }

        var fee = await CalculateFeeAsync("Withdraw", amount);
        var totalDeduct = amount + fee;

        if (user.Balance < totalDeduct)
        {
            ModelState.AddModelError("", $"Số dư ví không đủ (bao gồm phí {fee:N0}đ).");
            return await Withdraw();
        }

        // Lưu thông tin hợp lệ vào Session để chờ xác nhận PIN
        HttpContext.Session.SetString("WithdrawAmount", amount.ToString());
        HttpContext.Session.SetInt32("WithdrawBankId", bankAccountId);
        HttpContext.Session.SetString("WithdrawFee", fee.ToString());

        return RedirectToAction("ConfirmWithdraw");
    }

    // BƯỚC 2: Màn hình yêu cầu nhập mã PIN
    public IActionResult ConfirmWithdraw()
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        // Nếu không có session rút tiền, đẩy về lại trang rút tiền
        if (HttpContext.Session.GetInt32("WithdrawBankId") == null)
            return RedirectToAction("Withdraw");

        return View();
    }

    // BƯỚC 3: Xác nhận mã PIN và tiến hành trừ tiền
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmWithdraw(string pinCode)
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var bankId = HttpContext.Session.GetInt32("WithdrawBankId");
        var amountStr = HttpContext.Session.GetString("WithdrawAmount");
        var feeStr = HttpContext.Session.GetString("WithdrawFee");

        if (bankId == null || amountStr == null || feeStr == null)
            return RedirectToAction("Withdraw");

        if (user.PinCode != pinCode)
        {
            ModelState.AddModelError("", "Sai mã PIN.");
            return View(); // Trả lại view nhập PIN
        }

        decimal amount = decimal.Parse(amountStr);
        decimal fee = decimal.Parse(feeStr);
        var totalDeduct = amount + fee;

        // Kiểm tra lại số dư lần cuối trước khi trừ (đề phòng tab khác đã tiêu tiền)
        if (user.Balance < totalDeduct)
        {
            ModelState.AddModelError("", "Số dư không đủ để thực hiện.");
            return View();
        }

        var bankAccount = await _context.BankAccounts.FindAsync(bankId.Value);

        using var dbTransaction = await _context.Database.BeginTransactionAsync();

        try
        {
            user.Balance -= totalDeduct;

            _context.Transactions.Add(new Transaction
            {
                TransactionCode = GenerateTransactionCode(),
                SenderId = user.UserId,
                Amount = amount,
                FeeAmount = fee,
                Type = "Withdraw",
                Description =
                    $"Rút tiền về {bankAccount.BankName} - ****{bankAccount.AccountNumber[^4..]}" +
                    (fee > 0 ? $" (phí {fee:N0}đ)" : ""),
                Status = "Pending",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            ModelState.AddModelError("", "Có lỗi xảy ra trong quá trình rút tiền.");
            return View();
        }

        // Dọn dẹp Session sau khi thành công
        HttpContext.Session.Remove("WithdrawAmount");
        HttpContext.Session.Remove("WithdrawBankId");
        HttpContext.Session.Remove("WithdrawFee");

        TempData["Success"] = $"Rút thành công {amount:N0}đ về {bankAccount.BankName}.";

        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Transfer(string? receiverPhone = null)
    {
        var model = new TransferViewModel();
        if (!string.IsNullOrWhiteSpace(receiverPhone)) model.ReceiverPhone = receiverPhone;

        var currentUser = await GetCurrentUserAsync();
        ViewBag.CurrentBalance = currentUser?.Balance ?? 0m;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(TransferViewModel model)
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;
        if (!ModelState.IsValid) return View(model);

        var sender = await GetCurrentUserAsync();
        if (sender == null) return RedirectToAction("Login", "Account");

        if (!IsWalletActive(sender))
        {
            ModelState.AddModelError("", "Ví của bạn đang bị khóa.");
            return View(model);
        }

        var receiver = await _context.Users.FirstOrDefaultAsync(u => u.Phone == model.ReceiverPhone);
        if (receiver == null)
        {
            ModelState.AddModelError(nameof(model.ReceiverPhone), "Không tìm thấy người nhận.");
            return View(model);
        }
        if (!IsWalletActive(receiver))
        {
            ModelState.AddModelError("", "Ví người nhận đang bị khóa.");
            return View(model);
        }
        if (sender.UserId == receiver.UserId)
        {
            ModelState.AddModelError("", "Không thể chuyển tiền cho chính mình.");
            return View(model);
        }
        if (sender.PinCode != model.PinCode)
        {
            ModelState.AddModelError(nameof(model.PinCode), "Sai mã PIN.");
            return View(model);
        }

        var validation = await ValidateTransactionLimitAsync(sender, model.Amount);
        if (!validation.IsValid)
        {
            ModelState.AddModelError("", validation.Message);
            return View(model);
        }
        if (sender.Balance < model.Amount)
        {
            ModelState.AddModelError("", "Số dư không đủ để thực hiện giao dịch.");
            return View(model);
        }

        // US022: không thực hiện ngay — lưu tạm, bắt buộc xác nhận OTP trước khi trừ tiền
        var otp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString("TransferOtp", otp);
        HttpContext.Session.SetInt32("TransferSenderId", sender.UserId);
        HttpContext.Session.SetInt32("TransferReceiverId", receiver.UserId);
        HttpContext.Session.SetString("TransferAmount", model.Amount.ToString());
        HttpContext.Session.SetString("TransferDescription", model.Description ?? "");
        HttpContext.Session.SetString("TransferOtpExpiry", DateTime.Now.AddMinutes(5).ToString("O"));

        TempData["DemoOtp"] = otp; // demo vì chưa có SMS/Email gateway thật (giống US003)
        return RedirectToAction("ConfirmTransferOtp");
    }

    public IActionResult ConfirmTransferOtp()
    {
        if (HttpContext.Session.GetInt32("TransferSenderId") == null)
            return RedirectToAction("Transfer");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmTransferOtp(string otp)
    {
        var senderId = HttpContext.Session.GetInt32("TransferSenderId");
        var receiverId = HttpContext.Session.GetInt32("TransferReceiverId");
        var sessionOtp = HttpContext.Session.GetString("TransferOtp");
        var expiryRaw = HttpContext.Session.GetString("TransferOtpExpiry");
        var amountRaw = HttpContext.Session.GetString("TransferAmount");
        var description = HttpContext.Session.GetString("TransferDescription") ?? "";

        if (senderId == null || receiverId == null || amountRaw == null)
            return RedirectToAction("Transfer");

        bool otpValid = sessionOtp != null && sessionOtp == otp &&
            expiryRaw != null && DateTime.Parse(expiryRaw) >= DateTime.Now;

        if (!otpValid)
        {
            ModelState.AddModelError("", "Mã OTP không đúng hoặc đã hết hạn.");
            return View();
        }

        var sender = await _context.Users.FindAsync(senderId.Value);
        var receiver = await _context.Users.FindAsync(receiverId.Value);
        var amount = decimal.Parse(amountRaw);

        if (sender == null || receiver == null || sender.Balance < amount)
        {
            ModelState.AddModelError("", "Giao dịch không còn hợp lệ, vui lòng thực hiện lại.");
            return RedirectToAction("Transfer");
        }

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            sender.Balance -= amount;
            receiver.Balance += amount;

            _context.Transactions.Add(new Transaction
            {
                TransactionCode = GenerateTransactionCode(),
                SenderId = sender.UserId,
                ReceiverId = receiver.UserId,
                Amount = amount,
                Type = "Transfer",
                Description = string.IsNullOrWhiteSpace(description)
                    ? $"Chuyển tiền cho {receiver.FullName}"
                    : description,
                Status = "Success",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            ModelState.AddModelError("", "Giao dịch thất bại. Vui lòng thử lại.");
            return View();
        }

        HttpContext.Session.Remove("TransferOtp");
        HttpContext.Session.Remove("TransferSenderId");
        HttpContext.Session.Remove("TransferReceiverId");
        HttpContext.Session.Remove("TransferAmount");
        HttpContext.Session.Remove("TransferDescription");
        HttpContext.Session.Remove("TransferOtpExpiry");

        TempData["Success"] = $"Đã chuyển {amount:N0}đ cho {receiver.FullName}.";
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
    // US016 - Lấy biểu phí đang áp dụng (nếu có) và tính số tiền phí
    // cho một giao dịch nạp/rút theo loại "Deposit"/"Withdraw".
    private async Task<decimal> CalculateFeeAsync(string transactionType, decimal amount)
    {
        var fee = await _context.Fees
            .Where(f => f.TransactionType == transactionType && f.IsActive)
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();

        if (fee == null || fee.Value <= 0)
            return 0;

        decimal feeAmount = fee.FeeType == "Fixed"
            ? fee.Value
            : Math.Round(amount * fee.Value / 100m, 0);

        if (fee.MaxFee > 0 && feeAmount > fee.MaxFee)
            feeAmount = fee.MaxFee;

        return feeAmount;
    }

#if DEBUG

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTestFailedTransaction(decimal amount)
    {
        var redirect = CheckLogin();

        if (redirect != null)
            return redirect;

        var user = await GetCurrentUserAsync();

        if (user == null)
            return RedirectToAction("Login", "Account");

        if (amount < 1000)
            amount = 100000;

        var transaction = new Transaction
        {
            TransactionCode = GenerateTransactionCode(),
            ReceiverId = user.UserId,
            Amount = amount,
            Type = "Deposit",
            Description = "Giao dịch nạp tiền lỗi - TEST US017",
            Status = "Failed",
            CreatedAt = DateTime.Now
        };

        _context.Transactions.Add(transaction);

        await _context.SaveChangesAsync();

        TempData["Success"] =
            $"Đã tạo giao dịch lỗi {amount:N0}đ để kiểm thử US017.";

        return RedirectToAction("History");
    }
    // ---------------- US015: Tra cứu trạng thái giao dịch ----------------
    public async Task<IActionResult> TransactionStatus(string code)
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        if (string.IsNullOrWhiteSpace(code)) return View();

        var transaction = await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Receiver)
            .FirstOrDefaultAsync(t => t.TransactionCode == code &&
                (t.SenderId == CurrentUserId || t.ReceiverId == CurrentUserId));

        if (transaction == null) ViewBag.NotFound = true;
        return View(transaction);
    }

    // ---------------- US023: Hủy giao dịch rút tiền đang chờ xử lý ----------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelWithdraw(int id)
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;

        var transaction = await _context.Transactions.FirstOrDefaultAsync(t =>
            t.TransactionId == id && t.SenderId == CurrentUserId && t.Type == "Withdraw");

        if (transaction == null || transaction.Status != "Pending")
        {
            TempData["Success"] = "Không thể hủy giao dịch này.";
            return RedirectToAction("History");
        }

        var user = await _context.Users.FindAsync(CurrentUserId);
        if (user == null) return RedirectToAction("Login", "Account");

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            user.Balance += transaction.Amount + transaction.FeeAmount; // hoàn lại cả phí đã giữ
            transaction.Status = "Cancelled";
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            TempData["Success"] = "Có lỗi xảy ra, vui lòng thử lại.";
            return RedirectToAction("History");
        }

        TempData["Success"] = $"Đã hủy giao dịch rút tiền {transaction.Amount:N0}đ và hoàn tiền vào ví.";
        return RedirectToAction("History");
    }
#endif
    // ---------------- US021: Thanh toán bằng QR Code ----------------
    public async Task<IActionResult> MyQr()
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");
        return View(user);
    }

    public async Task<IActionResult> MyQrImage()
    {
        var redirect = CheckLogin();
        if (redirect != null) return redirect;
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        using var qrGenerator = new QRCoder.QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(user.Phone, QRCoder.QRCodeGenerator.ECCLevel.Q);
        var qrCode = new QRCoder.PngByteQRCode(qrData);
        var bytes = qrCode.GetGraphic(10);
        return File(bytes, "image/png");
    }

    public IActionResult PayByQr() => View();

    [HttpPost]
    public IActionResult PayByQr(string qrContent)
    {
        if (string.IsNullOrWhiteSpace(qrContent))
        {
            ModelState.AddModelError("", "Vui lòng nhập/dán nội dung mã QR.");
            return View();
        }
        return RedirectToAction("Transfer", new { receiverPhone = qrContent });
    }
}
