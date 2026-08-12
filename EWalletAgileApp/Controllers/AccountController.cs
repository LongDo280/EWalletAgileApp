using EWalletAgileApp.Data;
using EWalletAgileApp.Helpers;
using EWalletAgileApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EWalletAgileApp.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
    public AccountController(AppDbContext context)
    {
        _context = context;
    }
    private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(User user)
    {
        // Các trường nội bộ không được nhập từ form đăng ký
        ModelState.Remove(nameof(EWalletAgileApp.Models.User.PasswordSalt));
        ModelState.Remove(nameof(EWalletAgileApp.Models.User.Status));
        ModelState.Remove(nameof(EWalletAgileApp.Models.User.WalletStatus));
        ModelState.Remove(nameof(EWalletAgileApp.Models.User.Role));

        if (!ModelState.IsValid) return View(user);

        bool exists = await _context.Users.AnyAsync(u => u.Email == user.Email || u.Phone == user.Phone);
        if (exists)
        {
            ModelState.AddModelError("", "Email hoặc số điện thoại đã tồn tại");
            return View(user);
        }

        // US002 - Đăng nhập an toàn: không lưu mật khẩu dạng plain-text
        var salt = PasswordHelper.GenerateSalt();
        user.PasswordSalt = salt;
        user.Password = PasswordHelper.HashPassword(user.Password, salt);

        user.Balance = 0;
        user.Status = "Active";
        user.WalletStatus = "Active";
        user.Role = "User";
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.CreatedAt = DateTime.Now;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đăng ký thành công. Vui lòng đăng nhập.";
        return RedirectToAction("Login");
    }

    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Email == model.Account || u.Phone == model.Account);

        if (user == null)
        {
            ModelState.AddModelError("", "Sai tài khoản hoặc mật khẩu");
            return View(model);
        }

        // US002 - Chống brute-force: tài khoản đang bị khóa tạm do đăng nhập sai nhiều lần
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.Now)
        {
            var minutesLeft = Math.Ceiling((user.LockedUntil.Value - DateTime.Now).TotalMinutes);
            ModelState.AddModelError("", $"Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau {minutesLeft} phút.");
            return View(model);
        }

        bool passwordOk = PasswordHelper.Verify(model.Password, user.PasswordSalt, user.Password);
        if (!passwordOk)
        {
            user.FailedLoginCount += 1;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.Now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                await _context.SaveChangesAsync();
                ModelState.AddModelError("", $"Sai mật khẩu quá {MaxFailedAttempts} lần. Tài khoản đã bị tạm khóa {LockoutDuration.TotalMinutes} phút.");
                return View(model);
            }

            await _context.SaveChangesAsync();
            ModelState.AddModelError("", "Sai tài khoản hoặc mật khẩu");
            return View(model);
        }

        if (user.Status != "Active")
        {
            ModelState.AddModelError("", "Tài khoản đã bị khóa");
            return View(model);
        }

        // Đăng nhập thành công: reset bộ đếm sai
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        await _context.SaveChangesAsync();

        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("Role", user.Role);

        if (user.Role == "Admin")
            return RedirectToAction("Index", "Admin");

        return RedirectToAction("Index", "Wallet");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
    public async Task<IActionResult> Profile()
    {
        if (CurrentUserId == null) return RedirectToAction("Login");

        var user = await _context.Users.FindAsync(CurrentUserId.Value);
        if (user == null) return RedirectToAction("Login");

        var model = new ProfileViewModel
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Phone = user.Phone,
            Email = user.Email
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (CurrentUserId == null) return RedirectToAction("Login");
        if (!ModelState.IsValid) return View(model);

        var user = await _context.Users.FindAsync(CurrentUserId.Value);
        if (user == null) return RedirectToAction("Login");

        bool duplicate = await _context.Users.AnyAsync(u =>
            u.UserId != user.UserId && (u.Email == model.Email || u.Phone == model.Phone));
        if (duplicate)
        {
            ModelState.AddModelError("", "Email hoặc số điện thoại đã được sử dụng bởi tài khoản khác");
            return View(model);
        }

        user.FullName = model.FullName;
        user.Phone = model.Phone;
        user.Email = model.Email;
        await _context.SaveChangesAsync();

        HttpContext.Session.SetString("FullName", user.FullName);
        TempData["Success"] = "Cập nhật hồ sơ thành công";
        return RedirectToAction("Profile");
    }
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Email == model.Account || u.Phone == model.Account);

        if (user == null)
        {
            ModelState.AddModelError("", "Không tìm thấy tài khoản với email/số điện thoại này");
            return View(model);
        }

        // Sinh mã OTP 6 số, lưu tạm trong Session kèm thời hạn 5 phút.
        // Lưu ý: hệ thống hiện chưa tích hợp cổng gửi Email/SMS thật, nên mã OTP
        // được hiển thị trực tiếp trên giao diện để phục vụ demo/kiểm thử.
        var otp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString("ResetOtp", otp);
        HttpContext.Session.SetString("ResetAccount", model.Account);
        HttpContext.Session.SetString("ResetOtpExpiry", DateTime.Now.AddMinutes(5).ToString("O"));

        TempData["DemoOtp"] = otp;
        TempData["Success"] = "Mã OTP đã được tạo. Vui lòng nhập mã để đặt lại mật khẩu.";

        return RedirectToAction("ResetPassword", new { account = model.Account });
    }

    public IActionResult ResetPassword(string account)
    {
        if (string.IsNullOrEmpty(account)) return RedirectToAction("ForgotPassword");
        return View(new ResetPasswordViewModel { Account = account });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var sessionOtp = HttpContext.Session.GetString("ResetOtp");
        var sessionAccount = HttpContext.Session.GetString("ResetAccount");
        var expiryRaw = HttpContext.Session.GetString("ResetOtpExpiry");

        bool otpValid = sessionOtp != null
            && sessionAccount == model.Account
            && sessionOtp == model.Otp
            && expiryRaw != null
            && DateTime.Parse(expiryRaw) >= DateTime.Now;

        if (!otpValid)
        {
            ModelState.AddModelError("", "Mã OTP không đúng hoặc đã hết hạn. Vui lòng thử lại.");
            return View(model);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Email == model.Account || u.Phone == model.Account);

        if (user == null)
        {
            ModelState.AddModelError("", "Không tìm thấy tài khoản");
            return View(model);
        }

        var salt = PasswordHelper.GenerateSalt();
        user.PasswordSalt = salt;
        user.Password = PasswordHelper.HashPassword(model.NewPassword, salt);
        await _context.SaveChangesAsync();
        await _context.SaveChangesAsync();

        HttpContext.Session.Remove("ResetOtp");
        HttpContext.Session.Remove("ResetAccount");
        HttpContext.Session.Remove("ResetOtpExpiry");

        TempData["Success"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập với mật khẩu mới.";
        return RedirectToAction("Login");
    }
}
