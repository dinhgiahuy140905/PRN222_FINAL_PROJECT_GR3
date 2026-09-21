using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreContext _currentStore;

        public ProductController(
            ApplicationDbContext context,
            ICurrentStoreContext currentStore)
        {
            _context = context;
            _currentStore = currentStore;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            int? categoryId,
            string status = "all",
            int page = 1,
            bool lowStockOnly = false)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            const int pageSize = 20;
            page = Math.Max(1, page);
            status = lowStockOnly
                ? "low-stock"
                : (status ?? "all").Trim().ToLowerInvariant();
            if (status is not ("all" or "in-stock" or "low-stock" or "out-of-stock" or "inactive"))
            {
                status = "all";
            }

            var storeProducts = _context.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .Include(product => product.Inventory)
                .Where(product => product.StoreId == storeId);

            var model = new ProductIndexViewModel
            {
                Search = search,
                CategoryId = categoryId,
                Status = status,
                Page = page,
                PageSize = pageSize,
                TotalProducts = await storeProducts.CountAsync(),
                LowStockProducts = await storeProducts.CountAsync(product =>
                    product.IsActive &&
                    product.Inventory.Quantity > 0 &&
                    product.Inventory.Quantity <= product.Inventory.ReorderLevel),
                OutOfStockProducts = await storeProducts.CountAsync(product =>
                    product.IsActive && product.Inventory.Quantity <= 0),
                InactiveProducts = await storeProducts.CountAsync(product => !product.IsActive),
                Categories = await _context.Categories
                    .AsNoTracking()
                    .Where(category => category.StoreId == storeId)
                    .OrderBy(category => category.Name)
                    .Select(category => new SelectListItem(category.Name, category.Id.ToString()))
                    .ToListAsync()
            };

            var query = storeProducts.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(product =>
                    product.Name.Contains(term) ||
                    product.Sku.Contains(term) ||
                    (product.Barcode != null && product.Barcode.Contains(term)));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(product => product.CategoryId == categoryId.Value);
            }

            query = status switch
            {
                "in-stock" => query.Where(product =>
                    product.IsActive && product.Inventory.Quantity > product.Inventory.ReorderLevel),
                "low-stock" => query.Where(product =>
                    product.IsActive &&
                    product.Inventory.Quantity > 0 &&
                    product.Inventory.Quantity <= product.Inventory.ReorderLevel),
                "out-of-stock" => query.Where(product =>
                    product.IsActive && product.Inventory.Quantity <= 0),
                "inactive" => query.Where(product => !product.IsActive),
                _ => query
            };

            model.FilteredCount = await query.CountAsync();
            model.Page = Math.Min(page, model.PageCount);
            model.Products = await query
                .OrderByDescending(product => product.IsActive)
                .ThenBy(product => product.Name)
                .Skip((model.Page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var model = new ProductFormViewModel();
            await LoadCategoriesAsync(model, storeId.Value);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductFormViewModel model)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            await ValidateProductAsync(model, storeId.Value);
            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model, storeId.Value);
                return View(model);
            }

            var product = new Product
            {
                StoreId = storeId.Value,
                CategoryId = model.CategoryId,
                Name = model.Name.Trim(),
                Sku = model.Sku.Trim().ToUpperInvariant(),
                Barcode = NormalizeOptional(model.Barcode),
                Unit = model.Unit.Trim(),
                CostPrice = model.CostPrice,
                SalePrice = model.SalePrice,
                Inventory = new InventoryItem
                {
                    StoreId = storeId.Value,
                    Quantity = model.OpeningStock,
                    ReorderLevel = model.ReorderLevel
                }
            };

            if (model.OpeningStock > 0)
            {
                product.InventoryTransactions.Add(new InventoryTransaction
                {
                    StoreId = storeId.Value,
                    UserId = _currentStore.UserId,
                    Type = InventoryTransactionType.OpeningStock,
                    QuantityChange = model.OpeningStock,
                    QuantityAfter = model.OpeningStock,
                    Note = "Tồn kho ban đầu"
                });
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm sản phẩm mới.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var product = await _context.Products
                .Include(item => item.Inventory)
                .SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId);
            if (product is null)
            {
                return NotFound();
            }

            var model = new ProductFormViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                Unit = product.Unit,
                CostPrice = product.CostPrice,
                SalePrice = product.SalePrice,
                CategoryId = product.CategoryId,
                OpeningStock = product.Inventory.Quantity,
                ReorderLevel = product.Inventory.ReorderLevel
            };
            await LoadCategoriesAsync(model, storeId.Value);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductFormViewModel model)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            if (model.Id != id)
            {
                return BadRequest();
            }

            await ValidateProductAsync(model, storeId.Value, id);
            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model, storeId.Value);
                return View(model);
            }

            var product = await _context.Products
                .Include(item => item.Inventory)
                .SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId);
            if (product is null)
            {
                return NotFound();
            }

            product.Name = model.Name.Trim();
            product.Sku = model.Sku.Trim().ToUpperInvariant();
            product.Barcode = NormalizeOptional(model.Barcode);
            product.Unit = model.Unit.Trim();
            product.CostPrice = model.CostPrice;
            product.SalePrice = model.SalePrice;
            product.CategoryId = model.CategoryId;
            product.Inventory.ReorderLevel = model.ReorderLevel;
            product.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> AdjustStock(int id)
        {
            var model = await BuildStockAdjustmentAsync(id);
            return model is null ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustStock(StockAdjustmentViewModel model)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var product = await _context.Products
                .Include(item => item.Inventory)
                .SingleOrDefaultAsync(item =>
                    item.Id == model.ProductId &&
                    item.StoreId == storeId &&
                    item.IsActive);
            if (product is null)
            {
                return NotFound();
            }

            model.ProductName = product.Name;
            model.Sku = product.Sku;
            model.CurrentQuantity = product.Inventory.Quantity;

            if (model.QuantityChange == 0)
            {
                ModelState.AddModelError(nameof(model.QuantityChange), "Số lượng thay đổi phải khác 0.");
            }

            var quantityAfter = product.Inventory.Quantity + model.QuantityChange;
            if (quantityAfter < 0)
            {
                ModelState.AddModelError(nameof(model.QuantityChange), "Số lượng xuất vượt quá tồn kho hiện tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            product.Inventory.Quantity = quantityAfter;
            product.InventoryTransactions.Add(new InventoryTransaction
            {
                StoreId = storeId.Value,
                UserId = _currentStore.UserId,
                Type = InventoryTransactionType.Adjustment,
                QuantityChange = model.QuantityChange,
                QuantityAfter = quantityAfter,
                Note = NormalizeOptional(model.Note)
            });

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Tồn kho vừa được cập nhật bởi người khác. Vui lòng thử lại.");
                var refreshed = await BuildStockAdjustmentAsync(model.ProductId);
                if (refreshed is not null)
                {
                    model.CurrentQuantity = refreshed.CurrentQuantity;
                }
                return View(model);
            }

            TempData["Success"] = "Đã cập nhật tồn kho.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var product = await _context.Products
                .SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId);
            if (product is null)
            {
                return NotFound();
            }

            product.IsActive = false;
            product.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã ngừng kinh doanh sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var product = await _context.Products
                .SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId);
            if (product is null)
            {
                return NotFound();
            }

            product.IsActive = true;
            product.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã mở bán lại sản phẩm.";
            return RedirectToAction(nameof(Index), new { status = "inactive" });
        }

        private async Task<int?> GetStoreIdAsync()
        {
            return (await _currentStore.GetMembershipAsync())?.StoreId;
        }

        private async Task LoadCategoriesAsync(ProductFormViewModel model, int storeId)
        {
            model.Categories = await _context.Categories
                .AsNoTracking()
                .Where(category => category.StoreId == storeId)
                .OrderBy(category => category.Name)
                .Select(category => new SelectListItem(category.Name, category.Id.ToString()))
                .ToListAsync();
        }

        private async Task ValidateProductAsync(ProductFormViewModel model, int storeId, int? ignoredId = null)
        {
            if (string.IsNullOrWhiteSpace(model.Sku))
            {
                return;
            }

            var normalizedSku = model.Sku.Trim().ToUpperInvariant();
            if (await _context.Products.AnyAsync(product =>
                product.StoreId == storeId &&
                product.Sku == normalizedSku &&
                product.Id != ignoredId))
            {
                ModelState.AddModelError(nameof(model.Sku), "SKU này đã tồn tại trong cửa hàng.");
            }

            if (model.CategoryId is int categoryId &&
                !await _context.Categories.AnyAsync(category =>
                    category.Id == categoryId && category.StoreId == storeId))
            {
                ModelState.AddModelError(nameof(model.CategoryId), "Danh mục không hợp lệ.");
            }
        }

        private async Task<StockAdjustmentViewModel?> BuildStockAdjustmentAsync(int productId)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return null;
            }

            return await _context.Products
                .AsNoTracking()
                .Where(product =>
                    product.Id == productId &&
                    product.StoreId == storeId &&
                    product.IsActive)
                .Select(product => new StockAdjustmentViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    CurrentQuantity = product.Inventory.Quantity
                })
                .SingleOrDefaultAsync();
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
