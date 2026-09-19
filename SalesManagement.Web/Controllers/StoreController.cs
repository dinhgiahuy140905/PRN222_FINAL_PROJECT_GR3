using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers
{
    [Authorize]
    public class StoreController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserSessionService _userSession;

        public StoreController(
            ApplicationDbContext context,
            IUserSessionService userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        [HttpGet]
        public async Task<IActionResult> Select()
        {
            if (!TryGetUserId(out var userId))
            {
                return Challenge();
            }

            var stores = await _context.StoreMembers
                .AsNoTracking()
                .Where(member => member.UserId == userId)
                .OrderBy(member => member.Store.Name)
                .Select(member => new StoreListItemViewModel
                {
                    Id = member.StoreId,
                    Name = member.Store.Name,
                    Address = member.Store.Address,
                    Role = member.Role == StoreRole.Owner ? "Chủ sở hữu" : "Nhân viên"
                })
                .ToListAsync();

            var activeStoreValue = User.FindFirstValue(StoreClaimTypes.StoreId);
            int? activeStoreId = int.TryParse(activeStoreValue, out var id) ? id : null;

            return View(new StoreSelectionViewModel
            {
                ActiveStoreId = activeStoreId,
                Stores = stores
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Switch(int storeId)
        {
            if (!TryGetUserId(out var userId))
            {
                return Challenge();
            }

            var membership = await _context.StoreMembers
                .Include(member => member.Store)
                .SingleOrDefaultAsync(member => member.UserId == userId && member.StoreId == storeId);

            if (membership is null)
            {
                return Forbid();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user is null)
            {
                return Challenge();
            }

            var authentication = await HttpContext.AuthenticateAsync();
            await _userSession.SignInAsync(
                user,
                membership,
                authentication.Properties?.IsPersistent ?? false);

            return RedirectToAction("Dashboard", "Home");
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateStoreViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateStoreViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!TryGetUserId(out var userId))
            {
                return Challenge();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user is null)
            {
                return Challenge();
            }

            var store = new Store
            {
                Name = model.Name.Trim(),
                Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
                Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim()
            };

            var membership = new StoreMember
            {
                User = user,
                Store = store,
                Role = StoreRole.Owner
            };

            _context.StoreMembers.Add(membership);
            await _context.SaveChangesAsync();

            var authentication = await HttpContext.AuthenticateAsync();
            await _userSession.SignInAsync(
                user,
                membership,
                authentication.Properties?.IsPersistent ?? false);

            return RedirectToAction("Dashboard", "Home");
        }

        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }
    }
}
