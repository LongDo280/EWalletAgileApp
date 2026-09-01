using EWalletAgileApp.Data;
using EWalletAgileApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EWalletAgileApp.Controllers;

public class ReconciliationController : Controller
{
    private readonly AppDbContext _context;

    public ReconciliationController(AppDbContext context)
    {
        _context = context;
    }

    private int? CurrentUserId =>
        HttpContext.Session.GetInt32("UserId");

    private bool IsStaff()
    {
        var role = HttpContext.Session.GetString("Role");

        return role == "Staff" || role == "Admin";
    }

    private string GenerateTransactionCode()
    {
        return "EW" +
               Guid.NewGuid()
                   .ToString("N")
                   .Substring(0, 12)
                   .ToUpper();
    }

    public async Task<IActionResult> Index()
    {
        if (CurrentUserId == null)
            return RedirectToAction("Login", "Account");

        if (!IsStaff())
        {
            TempData["Error"] = "Bạn không có quyền thực hiện hoàn tiền.";
            return RedirectToAction(nameof(Index));
        }

        var failedTransactions = await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Receiver)
            .Where(t =>
                t.Status == "Failed" &&
                (t.Type == "Deposit" || t.Type == "Withdraw"))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View(failedTransactions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int transactionId)
    {
        // ==========================================
        // 1. Kiểm tra đăng nhập
        // ==========================================

        if (CurrentUserId == null)
            return RedirectToAction("Login", "Account");


        // ==========================================
        // 2. Kiểm tra quyền
        // Admin hoặc Staff được phép đối soát
        // ==========================================

        if (!IsStaff())
        {
            TempData["Error"] =
                "Bạn không có quyền thực hiện hoàn tiền.";

            return RedirectToAction(nameof(Index));
        }


        // ==========================================
        // 3. Tìm giao dịch gốc
        // ==========================================

        var transaction = await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Receiver)
            .FirstOrDefaultAsync(t =>
                t.TransactionId == transactionId);


        if (transaction == null)
        {
            TempData["Error"] =
                "Không tìm thấy giao dịch.";

            return RedirectToAction(nameof(Index));
        }


        // ==========================================
        // 4. Chỉ cho phép refund giao dịch Failed
        // ==========================================

        if (transaction.Status != "Failed")
        {
            TempData["Error"] =
                "Chỉ có thể hoàn tiền cho giao dịch lỗi.";

            return RedirectToAction(nameof(Index));
        }


        // ==========================================
        // 5. Kiểm tra đã Refund trước đó chưa
        // ==========================================

        var alreadyRefunded =
            await _context.Transactions
                .AnyAsync(t =>
                    t.Type == "Refund" &&
                    t.RelatedTransactionId ==
                        transaction.TransactionId &&
                    t.Status == "Success");


        if (alreadyRefunded)
        {
            TempData["Error"] =
                "Giao dịch này đã được hoàn tiền trước đó.";

            return RedirectToAction(nameof(Index));
        }


        // ==========================================
        // 6. Xác định user nhận tiền
        // ==========================================

        User? user = null;

        if (transaction.Type == "Deposit")
        {
            user = transaction.Receiver;
        }
        else if (transaction.Type == "Withdraw")
        {
            user = transaction.Sender;
        }


        if (user == null)
        {
            TempData["Error"] =
                "Không xác định được ví cần hoàn tiền.";

            return RedirectToAction(nameof(Index));
        }


        // ==========================================
        // 7. Kiểm tra hạn mức ví
        // ==========================================

        if (user.Balance + transaction.Amount >
            user.MaximumBalance)
        {
            TempData["Error"] =
                "Không thể hoàn tiền vì số dư ví sẽ vượt quá hạn mức.";

            return RedirectToAction(nameof(Index));
        }


        // ==========================================
        // 8. Bắt đầu Database Transaction
        // ==========================================

        await using var dbTransaction =
            await _context.Database.BeginTransactionAsync();


        try
        {
            // ======================================
            // 9. Cộng tiền vào ví
            // ======================================

            user.Balance += transaction.Amount;


            // ======================================
            // 10. Tạo giao dịch Refund
            // ======================================

            var refundTransaction = new Transaction
            {
                TransactionCode =
                    GenerateTransactionCode(),

                ReceiverId = user.UserId,

                Amount = transaction.Amount,

                Type = "Refund",

                Description =
                    $"Hoàn tiền giao dịch lỗi {transaction.TransactionCode}",

                Status = "Success",

                CreatedAt = DateTime.Now,

                RelatedTransactionId =
                    transaction.TransactionId
            };


            _context.Transactions.Add(refundTransaction);


            // ======================================
            // 11. Đánh dấu giao dịch gốc đã Refund
            // ======================================

            transaction.Status = "Refunded";


            // ======================================
            // 12. Lưu Database
            // ======================================

            await _context.SaveChangesAsync();


            // ======================================
            // 13. Commit
            // ======================================

            await dbTransaction.CommitAsync();


            TempData["Success"] =
                $"Đã hoàn tiền {transaction.Amount:N0}đ cho {user.FullName}.";


            return RedirectToAction(nameof(Index));
        }
        catch
        {
            // ======================================
            // 14. Nếu lỗi → Rollback
            // ======================================

            await dbTransaction.RollbackAsync();

            TempData["Error"] =
                "Có lỗi xảy ra. Giao dịch hoàn tiền đã được hủy.";

            return RedirectToAction(nameof(Index));
        }
    }
}