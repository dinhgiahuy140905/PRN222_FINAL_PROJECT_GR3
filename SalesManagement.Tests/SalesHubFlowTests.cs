using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using SalesManagement.Web.Services;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;

namespace SalesManagement.Tests;

public sealed partial class SalesHubFlowTests
{
    [Fact]
    public async Task Order_payment_refund_cancel_and_store_scope()
    {
        using var app = new SalesHubFactory();
        var (ownerId, staffId, storeA, storeB, productId) = app.Seed();
        using var owner = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await Login(owner, "owner@test.local");

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/Order/Create")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PostAsync("/Order/RecordPayment/999", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/Customer/Create", "/Customer/Create", new()
        { ["Name"] = "Alice", ["Phone"] = "0900000000" })).StatusCode);
        var customerId = app.CustomerId();
        var created = await Post(owner, "/Order/Create", "/Order/Create", new Dictionary<string, string>
        {
            ["Items[0].ProductId"] = productId.ToString(), ["Items[0].Quantity"] = "2",
            ["PaymentMethod"] = "Cash", ["Discount"] = "0", ["CustomerId"] = customerId.ToString()
        });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var orderId = int.Parse(created.Headers.Location!.ToString().Split('/').Last());
        Assert.Equal((PaymentStatus.Unpaid, 8), app.ReadOrderAndStock(orderId, productId));
        await Post(owner, $"/Customer/Edit/{customerId}", $"/Customer/Edit/{customerId}", new()
        { ["Name"] = "Alice Updated", ["Phone"] = "0911111111" });
        Assert.Equal("Alice", app.OrderCustomerName(orderId));

        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, $"/Order/Complete/{orderId}", $"/Order/Details/{orderId}")).StatusCode);
        await Post(owner, $"/Order/Complete/{orderId}", $"/Order/Details/{orderId}");
        Assert.Equal(PaymentStatus.Unpaid, app.ReadOrderAndStock(orderId, productId).Item1);
        await Post(owner, $"/Order/RecordPayment/{orderId}", $"/Order/Details/{orderId}");
        await Post(owner, $"/Order/RecordPayment/{orderId}", $"/Order/Details/{orderId}");
        Assert.Equal(PaymentStatus.Paid, app.ReadOrderAndStock(orderId, productId).Item1);
        var currentReport = WebUtility.HtmlDecode(await owner.GetStringAsync("/Report/Index"));
        Assert.Contains("200 ₫", currentReport);
        var pastReport = WebUtility.HtmlDecode(await owner.GetStringAsync("/Report/Index?from=2000-01-01&to=2000-12-31"));
        Assert.DoesNotContain("200 ₫", pastReport);
        await Post(owner, $"/Order/Cancel/{orderId}", $"/Order/Details/{orderId}");
        Assert.Equal((PaymentStatus.Paid, 8), app.ReadOrderAndStock(orderId, productId));
        await Post(owner, $"/Order/RecordRefund/{orderId}", $"/Order/Details/{orderId}");
        await Post(owner, $"/Order/RecordRefund/{orderId}", $"/Order/Details/{orderId}");
        await Post(owner, $"/Order/Cancel/{orderId}", $"/Order/Details/{orderId}");
        await Post(owner, $"/Order/Cancel/{orderId}", $"/Order/Details/{orderId}");
        Assert.Equal((PaymentStatus.Refunded, 10), app.ReadOrderAndStock(orderId, productId));
        Assert.Equal(OrderStatus.Cancelled, app.ReadOrderStatus(orderId));

        using var staff = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await Login(staff, "staff@test.local");
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/Order/Index")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(staff, "/Product/AdjustStock", $"/Product/AdjustStock/{productId}", new()
        { ["ProductId"] = productId.ToString(), ["QuantityChange"] = "1", ["Note"] = "count" })).StatusCode);
        Assert.Equal(11, app.ReadOrderAndStock(orderId, productId).Item2);
        Assert.Equal(HttpStatusCode.Redirect, (await staff.GetAsync("/Staff/Index")).StatusCode);
        var forbiddenSwitch = await Post(staff, "/Store/Switch", "/Store/Select", new() { ["storeId"] = storeB.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, forbiddenSwitch.StatusCode);
        Assert.Contains("AccessDenied", forbiddenSwitch.Headers.Location!.ToString());

        await Post(owner, "/Store/Switch", "/Store/Select", new() { ["storeId"] = storeB.ToString() });
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Order/Details/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(owner, $"/Order/RecordPayment/{orderId}", "/Order/Index")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Customer/Edit/{customerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/Report/Index")).StatusCode);
    }

    [Fact]
    public async Task Registration_and_empty_store_pages()
    {
        using var app = new SalesHubFactory();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        app.EnsureDatabase();
        var response = await Post(client, "/Account/Register", "/Account/Register", new()
        {
            ["FullName"] = "New Owner", ["Email"] = "new@test.local", ["Password"] = "Password123!",
            ["ConfirmPassword"] = "Password123!", ["AccountType"] = "Owner"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Store/Create", response.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Store/Create")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Store/Create", "/Store/Create", new()
        { ["Name"] = "New Shop", ["Address"] = "Main Street" })).StatusCode);
        Assert.Equal(1, ReadDb(app, db => db.Stores.Count(x => x.Name == "New Shop")));
        Assert.Equal(StoreRole.Owner, ReadDb(app, db => db.StoreMembers.Single(x => x.Store.Name == "New Shop").Role));

        using var staff = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var staffRegister = await Post(staff, "/Account/Register", "/Account/Register", new()
        {
            ["FullName"] = "New Staff", ["Email"] = "newstaff@test.local", ["Password"] = "Password123!",
            ["ConfirmPassword"] = "Password123!", ["AccountType"] = "Staff"
        });
        Assert.Equal(HttpStatusCode.Redirect, staffRegister.StatusCode);
        Assert.Contains("/Store/Select", staffRegister.Headers.Location!.ToString());
        var denied = await staff.GetAsync("/Store/Create");
        Assert.Contains("AccessDenied", denied.Headers.Location!.ToString());
    }


    [Fact]
    public async Task Catalog_validation_stock_and_cross_store_ids()
    {
        using var app = new SalesHubFactory();
        var (_, _, storeA, storeB, _) = app.Seed();
        using var owner = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await Login(owner, "owner@test.local");

        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/Category/Create", "/Category/Index", new() { ["Name"] = "Hardware" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(owner, "/Category/Create", "/Category/Index", new() { ["Name"] = "Hardware" })).StatusCode);
        var categoryId = ReadDb(app, db => db.Categories.Single(x => x.StoreId == storeA && x.Name == "Hardware").Id);
        Assert.Equal(1, ReadDb(app, db => db.Categories.Count(x => x.StoreId == storeA)));

        var fields = new Dictionary<string, string>
        {
            ["Name"] = "Cable", ["Sku"] = " cable ", ["Unit"] = "pcs",
            ["CostPrice"] = "5", ["SalePrice"] = "10", ["OpeningStock"] = "3",
            ["ReorderLevel"] = "2", ["CategoryId"] = categoryId.ToString()
        };
        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/Product/Create", "/Product/Create", fields)).StatusCode);
        var productId = ReadDb(app, db => db.Products.Single(x => x.StoreId == storeA && x.Sku == "CABLE").Id);
        Assert.Equal(3, ReadDb(app, db => db.InventoryItems.Single(x => x.ProductId == productId).Quantity));
        Assert.Equal(1, ReadDb(app, db => db.InventoryTransactions.Count(x => x.ProductId == productId && x.Type == InventoryTransactionType.OpeningStock)));
        Assert.Equal(HttpStatusCode.OK, (await Post(owner, "/Product/Create", "/Product/Create", fields)).StatusCode);
        Assert.Equal(1, ReadDb(app, db => db.Products.Count(x => x.StoreId == storeA && x.Sku == "CABLE")));

        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, $"/Category/Delete/{categoryId}", "/Category/Index")).StatusCode);
        Assert.True(ReadDb(app, db => db.Categories.Any(x => x.Id == categoryId)));
        Assert.Equal(HttpStatusCode.OK, (await Post(owner, "/Product/AdjustStock", $"/Product/AdjustStock/{productId}", new()
        { ["ProductId"] = productId.ToString(), ["QuantityChange"] = "-4" })).StatusCode);
        Assert.Equal(3, ReadDb(app, db => db.InventoryItems.Single(x => x.ProductId == productId).Quantity));
        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/Product/AdjustStock", $"/Product/AdjustStock/{productId}", new()
        { ["ProductId"] = productId.ToString(), ["QuantityChange"] = "-2" })).StatusCode);
        Assert.Equal(1, ReadDb(app, db => db.InventoryItems.Single(x => x.ProductId == productId).Quantity));

        await Post(owner, "/Store/Switch", "/Store/Select", new() { ["storeId"] = storeB.ToString() });
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Product/Edit/{productId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(owner, $"/Product/Deactivate/{productId}", "/Product/Index")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(owner, $"/Category/Delete/{categoryId}", "/Category/Index")).StatusCode);
        Assert.DoesNotContain("Cable", await owner.GetStringAsync("/Product/Index"));
    }

    [Fact]
    public async Task Owner_manages_staff_and_store_settings_staff_cannot()
    {
        using var app = new SalesHubFactory();
        var (_, _, storeA, storeB, _) = app.Seed();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var candidate = new User { FullName = "Candidate", Email = "candidate@test.local", AccountType = AccountType.Staff };
            candidate.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(candidate, "Password123!");
            db.Users.Add(candidate);
            db.SaveChanges();
        }
        using var owner = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var staff = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await Login(owner, "owner@test.local");
        await Login(staff, "staff@test.local");

        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/Staff/Invite", "/Staff/Index", new()
        { ["Invite.Email"] = "candidate@test.local" })).StatusCode);
        var membershipId = ReadDb(app, db => db.StoreMembers.Single(x => x.StoreId == storeA && x.User.Email == "candidate@test.local").Id);
        var staffInvite = await Post(staff, "/Staff/Invite", "/Settings/Index", new() { ["Invite.Email"] = "candidate@test.local" });
        Assert.Contains("AccessDenied", staffInvite.Headers.Location!.ToString());
        var staffRemove = await Post(staff, $"/Staff/Remove/{membershipId}", "/Settings/Index");
        Assert.Contains("AccessDenied", staffRemove.Headers.Location!.ToString());
        Assert.True(ReadDb(app, db => db.StoreMembers.Any(x => x.Id == membershipId)));

        var storeFields = new Dictionary<string, string>
        { ["Store.Name"] = "Renamed Store A", ["Store.CurrencyCode"] = "VND", ["Store.InvoiceThankYou"] = "Thanks" };
        var staffUpdate = await Post(staff, "/Settings/UpdateStore", "/Settings/Index", storeFields);
        Assert.Contains("AccessDenied", staffUpdate.Headers.Location!.ToString());
        var preference = await Post(staff, "/Settings/UpdatePreferences", "/Settings/Index", new()
        { ["defaultStoreId"] = storeB.ToString() });
        Assert.Contains("AccessDenied", preference.Headers.Location!.ToString());
        Assert.Equal("Store A", ReadDb(app, db => db.Stores.Single(x => x.Id == storeA).Name));

        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, "/Settings/UpdateStore", "/Settings/Index?section=store", storeFields)).StatusCode);
        Assert.Equal("Renamed Store A", ReadDb(app, db => db.Stores.Single(x => x.Id == storeA).Name));
        Assert.Equal("Store B", ReadDb(app, db => db.Stores.Single(x => x.Id == storeB).Name));
        Assert.Equal(HttpStatusCode.Redirect, (await Post(owner, $"/Staff/Remove/{membershipId}", "/Staff/Index")).StatusCode);
        Assert.False(ReadDb(app, db => db.StoreMembers.Any(x => x.Id == membershipId)));
    }


    [Fact]
    public async Task Profile_and_password_change_require_current_password()
    {
        using var app = new SalesHubFactory();
        app.Seed();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        var originalHash = ReadDb(app, db => db.Users.Single(x => x.Email == "owner@test.local").PasswordHash);

        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Settings/UpdateProfile", "/Settings/Index", new()
        { ["Profile.FullName"] = "Owner Updated", ["Profile.Phone"] = "0900000000" })).StatusCode);
        Assert.Equal("Owner Updated", ReadDb(app, db => db.Users.Single(x => x.Email == "owner@test.local").FullName));

        var wrong = await Post(client, "/Settings/ChangePassword", "/Settings/Index?section=security", new()
        {
            ["Security.CurrentPassword"] = "wrong-password", ["Security.NewPassword"] = "NewPassword123!",
            ["Security.ConfirmPassword"] = "NewPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Equal(originalHash, ReadDb(app, db => db.Users.Single(x => x.Email == "owner@test.local").PasswordHash));

        var changed = await Post(client, "/Settings/ChangePassword", "/Settings/Index?section=security", new()
        {
            ["Security.CurrentPassword"] = "Password123!", ["Security.NewPassword"] = "NewPassword123!",
            ["Security.ConfirmPassword"] = "NewPassword123!"
        });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.NotEqual(originalHash, ReadDb(app, db => db.Users.Single(x => x.Email == "owner@test.local").PasswordHash));
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Account/Logout", "/Settings/Index")).StatusCode);
        var oldLogin = await Post(client, "/Account/Login", "/Account/Login", new()
        { ["Email"] = "owner@test.local", ["Password"] = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode);
        var newLogin = await Post(client, "/Account/Login", "/Account/Login", new()
        { ["Email"] = "owner@test.local", ["Password"] = "NewPassword123!" });
        Assert.Equal(HttpStatusCode.Redirect, newLogin.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Password_change_revokes_other_sessions_and_preserves_current_session(bool rememberMe)
    {
        using var app = new SalesHubFactory();
        var (ownerId, _, storeA, storeB, _) = app.Seed();
        using var a = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        using var b = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Post(a, "/Account/Login", "/Account/Login", new()
        {
            ["Email"] = "owner@test.local", ["Password"] = "Password123!",
            ["RememberMe"] = rememberMe.ToString()
        });
        await Login(b, "owner@test.local");
        // Preserve the selected store, even when it differs from the default store.
        await Post(a, "/Store/Switch", "/Store/Select", new() { ["storeId"] = storeB.ToString() });
        var original = ReadDb(app, db => db.Users.Where(u => u.Id == ownerId)
            .Select(u => new { u.PasswordHash, u.SecurityStamp, u.PasswordChangedAt }).Single());
        var fields = new Dictionary<string, string>
        {
            ["Security.CurrentPassword"] = "wrong-password",
            ["Security.NewPassword"] = "NewPassword123!",
            ["Security.ConfirmPassword"] = "NewPassword123!"
        };
        Assert.Equal(HttpStatusCode.OK,
            (await Post(a, "/Settings/ChangePassword", "/Settings/Index?section=security", fields)).StatusCode);
        Assert.Equal(original, ReadDb(app, db => db.Users.Where(u => u.Id == ownerId)
            .Select(u => new { u.PasswordHash, u.SecurityStamp, u.PasswordChangedAt }).Single()));
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/Settings/Index")).StatusCode);

        fields["Security.CurrentPassword"] = "Password123!";
        var changed = await Post(a, "/Settings/ChangePassword", "/Settings/Index?section=security", fields);
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        var ticket = ReadAuthTicket(app, changed);
        Assert.Equal(rememberMe, ticket.Properties.IsPersistent);
        Assert.Equal(storeB.ToString(), ticket.Principal.FindFirstValue(StoreClaimTypes.StoreId));
        Assert.Equal(StoreRole.Owner.ToString(), ticket.Principal.FindFirstValue(StoreClaimTypes.StoreRole));
        var currentStamp = ReadDb(app, db => db.Users.Single(u => u.Id == ownerId).SecurityStamp);
        Assert.NotEqual(original.SecurityStamp, currentStamp);
        Assert.Equal(currentStamp, ticket.Principal.FindFirstValue(StoreClaimTypes.SecurityStamp));
        Assert.NotEqual(original.PasswordHash, ReadDb(app, db => db.Users.Single(u => u.Id == ownerId).PasswordHash));
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/Settings/Index")).StatusCode);

        var rejected = await b.GetAsync("/Settings/Index");
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Contains("/Account/Login", rejected.Headers.Location!.ToString());
        Assert.Contains(rejected.Headers.GetValues("Set-Cookie"), value => value.Contains("expires=Thu, 01 Jan 1970"));
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/Account/Login")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(b, "/Account/Login", "/Account/Login", new()
        { ["Email"] = "owner@test.local", ["Password"] = "Password123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(b, "/Account/Login", "/Account/Login", new()
        { ["Email"] = "owner@test.local", ["Password"] = "NewPassword123!" })).StatusCode);
    }

    [Theory]
    [InlineData("missing-stamp")]
    [InlineData("invalid-stamp")]
    [InlineData("missing-user")]
    public async Task Invalid_or_legacy_sessions_are_rejected_without_server_errors(string scenario)
    {
        using var app = new SalesHubFactory();
        app.Seed();
        using var loginClient = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        var login = await Post(loginClient, "/Account/Login", "/Account/Login", new()
        { ["Email"] = "owner@test.local", ["Password"] = "Password123!" });
        var ticket = ReadAuthTicket(app, login);
        var identity = (ClaimsIdentity)ticket.Principal.Identity!;
        if (scenario == "missing-user")
        {
            // A real, valid cookie for a user subsequently deleted from the database.
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User { FullName = "Deleted", Email = "deleted@test.local" };
            db.Users.Add(user);
            db.SaveChanges();
            identity.RemoveClaim(identity.FindFirst(ClaimTypes.NameIdentifier)!);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            db.Users.Remove(user);
            db.SaveChanges();
        }
        else
        {
            identity.RemoveClaim(identity.FindFirst(StoreClaimTypes.SecurityStamp)!);
            if (scenario == "invalid-stamp")
                identity.AddClaim(new Claim(StoreClaimTypes.SecurityStamp, "invalid"));
        }
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket));
        var response = await client.GetAsync("/Settings/Index");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("expires=Thu, 01 Jan 1970"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
    }

    private static AuthenticationTicket ReadAuthTicket(SalesHubFactory app, HttpResponseMessage response)
    {
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(options.Cookie.Name + "="));
        return options.TicketDataFormat.Unprotect(cookie.Split(';')[0][(options.Cookie.Name!.Length + 1)..])!;
    }

    private static T ReadDb<T>(SalesHubFactory app, Func<ApplicationDbContext, T> read)
    {
        using var scope = app.Services.CreateScope();
        return read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static async Task Login(HttpClient client, string email)
    {
        var response = await Post(client, "/Account/Login", "/Account/Login", new()
        { ["Email"] = email, ["Password"] = "Password123!" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, string tokenPage, Dictionary<string, string>? values = null)
    {
        var html = await client.GetStringAsync(tokenPage);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, $"No antiforgery token on {tokenPage}");
        values ??= new();
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return await client.PostAsync(path, new FormUrlEncodedContent(values));
    }

    private sealed class SalesHubFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            connection.Open();
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            });
        }

        public void EnsureDatabase()
        {
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        }

        public (int OwnerId, int StaffId, int StoreA, int StoreB, int ProductId) Seed()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
            var owner = new User { FullName = "Owner", Email = "owner@test.local", AccountType = AccountType.Owner };
            var staff = new User { FullName = "Staff", Email = "staff@test.local", AccountType = AccountType.Staff };
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
            owner.PasswordHash = hasher.HashPassword(owner, "Password123!");
            staff.PasswordHash = hasher.HashPassword(staff, "Password123!");
            var a = new Store { Name = "Store A" };
            var b = new Store { Name = "Store B" };
            db.StoreMembers.AddRange(
                new StoreMember { User = owner, Store = a, Role = StoreRole.Owner },
                new StoreMember { User = owner, Store = b, Role = StoreRole.Owner },
                new StoreMember { User = staff, Store = a, Role = StoreRole.Staff });
            var product = new Product { Store = a, Name = "Item", Sku = "ITEM", SalePrice = 100, Inventory = new InventoryItem { Store = a, Quantity = 10, RowVersion = new byte[] { 1 } } };
            db.Products.Add(product);
            db.SaveChanges();
            owner.DefaultStoreId = a.Id;
            staff.DefaultStoreId = a.Id;
            db.SaveChanges();
            return (owner.Id, staff.Id, a.Id, b.Id, product.Id);
        }

        public (PaymentStatus, int) ReadOrderAndStock(int orderId, int productId)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return (db.Orders.AsNoTracking().Single(x => x.Id == orderId).PaymentStatus,
                db.InventoryItems.AsNoTracking().Single(x => x.ProductId == productId).Quantity);
        }

        public int CustomerId()
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Customers.Single().Id;
        }

        public string OrderCustomerName(int orderId)
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.AsNoTracking().Single(x => x.Id == orderId).CustomerName;
        }

        public OrderStatus ReadOrderStatus(int orderId)
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.AsNoTracking().Single(x => x.Id == orderId).Status;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) connection.Dispose();
        }
    }
}
