using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ICurrentStoreContext _currentStore;
        private readonly ApplicationDbContext _context;

        public HomeController(
            ICurrentStoreContext currentStore,
            ApplicationDbContext context)
        {
            _currentStore = currentStore;
            _context = context;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var membership = await _currentStore.GetMembershipAsync();
            if (membership is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var storeId = membership.StoreId;
            var model = new DashboardViewModel
            {
                StoreName = membership.Store.Name,
                ProductCount = await _context.Products
                    .CountAsync(product => product.StoreId == storeId && product.IsActive),
                TotalStock = await _context.InventoryItems
                    .Where(item => item.StoreId == storeId)
                    .SumAsync(item => (int?)item.Quantity) ?? 0,
                LowStockCount = await _context.Products
                    .CountAsync(product =>
                        product.StoreId == storeId &&
                        product.IsActive &&
                        product.Inventory.Quantity <= product.Inventory.ReorderLevel),
                RecentInventoryActivities = await _context.InventoryTransactions
                    .AsNoTracking()
                    .Where(transaction => transaction.StoreId == storeId)
                    .OrderByDescending(transaction => transaction.CreatedAt)
                    .Take(5)
                    .Select(transaction => new InventoryActivityViewModel
                    {
                        ProductName = transaction.Product.Name,
                        Sku = transaction.Product.Sku,
                        QuantityChange = transaction.QuantityChange,
                        QuantityAfter = transaction.QuantityAfter,
                        Note = transaction.Note,
                        CreatedAt = transaction.CreatedAt
                    })
                    .ToListAsync()
            };

            return View(model);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
