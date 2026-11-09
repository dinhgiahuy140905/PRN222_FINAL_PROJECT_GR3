using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers;

public partial class AccountController
{
    [AllowAnonymous, HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model,
        [FromServices] PasswordResetService reset,
        [FromServices] IResetEmailSender sender,
        [FromServices] IConfiguration configuration,
        [FromServices] IWebHostEnvironment environment,
        [FromServices] ILogger<AccountController> logger)
    {
        if (!ModelState.IsValid) return View(model);
        var baseUrl = configuration["PasswordReset:PublicBaseUrl"]
            ?? (environment.IsDevelopment() ? "https://localhost:7140" : null);
        if (!sender.IsConfigured || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(origin.UserInfo))
        {
            ModelState.AddModelError(string.Empty, "Chức năng gửi email chưa sẵn sàng. Vui lòng liên hệ người quản lý.");
            return View(model);
        }
        var email = NormalizeEmail(model.Email);
        var user = await _context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Email == email);
        if (user is not null)
        {
            var link = new Uri(origin, "/Account/ResetPassword?token=" + Uri.EscapeDataString(reset.CreateToken(user)));
            try { await sender.SendAsync(user.Email, link); }
            catch (Exception error) when (error is System.Net.Mail.SmtpException or IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                // Never log email bodies/tokens or reveal whether this account exists.
                logger.LogWarning("Password reset email delivery failed ({ErrorType}).", error.GetType().Name);
            }
        }
        ViewData["Sent"] = true;
        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous, HttpGet]
    public IActionResult ResetPassword(string? token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return View(new ResetPasswordViewModel { Token = token ?? string.Empty });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, [FromServices] PasswordResetService reset)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!ModelState.IsValid) return View(model);
        if (!await reset.ResetAsync(model.Token, model.Password))
        {
            ModelState.AddModelError(string.Empty, "Liên kết không hợp lệ, đã hết hạn hoặc đã được sử dụng. Vui lòng yêu cầu liên kết mới.");
            return View(model);
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["PasswordResetSuccess"] = "Đã đặt lại mật khẩu. Vui lòng đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }
}
