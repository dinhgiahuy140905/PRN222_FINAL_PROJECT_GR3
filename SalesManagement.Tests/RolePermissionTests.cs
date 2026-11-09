using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;

namespace SalesManagement.Tests;

public sealed partial class SalesHubFlowTests
{
    [Theory]
    [InlineData("owner@test.local")]
    [InlineData("staff@test.local")]
    public async Task Notification_links_and_payment_filter_are_scoped_to_current_store(string email)
    {
        using var app = new SalesHubFactory();
        var (_, _, a, b, _) = app.Seed();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.InventoryItems.Single().Quantity = 1;
            db.InventoryItems.Single().ReorderLevel = 2;
            db.Orders.AddRange(
                new Order { StoreId = a, Code = "A-UNPAID", Status = OrderStatus.Completed, PaymentStatus = PaymentStatus.Unpaid },
                new Order { StoreId = a, Code = "A-PAID", Status = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid },
                new Order { StoreId = a, Code = "A-PROCESS", Status = OrderStatus.Confirmed },
                new Order { StoreId = b, Code = "B-PRIVATE", Status = OrderStatus.Completed });
            db.SaveChanges();
        }
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, email);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Order/Create"));
        Assert.Contains("notification-count\">3</span>", html);
        Assert.Contains("paymentStatus=Unpaid", html);
        Assert.Contains("status=low-stock", html);
        Assert.DoesNotContain("B-PRIVATE", html);
        var filtered = await client.GetStringAsync("/Order/Index?status=Completed&paymentStatus=Unpaid");
        Assert.Contains("A-UNPAID", filtered);
        Assert.DoesNotContain("A-PAID", filtered);
        Assert.DoesNotContain("A-PROCESS", filtered);
        Assert.DoesNotContain("B-PRIVATE", filtered);
        if (email == "owner@test.local")
        {
            await Post(client, "/Store/Switch", "/Store/Select", new() { ["storeId"] = b.ToString() });
            var other = WebUtility.HtmlDecode(await client.GetStringAsync("/Order/Create"));
            Assert.Contains("notification-count\">1</span>", other);
            Assert.DoesNotContain("Sản phẩm sắp hết hàng", other);
        }
    }

    [Fact]
    public async Task Staff_cannot_write_management_endpoints_even_with_a_valid_antiforgery_token()
    {
        using var app = new SalesHubFactory();
        var (_, staffId, a, b, _) = app.Seed();
        var membershipId = ReadDb(app, db => db.StoreMembers.Single(x => x.UserId == staffId).Id);
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "staff@test.local");
        foreach (var (endpoint, fields) in new[]
        {
            ("/Staff/Invite", new Dictionary<string,string> { ["Invite.Email"] = "owner@test.local" }),
            ($"/Staff/Remove/{membershipId}", new Dictionary<string,string>()),
            ("/Settings/UpdateStore", new Dictionary<string,string>
                { ["Store.Name"] = "Not allowed", ["Store.CurrencyCode"] = "USD", ["Store.InvoiceThankYou"] = "Test" }),
            ("/Store/Switch", new Dictionary<string,string> { ["storeId"] = b.ToString() }),
            ("/Store/Create", new Dictionary<string,string> { ["Name"] = "Not allowed" })
        })
        {
            var response = await Post(client, endpoint, "/Settings/Index", fields);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
        }
        Assert.Equal("Store A", ReadDb(app, db => db.Stores.Single(x => x.Id == a).Name));
        Assert.True(ReadDb(app, db => db.StoreMembers.Any(x => x.Id == membershipId)));
        Assert.Equal(2, ReadDb(app, db => db.Stores.Count()));
    }

    [Fact]
    public async Task Membership_role_changes_override_old_owner_cookie_and_account_type()
    {
        using var app = new SalesHubFactory();
        var (ownerId, _, a, _, _) = app.Seed();
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StoreMembers.Single(x => x.UserId == ownerId && x.StoreId == a).Role = StoreRole.Staff;
            db.SaveChanges();
        }
        var settings = await client.GetStringAsync("/Settings/Index");
        Assert.DoesNotContain("data-settings-target=\"store\"", settings);
        Assert.DoesNotContain("href=\"/Staff\"", settings);
        var response = await Post(client, "/Settings/UpdateStore", "/Settings/Index", new()
        { ["Store.Name"] = "Not allowed", ["Store.CurrencyCode"] = "VND", ["Store.InvoiceThankYou"] = "Test" });
        Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Single(x => x.Id == ownerId).AccountType = AccountType.Staff;
            db.StoreMembers.Single(x => x.UserId == ownerId && x.StoreId == a).Role = StoreRole.Owner;
            db.SaveChanges();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Staff/Index")).StatusCode);
        var cannotCreate = await client.GetAsync("/Store/Create");
        Assert.Contains("AccessDenied", cannotCreate.Headers.Location!.ToString());
    }
}
