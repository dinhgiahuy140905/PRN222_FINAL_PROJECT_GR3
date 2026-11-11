using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;
using SalesManagement.Web.Services;

namespace SalesManagement.Tests;

public sealed partial class SalesHubFlowTests
{
    [Fact]
    public async Task Cancel_overflow_does_not_modify_order_stock_or_ledger()
    {
        using var app = new SalesHubFactory();
        var (_, _, _, _, productId) = app.Seed();
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        await Post(client, "/Order/Create", "/Order/Create", new()
        { ["Items[0].ProductId"] = productId.ToString(), ["Items[0].Quantity"] = "1" });
        var orderId = ReadDb(app, db => db.Orders.Single().Id);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.InventoryItems.Single().Quantity = int.MaxValue;
            db.SaveChanges();
        }
        await Post(client, $"/Order/Cancel/{orderId}", $"/Order/Details/{orderId}");
        Assert.Equal(int.MaxValue, ReadDb(app, db => db.InventoryItems.Single().Quantity));
        Assert.Equal(OrderStatus.Confirmed, ReadDb(app, db => db.Orders.Single().Status));
        Assert.Equal(1, ReadDb(app, db => db.InventoryTransactions.Count()));
        Assert.Contains("vượt giới hạn", WebUtility.HtmlDecode(await client.GetStringAsync($"/Order/Details/{orderId}")));
        var adjustment = await Post(client, "/Product/AdjustStock", $"/Product/AdjustStock/{productId}", new()
        { ["ProductId"] = productId.ToString(), ["QuantityChange"] = "1" });
        Assert.Contains("vượt giới hạn", WebUtility.HtmlDecode(await adjustment.Content.ReadAsStringAsync()));
        Assert.Equal(int.MaxValue, ReadDb(app, db => db.InventoryItems.Single().Quantity));
        await Post(client, "/Product/AdjustStock", $"/Product/AdjustStock/{productId}", new()
        { ["ProductId"] = productId.ToString(), ["QuantityChange"] = "-1" });
        await Post(client, $"/Order/Cancel/{orderId}", $"/Order/Details/{orderId}");
        Assert.Equal(OrderStatus.Cancelled, ReadDb(app, db => db.Orders.Single().Status));
        Assert.Equal(int.MaxValue, ReadDb(app, db => db.InventoryItems.Single().Quantity));
    }

    [Fact]
    public async Task Oversized_order_shows_error_and_preserves_database()
    {
        using var app = new SalesHubFactory();
        var (_, _, _, _, productId) = app.Seed();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Products.Single().SalePrice = 999999999999m;
            db.InventoryItems.Single().Quantity = 100000;
            db.SaveChanges();
        }
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        var response = await Post(client, "/Order/Create", "/Order/Create", new()
        { ["Items[0].ProductId"] = productId.ToString(), ["Items[0].Quantity"] = "100000" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Tổng tiền vượt giới hạn", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.False(ReadDb(app, db => db.Orders.Any()));
        Assert.False(ReadDb(app, db => db.InventoryTransactions.Any()));
        Assert.Equal(100000, ReadDb(app, db => db.InventoryItems.Single().Quantity));
    }

    [Fact]
    public async Task Money_with_more_than_two_decimals_is_rejected()
    {
        using var app = new SalesHubFactory();
        var (_, _, _, _, productId) = app.Seed();
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(client, "owner@test.local");
        var product = await Post(client, "/Product/Create", "/Product/Create", new()
        { ["Name"] = "Decimal test", ["Sku"] = "DECIMAL", ["Unit"] = "pcs", ["SalePrice"] = "12.345" });
        Assert.Contains("tối đa 2 chữ số", WebUtility.HtmlDecode(await product.Content.ReadAsStringAsync()));
        Assert.Equal(1, ReadDb(app, db => db.Products.Count()));
        var order = await Post(client, "/Order/Create", "/Order/Create", new()
        { ["Items[0].ProductId"] = productId.ToString(), ["Items[0].Quantity"] = "1", ["Discount"] = "0.005" });
        Assert.Contains("tối đa 2 chữ số", WebUtility.HtmlDecode(await order.Content.ReadAsStringAsync()));
        Assert.False(ReadDb(app, db => db.Orders.Any()));
        var valid = await Post(client, "/Product/Create", "/Product/Create", new()
        { ["Name"] = "Valid decimal", ["Sku"] = "VALID-DECIMAL", ["Unit"] = "pcs", ["SalePrice"] = "12.50" });
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        Assert.Equal(12.50m, ReadDb(app, db => db.Products.Single(product => product.Sku == "VALID-DECIMAL").SalePrice));
    }

    [Fact]
    public async Task Recovery_email_resets_password_once_and_revokes_old_sessions()
    {
        using var factory = new SalesHubFactory();
        var sender = new CapturingResetSender();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IResetEmailSender>();
            services.AddSingleton<IResetEmailSender>(sender);
        }));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
            var user = new User { Email = "recovery@test.local", FullName = "Recovery" };
            user.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(user, "Password123!");
            db.Users.Add(user);
            db.SaveChanges();
        }
        using var loggedIn = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await Login(loggedIn, "recovery@test.local");
        var known = await Post(client, "/Account/ForgotPassword", "/Account/ForgotPassword", new() { ["Email"] = "recovery@test.local" });
        Assert.Equal("recovery@test.local", sender.Email);
        Assert.Equal("localhost", sender.Link!.Host);
        var token = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(sender.Link.Query)["token"].ToString();
        var knownHtml = WebUtility.HtmlDecode(await known.Content.ReadAsStringAsync());
        Assert.DoesNotContain(token, knownHtml);
        var unknown = await Post(client, "/Account/ForgotPassword", "/Account/ForgotPassword", new() { ["Email"] = "unknown@test.local" });
        Assert.Contains("Nếu email đã được đăng ký", WebUtility.HtmlDecode(await unknown.Content.ReadAsStringAsync()));
        Assert.Contains("Nếu email đã được đăng ký", knownHtml);
        Assert.Equal(1, sender.Count);
        var fields = new Dictionary<string,string>
        { ["Token"] = token, ["Password"] = "Recovered123!", ["ConfirmPassword"] = "Recovered123!" };
        var changed = await Post(client, "/Account/ResetPassword", "/Account/ResetPassword?token=" + Uri.EscapeDataString(token), fields);
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Contains("/Account/Login", changed.Headers.Location!.ToString());
        var rejected = await loggedIn.GetAsync("/Settings/Index");
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Contains("/Account/Login", rejected.Headers.Location!.ToString());
        var replay = await Post(client, "/Account/ResetPassword", "/Account/ResetPassword", fields);
        Assert.Contains("đã được sử dụng", WebUtility.HtmlDecode(await replay.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Account/Login", "/Account/Login", new()
        { ["Email"] = "recovery@test.local", ["Password"] = "Password123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Account/Login", "/Account/Login", new()
        { ["Email"] = "recovery@test.local", ["Password"] = "Recovered123!" })).StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("tampered")]
    [InlineData("wrong-confirmation")]
    [InlineData("password-changed")]
    public async Task Invalid_recovery_cannot_change_password(string scenario)
    {
        using var app = new SalesHubFactory();
        app.Seed();
        string token, originalHash;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = db.Users.Single(u => u.Email == "owner@test.local");
            originalHash = user.PasswordHash;
            token = scope.ServiceProvider.GetRequiredService<PasswordResetService>().CreateToken(user,
                scenario == "expired" ? TimeSpan.FromSeconds(-5) : TimeSpan.FromMinutes(30));
            if (scenario == "password-changed") { user.SecurityStamp = Guid.NewGuid().ToString(); db.SaveChanges(); }
        }
        if (scenario == "tampered") token = "invalid-token";
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        var response = await Post(client, "/Account/ResetPassword", "/Account/ResetPassword", new()
        { ["Token"] = token, ["Password"] = "Recovered123!", ["ConfirmPassword"] = scenario == "wrong-confirmation" ? "Different123!" : "Recovered123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(originalHash, ReadDb(app, db => db.Users.Single(u => u.Email == "owner@test.local").PasswordHash));
    }

    [Fact]
    public async Task Recovery_requests_are_rate_limited()
    {
        using var factory = new SalesHubFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        { services.RemoveAll<IResetEmailSender>(); services.AddSingleton<IResetEmailSender>(new CapturingResetSender()); }));
        using (var scope = app.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Account/ForgotPassword", "/Account/ForgotPassword", new() { ["Email"] = "unknown@test.local" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Post(client, "/Account/ForgotPassword", "/Account/ForgotPassword", new() { ["Email"] = "unknown@test.local" })).StatusCode);
    }

    [Fact]
    public async Task Unavailable_recovery_transport_does_not_reveal_accounts()
    {
        using var factory = new SalesHubFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        { services.RemoveAll<IResetEmailSender>(); services.AddSingleton<IResetEmailSender>(new CapturingResetSender { Available = false }); }));
        using (var scope = app.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        var response = await Post(client, "/Account/ForgotPassword", "/Account/ForgotPassword", new() { ["Email"] = "unknown@test.local" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("chưa sẵn sàng", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    private sealed class CapturingResetSender : IResetEmailSender
    {
        public bool Available { get; init; } = true;
        public bool IsConfigured => Available;
        public string? Email { get; private set; }
        public Uri? Link { get; private set; }
        public int Count { get; private set; }
        public Task SendAsync(string email, Uri link) { Email = email; Link = link; Count++; return Task.CompletedTask; }
    }
}
