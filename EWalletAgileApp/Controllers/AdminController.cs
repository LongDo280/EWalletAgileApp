using EWalletAgileApp.Data;
using EWalletAgileApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EWalletAgileApp.Controllers;

public class AdminController : Controller
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }
    private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");
    private IActionResult? EnsureAdmin()
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Account");

        var role = HttpContext.Session.GetString("Role");
        if (role != "Admin") return RedirectToAction("Index", "Wallet");

        return null;
    }
    public async Task<IActionResult> Index()
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;

        ViewBag.UserCount = await _context.Users.CountAsync();
        ViewBag.TransactionCount = await _context.Transactions.CountAsync();
        ViewBag.TotalMoney = await _context.Users.SumAsync(u => u.Balance);
        return View();
    }

    public async Task<IActionResult> Users()
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;

        var users = await _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        return View(users);
    }

    public async Task<IActionResult> Transactions()
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;
        var transactions = await _context.Transactions
            .Include(t => t.Sender)
            .Include(t => t.Receiver)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return View(transactions);
    }

    public async Task<IActionResult> ToggleStatus(int id)
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            user.Status = user.Status == "Active" ? "Locked" : "Active";
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("Users");
    }
    public async Task<IActionResult> Roles()
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;

        var users = await _context.Users.OrderBy(u => u.FullName).ToListAsync();
        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeRole(int id, string role)
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;

        if (role != "User" && role != "Admin")
        {
            TempData["Success"] = "Vai trò không hợp lệ";
            return RedirectToAction("Roles");
        }

        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            if (user.UserId == CurrentUserId && role != "Admin")
            {
                TempData["Success"] = "Không thể tự hạ quyền Admin của chính mình";
                return RedirectToAction("Roles");
            }

            user.Role = role;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật vai trò cho {user.FullName}";
        }

        return RedirectToAction("Roles");
    }
    public async Task<IActionResult> EditLimits(int id)
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;

        var user = await _context.Users.FindAsync(id);
        if (user == null) return RedirectToAction("Users");

        var model = new WalletLimitViewModel
        {
            UserId = user.UserId,
            FullName = user.FullName,
            TransactionLimit = user.TransactionLimit,
            DailyLimit = user.DailyLimit,
            MaximumBalance = user.MaximumBalance
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> EditLimits(WalletLimitViewModel model)
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;
        if (!ModelState.IsValid) return View(model);

        var user = await _context.Users.FindAsync(model.UserId);
        if (user == null) return RedirectToAction("Users");

        user.TransactionLimit = model.TransactionLimit;
        user.DailyLimit = model.DailyLimit;
        user.MaximumBalance = model.MaximumBalance;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Đã cập nhật hạn mức ví cho {user.FullName}";
        return RedirectToAction("Users");
    }
    public async Task<IActionResult> WalletStatus()
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;

        var users = await _context.Users.OrderBy(u => u.FullName).ToListAsync();
        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleWalletStatus(int id, string? note)
    {
        var redirect = EnsureAdmin();
        if (redirect != null) return redirect;

        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            user.WalletStatus = user.WalletStatus == "Active" ? "Locked" : "Active";
            user.WalletStatusNote = note;
            user.WalletStatusUpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật trạng thái ví cho {user.FullName}";
        }

        return RedirectToAction("WalletStatus");
    }
}
