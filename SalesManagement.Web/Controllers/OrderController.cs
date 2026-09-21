using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentStoreContext _currentStore;

        public OrderController(
            ApplicationDbContext context,
            ICurrentStoreContext currentStore)
        {
            _context = context;
            _currentStore = currentStore;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, OrderStatus? status)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var query = _context.Orders
                .AsNoTracking()
                .Include(order => order.Items)
                .Where(order => order.StoreId == storeId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(order =>
                    order.Code.Contains(term) ||
                    order.CustomerName.Contains(term) ||
                    (order.CustomerPhone != null && order.CustomerPhone.Contains(term)));
            }

            if (status is not null)
            {
                query = query.Where(order => order.Status == status);
            }

            return View(new OrderIndexViewModel
            {
                Search = search,
                Status = status,
                Orders = await query
                    .OrderByDescending(order => order.CreatedAt)
                    .ToListAsync()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var model = new CreateOrderViewModel();
            await LoadProductsAsync(model, storeId.Value);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOrderViewModel model)
        {
            var membership = await _currentStore.GetMembershipAsync();
            if (membership is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var storeId = membership.StoreId;
            model.Items ??= new List<CreateOrderItemViewModel>();
            var validInputs = model.Items
                .Where(item => item.ProductId > 0 && item.Quantity > 0)
                .ToList();

            if (validInputs.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Items), "Vui lòng thêm ít nhất một sản phẩm.");
            }

            if (validInputs.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
            {
                ModelState.AddModelError(nameof(model.Items), "Mỗi sản phẩm chỉ nên xuất hiện một lần trong đơn.");
            }

            var productIds = validInputs.Select(item => item.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Include(product => product.Inventory)
                .Where(product =>
                    product.StoreId == storeId &&
                    product.IsActive &&
                    productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id);

            if (products.Count != productIds.Count)
            {
                ModelState.AddModelError(nameof(model.Items), "Có sản phẩm không hợp lệ hoặc không còn kinh doanh.");
            }

            foreach (var input in validInputs)
            {
                if (products.TryGetValue(input.ProductId, out var product) &&
                    product.Inventory.Quantity < input.Quantity)
                {
                    ModelState.AddModelError(
                        nameof(model.Items),
                        $"{product.Name} chỉ còn {product.Inventory.Quantity} {product.Unit}.");
                }
            }

            var subtotal = validInputs
                .Where(input => products.ContainsKey(input.ProductId))
                .Sum(input => products[input.ProductId].SalePrice * input.Quantity);

            if (model.Discount > subtotal)
            {
                ModelState.AddModelError(nameof(model.Discount), "Giảm giá không thể lớn hơn tiền hàng.");
            }

            if (!ModelState.IsValid)
            {
                await LoadProductsAsync(model, storeId);
                return View(model);
            }

            var order = new Order
            {
                StoreId = storeId,
                CreatedByUserId = _currentStore.UserId,
                Code = GenerateOrderCode(),
                CustomerName = string.IsNullOrWhiteSpace(model.CustomerName)
                    ? "Khách lẻ"
                    : model.CustomerName.Trim(),
                CustomerPhone = NormalizeOptional(model.CustomerPhone),
                Status = OrderStatus.Confirmed,
                PaymentStatus = model.IsPaid ? PaymentStatus.Paid : PaymentStatus.Unpaid,
                PaymentMethod = model.PaymentMethod,
                Subtotal = subtotal,
                Discount = model.Discount,
                Total = subtotal - model.Discount,
                Note = NormalizeOptional(model.Note)
            };

            await using var databaseTransaction = await _context.Database.BeginTransactionAsync();
            foreach (var input in validInputs)
            {
                var product = products[input.ProductId];
                var lineTotal = product.SalePrice * input.Quantity;
                product.Inventory.Quantity -= input.Quantity;

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Quantity = input.Quantity,
                    UnitPrice = product.SalePrice,
                    CostPrice = product.CostPrice,
                    LineTotal = lineTotal
                });
                order.InventoryTransactions.Add(new InventoryTransaction
                {
                    StoreId = storeId,
                    ProductId = product.Id,
                    UserId = _currentStore.UserId,
                    Type = InventoryTransactionType.Sale,
                    QuantityChange = -input.Quantity,
                    QuantityAfter = product.Inventory.Quantity,
                    Note = $"Xuất kho cho đơn {order.Code}"
                });
            }

            _context.Orders.Add(order);
            try
            {
                await _context.SaveChangesAsync();
                await databaseTransaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await databaseTransaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Tồn kho vừa thay đổi. Vui lòng kiểm tra lại số lượng.");
                await LoadProductsAsync(model, storeId);
                return View(model);
            }

            TempData["Success"] = $"Đã tạo đơn hàng {order.Code}.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var order = await _context.Orders
                .AsNoTracking()
                .Include(item => item.Items)
                .Include(item => item.CreatedByUser)
                .SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId);

            return order is null ? NotFound() : View(order);
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Invoice(int id, bool print = false)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var order = await _context.Orders
                .AsNoTracking()
                .Include(item => item.Items)
                .Include(item => item.Store)
                .Include(item => item.CreatedByUser)
                .SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId);

            if (order is null)
            {
                return NotFound();
            }

            ViewData["AutoPrint"] = print;
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var order = await _context.Orders
                .SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId);
            if (order is null)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Confirmed)
            {
                TempData["Error"] = "Chỉ đơn đang xử lý mới có thể hoàn thành.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = OrderStatus.Completed;
            order.CompletedAt = DateTime.Now;
            order.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đơn {order.Code} đã hoàn thành.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var storeId = await GetStoreIdAsync();
            if (storeId is null)
            {
                return RedirectToAction("Select", "Store");
            }

            var order = await _context.Orders
                .Include(item => item.Items)
                    .ThenInclude(item => item.Product)
                        .ThenInclude(product => product.Inventory)
                .SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId);
            if (order is null)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Confirmed)
            {
                TempData["Error"] = "Chỉ đơn đang xử lý mới có thể hủy.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await using var databaseTransaction = await _context.Database.BeginTransactionAsync();
            foreach (var item in order.Items)
            {
                item.Product.Inventory.Quantity += item.Quantity;
                order.InventoryTransactions.Add(new InventoryTransaction
                {
                    StoreId = storeId.Value,
                    ProductId = item.ProductId,
                    UserId = _currentStore.UserId,
                    Type = InventoryTransactionType.Return,
                    QuantityChange = item.Quantity,
                    QuantityAfter = item.Product.Inventory.Quantity,
                    Note = $"Hoàn kho do hủy đơn {order.Code}"
                });
            }

            order.Status = OrderStatus.Cancelled;
            order.PaymentStatus = order.PaymentStatus == PaymentStatus.Paid
                ? PaymentStatus.Refunded
                : PaymentStatus.Unpaid;
            order.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
                await databaseTransaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await databaseTransaction.RollbackAsync();
                TempData["Error"] = "Tồn kho vừa thay đổi. Vui lòng thử hủy đơn lại.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] = $"Đã hủy đơn {order.Code} và hoàn lại tồn kho.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<int?> GetStoreIdAsync()
        {
            return (await _currentStore.GetMembershipAsync())?.StoreId;
        }

        private async Task LoadProductsAsync(CreateOrderViewModel model, int storeId)
        {
            model.Products = await _context.Products
                .AsNoTracking()
                .Where(product => product.StoreId == storeId && product.IsActive)
                .OrderBy(product => product.Name)
                .Select(product => new OrderProductOptionViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    Sku = product.Sku,
                    SalePrice = product.SalePrice,
                    Stock = product.Inventory.Quantity
                })
                .ToListAsync();
        }

        private static string GenerateOrderCode()
        {
            return $"DH-{DateTime.Now:yyMMdd}-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
