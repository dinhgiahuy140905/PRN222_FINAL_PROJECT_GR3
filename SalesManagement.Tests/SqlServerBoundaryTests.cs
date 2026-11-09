using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;

namespace SalesManagement.Tests;

public sealed partial class SalesHubFlowTests
{
    [SqlServerFact]
    public async Task SqlServer_limits_preserve_orders_and_database_prevents_negative_stock()
    {
        var database = "SalesHubAudit_" + Guid.NewGuid().ToString("N");
        using var factory = new SalesHubFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
                $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True"));
        }));
        try
        {
            int productId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureCreated();
                var user = new User { Email = "owner@test.local", FullName = "Owner" };
                user.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(user, "Password123!");
                var store = new Store { Name = "SQL boundary" };
                db.StoreMembers.Add(new StoreMember { User = user, Store = store, Role = StoreRole.Owner });
                var product = new Product { Store = store, Name = "Money test", Sku = "MONEY", SalePrice = 999999999999m,
                    Inventory = new InventoryItem { Store = store, Quantity = 100000 } };
                db.Products.Add(product);
                db.SaveChanges();
                productId = product.Id;
            }
            using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
            await Login(client, "owner@test.local");
            var response = await Post(client, "/Order/Create", "/Order/Create", new()
            { ["Items[0].ProductId"] = productId.ToString(), ["Items[0].Quantity"] = "100000" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Tổng tiền vượt giới hạn", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.False(db.Orders.Any());
                Assert.Equal(100000, db.InventoryItems.Single().Quantity);
                db.Products.Single().SalePrice = 100;
                db.SaveChanges();
            }
            var created = await Post(client, "/Order/Create", "/Order/Create", new()
            { ["Items[0].ProductId"] = productId.ToString(), ["Items[0].Quantity"] = "1" });
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            int orderId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                orderId = db.Orders.Single().Id;
                db.InventoryItems.Single().Quantity = int.MaxValue;
                db.SaveChanges();
            }
            await Post(client, $"/Order/Cancel/{orderId}", $"/Order/Details/{orderId}");
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal(int.MaxValue, db.InventoryItems.Single().Quantity);
                Assert.Equal(OrderStatus.Confirmed, db.Orders.Single().Status);
                db.InventoryItems.Single().Quantity = -1;
                Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            }
            using var first = app.Services.CreateScope();
            using var second = app.Services.CreateScope();
            var firstDb = first.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var secondDb = second.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            firstDb.InventoryItems.Single().Quantity--;
            secondDb.InventoryItems.Single().Quantity--;
            firstDb.SaveChanges();
            Assert.Throws<DbUpdateConcurrencyException>(() => secondDb.SaveChanges());
        }
        finally
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^SalesHubAudit_[0-9a-f]{32}$"))
                throw new InvalidOperationException("Unexpected scratch database");
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeleted();
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("SALESHUB_TEST_SQLSERVER") != "1")
                Skip = "Set SALESHUB_TEST_SQLSERVER=1 on Windows with LocalDB to run the isolated SQL Server test.";
        }
    }
}
