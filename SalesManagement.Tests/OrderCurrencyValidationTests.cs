using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;

namespace SalesManagement.Tests;

public sealed partial class SalesHubFlowTests
{
    [Theory]
    [InlineData("VND", "100 ₫")]
    [InlineData("USD", "12,50 USD")]
    [InlineData("EUR", "12,50 EUR")]
    public async Task Orders_snapshot_currency_and_render_correct_decimals(string currency, string expected)
    {
        using var app = new SalesHubFactory();
        var (_, _, storeA, storeB, productId) = app.Seed();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Stores.Single(x => x.Id == storeA).CurrencyCode = currency;
            db.Products.Single(x => x.Id == productId).SalePrice = currency == "VND" ? 100 : 12.50m;
            db.SaveChanges();
        }
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        var created = await Post(client, "/Order/Create", "/Order/Create", new()
        {
            ["Items[0].ProductId"] = productId.ToString(), ["Items[0].Quantity"] = "1",
            ["CurrencyCode"] = "ATTACKER"
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var orderId = ReadDb(app, db => db.Orders.Single().Id);
        Assert.Equal(currency, ReadDb(app, db => db.Orders.Single().CurrencyCode));
        Assert.Contains(expected, WebUtility.HtmlDecode(await client.GetStringAsync("/Product/Index")));
        Assert.Contains(currency == "VND" ? "₫" : currency,
            WebUtility.HtmlDecode(await client.GetStringAsync("/Product/Create")));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Stores.Single(x => x.Id == storeA).CurrencyCode = currency == "USD" ? "VND" : "USD";
            db.SaveChanges();
        }
        foreach (var url in new[] { $"/Order/Invoice/{orderId}", $"/Order/Details/{orderId}", "/Order/Index", "/Home/Dashboard" })
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync(url));
            Assert.Contains(expected, html);
        }
        await Post(client, "/Store/Switch", "/Store/Select", new() { ["storeId"] = storeB.ToString() });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Order/Invoice/{orderId}")).StatusCode);
    }

    [Fact]
    public async Task Currency_changes_are_blocked_with_data_and_allowed_for_empty_stores()
    {
        using var app = new SalesHubFactory();
        var (_, _, storeA, storeB, _) = app.Seed();
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        var fields = new Dictionary<string, string>
        { ["Store.Name"] = "Store", ["Store.CurrencyCode"] = "USD", ["Store.InvoiceThankYou"] = "Thanks" };
        var blocked = await Post(client, "/Settings/UpdateStore", "/Settings/Index?section=store", fields);
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        Assert.Contains("Không thể đổi tiền tệ", WebUtility.HtmlDecode(await blocked.Content.ReadAsStringAsync()));
        Assert.Equal("VND", ReadDb(app, db => db.Stores.Single(x => x.Id == storeA).CurrencyCode));
        await Post(client, "/Store/Switch", "/Store/Select", new() { ["storeId"] = storeB.ToString() });
        Assert.Equal(HttpStatusCode.Redirect,
            (await Post(client, "/Settings/UpdateStore", "/Settings/Index?section=store", fields)).StatusCode);
        Assert.Equal("USD", ReadDb(app, db => db.Stores.Single(x => x.Id == storeB).CurrencyCode));
        Assert.Equal("VND", ReadDb(app, db => db.Stores.Single(x => x.Id == storeA).CurrencyCode));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Orders.Add(new Order { StoreId = storeB, CurrencyCode = "USD", Code = "ORDER-ONLY" });
            db.SaveChanges();
        }
        fields["Store.CurrencyCode"] = "EUR";
        var orderOnly = await Post(client, "/Settings/UpdateStore", "/Settings/Index?section=store", fields);
        Assert.Equal(HttpStatusCode.OK, orderOnly.StatusCode);
        Assert.Contains("Không thể đổi tiền tệ", WebUtility.HtmlDecode(await orderOnly.Content.ReadAsStringAsync()));
        Assert.Equal("USD", ReadDb(app, db => db.Stores.Single(x => x.Id == storeB).CurrencyCode));
    }

    [Fact]
    public async Task Reports_and_dashboard_never_add_different_currencies()
    {
        using var app = new SalesHubFactory();
        var (_, _, storeA, storeB, productId) = app.Seed();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var (storeId, currency, total) in new[] { (storeA, "VND", 100m), (storeA, "USD", 12.50m), (storeB, "USD", 999m) })
            {
                db.Orders.Add(new Order
                {
                    StoreId = storeId, CurrencyCode = currency, Code = Guid.NewGuid().ToString()[..8],
                    Status = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, Total = total,
                    Items = new List<OrderItem> { new() { ProductId = productId, ProductName = "Report Item", Sku = "REPORT", Quantity = 1, LineTotal = total } }
                });
            }
            db.SaveChanges();
        }
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        var vnd = WebUtility.HtmlDecode(await client.GetStringAsync("/Report/Index"));
        Assert.Contains("100 ₫", vnd);
        Assert.DoesNotContain("113", vnd);
        var usd = WebUtility.HtmlDecode(await client.GetStringAsync("/Report/Index?currencyCode=USD"));
        Assert.Contains("12,50 USD", usd);
        Assert.DoesNotContain("999", usd);
        Assert.DoesNotContain("100 ₫", usd);
        var dashboard = WebUtility.HtmlDecode(await client.GetStringAsync("/Home/Dashboard"));
        Assert.Contains("100 ₫", dashboard);
        Assert.DoesNotContain("113 ₫", dashboard);
    }

    [Theory]
    [InlineData("stock", "chỉ còn")]
    [InlineData("duplicate", "Mỗi sản phẩm chỉ nên xuất hiện một lần")]
    [InlineData("zero", "Số lượng phải lớn hơn 0")]
    [InlineData("limit", "Số lượng phải lớn hơn 0")]
    [InlineData("inactive", "không còn kinh doanh")]
    public async Task Invalid_orders_show_errors_preserve_input_and_leave_database_unchanged(string scenario, string error)
    {
        using var app = new SalesHubFactory();
        var (_, _, _, _, productId) = app.Seed();
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        var page = await client.GetStringAsync("/Order/Create");
        var token = System.Text.RegularExpressions.Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        if (scenario == "inactive")
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Products.Single(x => x.Id == productId).IsActive = false;
            db.SaveChanges();
        }
        var quantity = scenario switch { "stock" => "11", "zero" => "0", "limit" => "100001", _ => "1" };
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token),
            ["Items[0].ProductId"] = productId.ToString(), ["Items[0].Quantity"] = quantity,
            ["CustomerName"] = "Keep Customer", ["CustomerPhone"] = "0901234567",
            ["Discount"] = "5", ["Note"] = "Keep Note"
        };
        if (scenario == "duplicate")
        {
            fields["Items[1].ProductId"] = productId.ToString();
            fields["Items[1].Quantity"] = "1";
        }
        var response = await client.PostAsync("/Order/Create", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(error, html);
        Assert.Contains("id=\"orderForm\"", html);
        Assert.Contains("Keep Customer", html);
        Assert.Contains("0901234567", html);
        Assert.Contains("Keep Note", html);
        Assert.Matches("name=\"Discount\"[^>]*value=\"5\"", html);
        if (scenario == "inactive")
            Assert.Contains($"Sản phẩm #{productId} không còn khả dụng", html);
        Assert.Matches("name=\"Items\\[0\\].Quantity\"[^>]*value=\"" + quantity + "\"", html);
        Assert.Equal(0, ReadDb(app, db => db.Orders.Count()));
        Assert.Equal(0, ReadDb(app, db => db.InventoryTransactions.Count()));
        Assert.Equal(10, ReadDb(app, db => db.InventoryItems.Single(x => x.ProductId == productId).Quantity));
        if (scenario != "inactive")
        {
            fields["Items[0].Quantity"] = "1";
            fields.Remove("Items[1].ProductId");
            fields.Remove("Items[1].Quantity");
            Assert.Equal(HttpStatusCode.Redirect,
                (await Post(client, "/Order/Create", "/Order/Create", fields)).StatusCode);
            Assert.Equal(1, ReadDb(app, db => db.Orders.Count()));
            Assert.Equal(9, ReadDb(app, db => db.InventoryItems.Single(x => x.ProductId == productId).Quantity));
        }
    }
}
