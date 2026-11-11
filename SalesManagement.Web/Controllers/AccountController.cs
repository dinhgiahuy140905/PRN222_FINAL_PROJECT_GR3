using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers
{
    public partial class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IUserSessionService _userSession;

        public AccountController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            IUserSessionService userSession)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _userSession = userSession;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = NormalizeEmail(model.Email);
            var user = await _context.Users.SingleOrDefaultAsync(item => item.Email == email);

            var verificationResult = VerifyPassword(user, model.Password);
            if (user is null || verificationResult == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
                return View(model);
            }

            if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);
                await _context.SaveChangesAsync();
            }

            var membership = await _context.StoreMembers
                .Include(member => member.Store)
                .Where(member => member.UserId == user.Id)
                .OrderByDescending(member => member.StoreId == user.DefaultStoreId)
                .ThenByDescending(member => member.Role == StoreRole.Owner)
                .ThenBy(member => member.Store.Name)
                .FirstOrDefaultAsync();

            await _userSession.SignInAsync(user, membership, model.RememberMe);
            if (membership is null)
            {
                return user.AccountType == AccountType.Owner
                    ? RedirectToAction("Create", "Store", new { firstStore = true })
                    : RedirectToAction("Select", "Store");
            }

            return RedirectToLocal(returnUrl);
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard", "Home");
            }

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = NormalizeEmail(model.Email);
            if (await _context.Users.AnyAsync(item => item.Email == email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email này đã được sử dụng.");
                return View(model);
            }

            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = email,
                AccountType = model.AccountType!.Value
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _userSession.SignInAsync(user, membership: null, isPersistent: false);
            return user.AccountType == AccountType.Owner
                ? RedirectToAction("Create", "Store", new { firstStore = true })
                : RedirectToAction("Select", "Store");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Dashboard", "Home")!;
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private PasswordVerificationResult VerifyPassword(User? user, string password)
        {
            if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return PasswordVerificationResult.Failed;
            }

            try
            {
                return _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            }
            catch (FormatException)
            {
                // Treat legacy or damaged password data as invalid credentials instead of
                // exposing an application error. The account can then be recovered safely.
                return PasswordVerificationResult.Failed;
            }
        }
    }
}
