using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Tests;

public sealed partial class SalesHubFlowTests
{
    [Fact]
    public async Task Settings_password_change_invalidates_an_existing_recovery_link()
    {
        using var app = new SalesHubFactory();
        app.Seed();
        string token;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = db.Users.Single(user => user.Email == "owner@test.local");
            token = scope.ServiceProvider.GetRequiredService<PasswordResetService>().CreateToken(user);
        }
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        var changed = await Post(client, "/Settings/ChangePassword", "/Settings/Index?section=security", new()
        {
            ["Security.CurrentPassword"] = "Password123!",
            ["Security.NewPassword"] = "NewPassword123!",
            ["Security.ConfirmPassword"] = "NewPassword123!"
        });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        var hash = ReadDb(app, db => db.Users.Single(user => user.Email == "owner@test.local").PasswordHash);

        using var anonymous = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        var reset = await Post(anonymous, "/Account/ResetPassword", "/Account/ResetPassword", new()
        { ["Token"] = token, ["Password"] = "Recovered123!", ["ConfirmPassword"] = "Recovered123!" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Contains("đã được sử dụng", WebUtility.HtmlDecode(await reset.Content.ReadAsStringAsync()));
        Assert.Equal(hash, ReadDb(app, db => db.Users.Single(user => user.Email == "owner@test.local").PasswordHash));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Settings/Index")).StatusCode);
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(1, true, true)]
    [InlineData(3, true, true)]
    [InlineData(4, true, false)]
    [InlineData(1, false, false)]
    public async Task Dashboard_low_stock_count_matches_the_product_filter(int quantity, bool active, bool expected)
    {
        using var app = new SalesHubFactory();
        var (_, _, storeA, storeB, productId) = app.Seed();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = db.Products.Include(product => product.Inventory).Single(product => product.Id == productId);
            product.Sku = "MERGE-CANDIDATE";
            product.IsActive = active;
            product.Inventory.Quantity = quantity;
            product.Inventory.ReorderLevel = 3;
            // Keep the dashboard populated and ensure another store's low stock is excluded.
            db.Products.AddRange(
                new Product { StoreId = storeA, Name = "Healthy", Sku = "HEALTHY",
                    Inventory = new InventoryItem { StoreId = storeA, Quantity = 10, ReorderLevel = 1 } },
                new Product { StoreId = storeB, Name = "Other store", Sku = "OTHER-STORE",
                    Inventory = new InventoryItem { StoreId = storeB, Quantity = 1, ReorderLevel = 3 } });
            db.SaveChanges();
        }
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        var dashboard = WebUtility.HtmlDecode(await client.GetStringAsync("/Home/Dashboard"));
        Assert.Contains($"{(expected ? 1 : 0)} sản phẩm cần chú ý", dashboard);
        var filtered = await client.GetStringAsync("/Product/Index?status=low-stock");
        Assert.Equal(expected, filtered.Contains("MERGE-CANDIDATE"));
        Assert.DoesNotContain("OTHER-STORE", filtered);
    }
}
