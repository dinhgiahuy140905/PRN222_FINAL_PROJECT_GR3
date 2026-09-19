using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers
{
    [Authorize]
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreContext _currentStore;

        public CategoryController(ApplicationDbContext context, ICurrentStoreContext currentStore)
        {
            _context = context;
            _currentStore = currentStore;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            ViewData["Categories"] = await _context.Categories
                .AsNoTracking()
                .Where(category => category.StoreId == storeId)
                .Include(category => category.Products)
                .OrderBy(category => category.Name)
                .ToListAsync();
            return View(new CategoryFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryFormViewModel model)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var normalizedName = model.Name?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedName) &&
                await _context.Categories.AnyAsync(category =>
                category.StoreId == storeId && category.Name == normalizedName))
            {
                ModelState.AddModelError(nameof(model.Name), "Danh mục này đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                ViewData["Categories"] = await _context.Categories
                    .AsNoTracking()
                    .Where(category => category.StoreId == storeId)
                    .Include(category => category.Products)
                    .OrderBy(category => category.Name)
                    .ToListAsync();
                return View("Index", model);
            }

            _context.Categories.Add(new Category
            {
                StoreId = storeId.Value,
                Name = normalizedName
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm danh mục.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var category = await _context.Categories
                .Include(item => item.Products)
                .SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId);
            if (category is null)
            {
                return NotFound();
            }

            if (category.Products.Count > 0)
            {
                TempData["Error"] = "Không thể xóa danh mục đang có sản phẩm.";
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa danh mục.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<int?> GetStoreIdAsync()
        {
            return (await _currentStore.GetMembershipAsync())?.StoreId;
        }
    }
}
