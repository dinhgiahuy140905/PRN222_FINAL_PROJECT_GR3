using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers;

[Authorize]
public class ReportController(ApplicationDbContext db, ICurrentStoreContext currentStore) : Controller
{
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var member = await currentStore.GetMembershipAsync();
        if (member is null) return RedirectToAction("Select", "Store");
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            ModelState.AddModelError(string.Empty, "Ngày bắt đầu không thể sau ngày kết thúc.");

        var orders = db.Orders.AsNoTracking().Where(x => x.StoreId == member.StoreId);
        if (from.HasValue) orders = orders.Where(x => x.CreatedAt >= from.Value.Date);
        if (to.HasValue) orders = orders.Where(x => x.CreatedAt < to.Value.Date.AddDays(1));
        var model = new ReportViewModel { From = from, To = to };
        if (!ModelState.IsValid) return View(model);
        var rows = await orders.Select(x => new { x.Id, x.Status, x.PaymentStatus, x.Total }).ToListAsync();
        model.CompletedSales = rows.Where(x => x.Status == OrderStatus.Completed).Sum(x => x.Total);
        model.RecordedPayments = rows.Where(x => x.PaymentStatus == PaymentStatus.Paid).Sum(x => x.Total);
        model.ConfirmedCount = rows.Count(x => x.Status == OrderStatus.Confirmed);
        model.CompletedCount = rows.Count(x => x.Status == OrderStatus.Completed);
        model.CancelledCount = rows.Count(x => x.Status == OrderStatus.Cancelled);
        var completedIds = rows.Where(x => x.Status == OrderStatus.Completed).Select(x => x.Id).ToList();
        var items = await db.OrderItems.AsNoTracking()
            .Where(x => completedIds.Contains(x.OrderId))
            .Select(x => new { x.ProductId, x.ProductName, x.Quantity, x.LineTotal })
            .ToListAsync();
        model.TopProducts = items.GroupBy(x => new { x.ProductId, x.ProductName })
            .Select(group => new TopProductViewModel
            {
                Name = group.Key.ProductName,
                Quantity = group.Sum(x => x.Quantity),
                Sales = group.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.Quantity).Take(10).ToList();
        return View(model);
    }
}
