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
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var monthOrders = _context.Orders
                .AsNoTracking()
                .Where(order =>
                    order.StoreId == storeId &&
                    order.CreatedAt >= monthStart &&
                    order.Status != OrderStatus.Cancelled);

            var model = new DashboardViewModel
            {
                IsOwner = membership.Role == StoreRole.Owner,
                CurrencyCode = membership.Store.CurrencyCode,
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
        product.Inventory.Quantity > 0 &&
        product.Inventory.Quantity <= product.Inventory.ReorderLevel),
                OrderCount = await monthOrders.CountAsync(),
                Revenue = (await monthOrders
                    .Where(order => order.Status == OrderStatus.Completed && order.CurrencyCode == membership.Store.CurrencyCode)
                    .Select(order => order.Total).ToListAsync()).Sum(),
                CustomerCount = await _context.Orders
                    .AsNoTracking()
                    .Where(order =>
                        order.StoreId == storeId &&
                        order.Status != OrderStatus.Cancelled &&
                        order.CustomerPhone != null &&
                        order.CustomerPhone != "")
                    .Select(order => order.CustomerPhone)
                    .Distinct()
                    .CountAsync(),
                RecentOrders = await _context.Orders
                    .AsNoTracking()
                    .Where(order => order.StoreId == storeId)
                    .OrderByDescending(order => order.CreatedAt)
                    .Take(5)
                    .Select(order => new DashboardOrderViewModel
                    {
                        Id = order.Id,
                        Code = order.Code,
                        CustomerName = order.CustomerName,
                        Status = order.Status,
                        PaymentStatus = order.PaymentStatus,
                        Total = order.Total,
                        CurrencyCode = order.CurrencyCode,
                        CreatedAt = order.CreatedAt
                    })
                    .ToListAsync(),
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

        [Authorize]
        public async Task<IActionResult> Help()
        {
            var member = await _currentStore.GetMembershipAsync();
            ViewData["IsOwner"] = member?.Role == StoreRole.Owner;
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
