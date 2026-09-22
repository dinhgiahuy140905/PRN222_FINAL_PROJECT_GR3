using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Web.Controllers;

[Authorize]
public class CustomerController(ApplicationDbContext db, ICurrentStoreContext currentStore) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        var member = await currentStore.GetMembershipAsync();
        if (member is null) return RedirectToAction("Select", "Store");
        var query = db.Customers.AsNoTracking().Where(x => x.StoreId == member.StoreId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || (x.Phone != null && x.Phone.Contains(term)));
        }
        ViewData["Search"] = search;
        return View(await query.OrderBy(x => x.Name).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create() => await currentStore.GetMembershipAsync() is null
        ? RedirectToAction("Select", "Store") : View("Edit", new Customer());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer model)
    {
        var member = await currentStore.GetMembershipAsync();
        if (member is null) return RedirectToAction("Select", "Store");
        ModelState.Remove(nameof(Customer.Store));
        ModelState.Remove(nameof(Customer.Orders));
        if (!ModelState.IsValid) return View("Edit", model);
        db.Customers.Add(new Customer { StoreId = member.StoreId, Name = model.Name.Trim(), Phone = Trim(model.Phone), Email = Trim(model.Email), Note = Trim(model.Note) });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var member = await currentStore.GetMembershipAsync();
        if (member is null) return RedirectToAction("Select", "Store");
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.StoreId == member.StoreId);
        return customer is null ? NotFound() : View(customer);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer model)
    {
        var member = await currentStore.GetMembershipAsync();
        if (member is null) return RedirectToAction("Select", "Store");
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.StoreId == member.StoreId);
        if (customer is null) return NotFound();
        ModelState.Remove(nameof(Customer.Store));
        ModelState.Remove(nameof(Customer.Orders));
        if (!ModelState.IsValid) return View(model);
        customer.Name = model.Name.Trim();
        customer.Phone = Trim(model.Phone);
        customer.Email = Trim(model.Email);
        customer.Note = Trim(model.Note);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
