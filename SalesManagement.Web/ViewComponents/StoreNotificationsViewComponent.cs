using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.ViewComponents;

public sealed class StoreNotificationsViewComponent(ApplicationDbContext db, ICurrentStoreContext currentStore) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new StoreNotificationsViewModel();
        var member = await currentStore.GetMembershipAsync();
        if (member is null) return View(model);
        model.HasStore = true;
        var stocks = await db.Products.AsNoTracking().Where(p => p.StoreId == member.StoreId && p.IsActive)
            .GroupBy(p => 1).Select(group => new
            {
                Empty = group.Count(p => p.Inventory.Quantity <= 0),
                Low = group.Count(p => p.Inventory.Quantity > 0 && p.Inventory.Quantity <= p.Inventory.ReorderLevel)
            }).SingleOrDefaultAsync();
        var orders = await db.Orders.AsNoTracking().Where(o => o.StoreId == member.StoreId)
            .GroupBy(o => 1).Select(group => new
            {
                Processing = group.Count(o => o.Status == OrderStatus.Confirmed),
                Unpaid = group.Count(o => o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Unpaid)
            }).SingleOrDefaultAsync();
        if (stocks?.Empty > 0) model.Items.Add(new("Sản phẩm hết hàng", stocks.Empty, "Product", "Index", "out-of-stock"));
        if (stocks?.Low > 0) model.Items.Add(new("Sản phẩm sắp hết hàng", stocks.Low, "Product", "Index", "low-stock"));
        if (orders?.Processing > 0) model.Items.Add(new("Đơn đang xử lý", orders.Processing, "Order", "Index", "Confirmed"));
        if (orders?.Unpaid > 0) model.Items.Add(new("Đơn hoàn tất chưa thanh toán", orders.Unpaid, "Order", "Index", "Completed", "Unpaid"));
        return View(model);
    }
}
