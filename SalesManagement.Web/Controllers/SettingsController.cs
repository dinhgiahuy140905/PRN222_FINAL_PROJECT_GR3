using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private static readonly HashSet<string> AllowedImageExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreContext _currentStore;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IUserSessionService _userSession;
        private readonly IWebHostEnvironment _environment;

        public SettingsController(
            ApplicationDbContext context,
            ICurrentStoreContext currentStore,
            IPasswordHasher<User> passwordHasher,
            IUserSessionService userSession,
            IWebHostEnvironment environment)
        {
            _context = context;
            _currentStore = currentStore;
            _passwordHasher = passwordHasher;
            _userSession = userSession;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string section = "profile")
        {
            var model = await BuildPageAsync(section);
            return model is null ? Challenge() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(
            [Bind(Prefix = "Profile")] ProfileSettingsViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user is null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                return View("Index", await BuildPageAsync("profile", profile: model));
            }

            var avatarPath = await SaveImageAsync(
                model.Avatar,
                "avatars",
                "Profile.Avatar");
            if (!ModelState.IsValid)
            {
                model.AvatarPath = user.AvatarPath;
                return View("Index", await BuildPageAsync("profile", profile: model));
            }

            user.FullName = model.FullName.Trim();
            user.Phone = NormalizeOptional(model.Phone);
            if (avatarPath is not null)
            {
                user.AvatarPath = avatarPath;
            }

            await _context.SaveChangesAsync();
            await RefreshSessionAsync(user);
            TempData["Success"] = "Đã cập nhật hồ sơ.";
            return RedirectToAction(nameof(Index), new { section = "profile" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStore(
            [Bind(Prefix = "Store")] StoreSettingsViewModel model)
        {
            var membership = await _currentStore.GetMembershipAsync();
            if (membership is null)
            {
                return RedirectToAction("Select", "Store");
            }
            if (membership.Role != StoreRole.Owner)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View("Index", await BuildPageAsync("store", store: model));
            }

            var store = await _context.Stores.FindAsync(membership.StoreId);
            if (store is null)
            {
                return NotFound();
            }

            var newCurrency = model.CurrencyCode.Trim().ToUpperInvariant();
            if (store.CurrencyCode != newCurrency &&
                (await _context.Products.AnyAsync(product => product.StoreId == store.Id) ||
                 await _context.Orders.AnyAsync(order => order.StoreId == store.Id)))
            {
                ModelState.AddModelError("Store.CurrencyCode",
                    "Không thể đổi tiền tệ khi cửa hàng đã có sản phẩm hoặc đơn hàng.");
                model.LogoPath = store.LogoPath;
                return View("Index", await BuildPageAsync("store", store: model));
            }

            var logoPath = await SaveImageAsync(model.Logo, "stores", "Store.Logo");
            if (!ModelState.IsValid)
            {
                model.LogoPath = store.LogoPath;
                return View("Index", await BuildPageAsync("store", store: model));
            }

            store.Name = model.Name.Trim();
            store.Phone = NormalizeOptional(model.Phone);
            store.Address = NormalizeOptional(model.Address);
            store.CurrencyCode = model.CurrencyCode.Trim().ToUpperInvariant();
            store.InvoiceFooter = NormalizeOptional(model.InvoiceFooter);
            store.InvoiceThankYou = model.InvoiceThankYou.Trim();
            if (logoPath is not null)
            {
                store.LogoPath = logoPath;
            }

            await _context.SaveChangesAsync();
            var user = await GetCurrentUserAsync();
            if (user is not null)
            {
                await RefreshSessionAsync(user);
            }

            TempData["Success"] = "Đã cập nhật cửa hàng và cấu hình hóa đơn.";
            return RedirectToAction(nameof(Index), new { section = "store" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePreferences(int? defaultStoreId)
        {
            var user = await GetCurrentUserAsync();
            if (user is null)
            {
                return Challenge();
            }

            if (defaultStoreId is not null)
            {
                var isMember = await _context.StoreMembers.AnyAsync(member =>
                    member.UserId == user.Id && member.StoreId == defaultStoreId);
                if (!isMember)
                {
                    return Forbid();
                }
            }

            user.DefaultStoreId = defaultStoreId;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã lưu tùy chọn sử dụng.";
            return RedirectToAction(nameof(Index), new { section = "preferences" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            [Bind(Prefix = "Security")] SecuritySettingsViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user is null)
            {
                return Challenge();
            }

            if (ModelState.IsValid &&
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    "Security.CurrentPassword",
                    "Mật khẩu hiện tại không chính xác.");
            }

            if (!ModelState.IsValid)
            {
                model.PasswordChangedAt = user.PasswordChangedAt;
                return View("Index", await BuildPageAsync("security", security: model));
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
            user.PasswordChangedAt = DateTime.Now;
            user.SecurityStamp = Guid.NewGuid().ToString();
            await _context.SaveChangesAsync();
            await RefreshSessionAsync(user);
            TempData["Success"] = "Đã đổi mật khẩu.";
            return RedirectToAction(nameof(Index), new { section = "security" });
        }

        private async Task<SettingsPageViewModel?> BuildPageAsync(
            string section,
            ProfileSettingsViewModel? profile = null,
            StoreSettingsViewModel? store = null,
            SecuritySettingsViewModel? security = null)
        {
            var user = await GetCurrentUserAsync();
            if (user is null)
            {
                return null;
            }

            var membership = await _currentStore.GetMembershipAsync();
            var isOwner = membership?.Role == StoreRole.Owner;
            if (section == "store" && !isOwner)
            {
                section = "profile";
            }

            var availableStores = await _context.StoreMembers
                .AsNoTracking()
                .Where(member => member.UserId == user.Id)
                .OrderBy(member => member.Store.Name)
                .Select(member => new SelectListItem(
                    member.Store.Name,
                    member.StoreId.ToString(),
                    user.DefaultStoreId == member.StoreId))
                .ToListAsync();

            return new SettingsPageViewModel
            {
                ActiveSection = NormalizeSection(section),
                IsOwner = isOwner,
                Profile = profile ?? new ProfileSettingsViewModel
                {
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    AvatarPath = user.AvatarPath
                },
                Store = isOwner
                    ? store ?? new StoreSettingsViewModel
                    {
                        Name = membership!.Store.Name,
                        Phone = membership.Store.Phone,
                        Address = membership.Store.Address,
                        CurrencyCode = membership.Store.CurrencyCode,
                        InvoiceFooter = membership.Store.InvoiceFooter,
                        InvoiceThankYou = membership.Store.InvoiceThankYou,
                        LogoPath = membership.Store.LogoPath
                    }
                    : null,
                Security = security ?? new SecuritySettingsViewModel
                {
                    PasswordChangedAt = user.PasswordChangedAt
                },
                DefaultStoreId = user.DefaultStoreId,
                AvailableStores = availableStores
            };
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            return _currentStore.UserId is int userId
                ? await _context.Users.FindAsync(userId)
                : null;
        }

        private async Task RefreshSessionAsync(User user)
        {
            var membership = await _context.StoreMembers
                .Include(member => member.Store)
                .SingleOrDefaultAsync(member =>
                    member.UserId == user.Id &&
                    member.StoreId == _currentStore.StoreId);
            var authentication = await HttpContext.AuthenticateAsync();
            await _userSession.SignInAsync(
                user,
                membership,
                authentication.Properties?.IsPersistent ?? false);
        }

        private async Task<string?> SaveImageAsync(
            IFormFile? file,
            string folder,
            string modelStateKey)
        {
            if (file is null || file.Length == 0)
            {
                return null;
            }

            var extension = Path.GetExtension(file.FileName);
            if (file.Length > 2 * 1024 * 1024 ||
                !AllowedImageExtensions.Contains(extension) ||
                !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    modelStateKey,
                    "Chỉ chấp nhận JPG, PNG hoặc WebP tối đa 2 MB.");
                return null;
            }

            var relativeFolder = Path.Combine("uploads", folder);
            var physicalFolder = Path.Combine(_environment.WebRootPath, relativeFolder);
            Directory.CreateDirectory(physicalFolder);
            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var physicalPath = Path.Combine(physicalFolder, fileName);
            await using var stream = System.IO.File.Create(physicalPath);
            await file.CopyToAsync(stream);
            return "/" + Path.Combine(relativeFolder, fileName).Replace('\\', '/');
        }

        private static string NormalizeSection(string section)
        {
            return section is "profile" or "store" or "appearance" or "preferences" or "security"
                ? section
                : "profile";
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
