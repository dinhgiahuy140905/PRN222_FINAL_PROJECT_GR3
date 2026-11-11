using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesManagement.Web.Data;
using SalesManagement.Web.Models;

namespace SalesManagement.Tests;

public sealed partial class SalesHubFlowTests
{
    [BrowserFact]
    public async Task Owner_and_staff_buttons_work_in_real_chrome()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var database = "SalesHubRoleUi_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(
            $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True").Options;
        using var db = new ApplicationDbContext(options);
        Process? web = null;
        try
        {
            db.Database.EnsureCreated();
            var a = new Store { Name = "Role UI Store A" };
            var b = new Store { Name = "Role UI Empty Store B" };
            var owner = new User { FullName = "Browser Owner", Email = "owner@browser.test", AccountType = AccountType.Owner };
            var staff = new User { FullName = "Browser Staff", Email = "staff@browser.test", AccountType = AccountType.Staff };
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
            foreach (var user in new[] { owner, staff }) user.PasswordHash = hasher.HashPassword(user, "BrowserTest123!");
            var candidate = new User { FullName = "Browser Candidate", Email = "candidate@browser.test", AccountType = AccountType.Staff };
            candidate.PasswordHash = hasher.HashPassword(candidate, "BrowserTest123!");
            db.Users.Add(candidate);
            db.StoreMembers.AddRange(new StoreMember { Store = a, User = owner, Role = StoreRole.Owner },
                new StoreMember { Store = a, User = staff, Role = StoreRole.Staff },
                new StoreMember { Store = b, User = owner, Role = StoreRole.Staff });
            db.Products.Add(new Product { Store = a, Name = "Shared low stock", Sku = "LOW-STOCK", SalePrice = 25,
                Inventory = new InventoryItem { Store = a, Quantity = 2, ReorderLevel = 3 } });
            db.SaveChanges();
            owner.DefaultStoreId = a.Id;
            staff.DefaultStoreId = a.Id;
            db.SaveChanges();
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var url = $"http://127.0.0.1:{port}";
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "SalesManagement.Web"),
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            start.ArgumentList.Add(Path.Combine(root, "SalesManagement.Web/bin/Debug/net8.0/SalesManagement.Web.dll"));
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["ASPNETCORE_URLS"] = url;
            start.Environment.Remove("ASPNETCORE_HTTPS_PORT");
            start.Environment["ConnectionStrings__DefaultConnection"] = db.Database.GetConnectionString();
            start.Environment["Mail__Mode"] = "Pickup";
            start.Environment["Logging__LogLevel__Default"] = "Warning";
            web = Process.Start(start)!;
            using var http = new HttpClient();
            var ready = false;
            for (var i = 0; i < 100 && !web.HasExited; i++)
            {
                try { ready = (await http.GetAsync(url + "/Account/Login")).IsSuccessStatusCode; }
                catch (HttpRequestException) { }
                if (ready) break;
                await Task.Delay(100);
            }
            Assert.True(ready, "Browser fixture did not start");
            var node = new ProcessStartInfo("node") { WorkingDirectory = root, UseShellExecute = false,
                CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
            node.ArgumentList.Add("SalesManagement.Tests/Browser/role-actions.cjs");
            node.ArgumentList.Add(url);
            node.ArgumentList.Add(b.Id.ToString());
            node.ArgumentList.Add(Path.Combine(Path.GetDirectoryName(root)!, "review-evidence", "role-ui"));
            using var browser = Process.Start(node)!;
            var stdout = browser.StandardOutput.ReadToEndAsync();
            var stderr = browser.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try { await browser.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { browser.Kill(entireProcessTree: true); throw; }
            Assert.True(browser.ExitCode == 0, await stdout + "\n" + await stderr);
            Assert.Contains("PASS", await stdout);
        }
        finally
        {
            if (web is not null) { if (!web.HasExited) { web.Kill(entireProcessTree: true); web.WaitForExit(); } web.Dispose(); }
            if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^SalesHubRoleUi_[0-9a-f]{32}$"))
                throw new InvalidOperationException("Unexpected scratch database");
            db.Database.EnsureDeleted();
        }
    }

    private sealed class BrowserFactAttribute : FactAttribute
    {
        public BrowserFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("SALESHUB_TEST_BROWSER") != "1")
                Skip = "Set SALESHUB_TEST_BROWSER=1 with Windows, LocalDB, Node and Chrome to run browser role tests.";
        }
    }
}
