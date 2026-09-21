using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers
{
    [Authorize]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreContext _currentStore;

        public StaffController(ApplicationDbContext context, ICurrentStoreContext currentStore)
        {
            _context = context;
            _currentStore = currentStore;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var membership = await GetOwnerMembershipAsync();
            if (membership is null)
            {
                return Forbid();
            }

            return View(await BuildViewModelAsync(membership));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Invite([Bind(Prefix = "Invite")] StaffInviteViewModel model)
        {
            var membership = await GetOwnerMembershipAsync();
            if (membership is null)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View("Index", await BuildViewModelAsync(membership, model));
            }

            var email = model.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.SingleOrDefaultAsync(item => item.Email == email);
            if (user is null)
            {
                ModelState.AddModelError("Invite.Email", "Chưa có tài khoản SalesHub với email này. Nhân viên cần đăng ký tài khoản Staff trước.");
                return View("Index", await BuildViewModelAsync(membership, model));
            }

            var alreadyMember = await _context.StoreMembers.AnyAsync(item =>
                item.StoreId == membership.StoreId && item.UserId == user.Id);
            if (alreadyMember)
            {
                ModelState.AddModelError("Invite.Email", "Tài khoản này đã là thành viên của cửa hàng.");
                return View("Index", await BuildViewModelAsync(membership, model));
            }

            _context.StoreMembers.Add(new StoreMember
            {
                StoreId = membership.StoreId,
                UserId = user.Id,
                Role = StoreRole.Staff
            });

            if (user.DefaultStoreId is null)
            {
                user.DefaultStoreId = membership.StoreId;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("Invite.Email", "Không thể thêm thành viên. Vui lòng tải lại trang và thử lại.");
                return View("Index", await BuildViewModelAsync(membership, model));
            }

            TempData["Success"] = $"Đã thêm {user.FullName} vào {membership.Store.Name}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id)
        {
            var ownerMembership = await GetOwnerMembershipAsync();
            if (ownerMembership is null)
            {
                return Forbid();
            }

            var member = await _context.StoreMembers
                .Include(item => item.User)
                .SingleOrDefaultAsync(item =>
                    item.Id == id &&
                    item.StoreId == ownerMembership.StoreId &&
                    item.Role == StoreRole.Staff);
            if (member is null)
            {
                return NotFound();
            }

            _context.StoreMembers.Remove(member);
            if (member.User.DefaultStoreId == ownerMembership.StoreId)
            {
                member.User.DefaultStoreId = await _context.StoreMembers
                    .Where(item => item.UserId == member.UserId && item.Id != member.Id)
                    .OrderByDescending(item => item.Role == StoreRole.Owner)
                    .Select(item => (int?)item.StoreId)
                    .FirstOrDefaultAsync();
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã gỡ {member.User.FullName} khỏi cửa hàng.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<StoreMember?> GetOwnerMembershipAsync()
        {
            var membership = await _currentStore.GetMembershipAsync();
            return membership?.Role == StoreRole.Owner ? membership : null;
        }

        private async Task<StaffManagementViewModel> BuildViewModelAsync(
            StoreMember membership,
            StaffInviteViewModel? invite = null)
        {
            return new StaffManagementViewModel
            {
                StoreName = membership.Store.Name,
                Invite = invite ?? new StaffInviteViewModel(),
                Members = await _context.StoreMembers
                    .AsNoTracking()
                    .Where(item => item.StoreId == membership.StoreId)
                    .OrderByDescending(item => item.Role == StoreRole.Owner)
                    .ThenBy(item => item.User.FullName)
                    .Select(item => new StoreMemberListItemViewModel
                    {
                        MembershipId = item.Id,
                        FullName = item.User.FullName,
                        Email = item.User.Email,
                        AvatarPath = item.User.AvatarPath,
                        Role = item.Role
                    })
                    .ToListAsync()
            };
        }
    }
}
