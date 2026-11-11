using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using SalesManagement.Web.Data;

namespace SalesManagement.Tests;

public sealed partial class SalesHubFlowTests
{
    [SqlServerTheory]
    [InlineData("empty")]
    [InlineData("main")]
    [InlineData("incoming")]
    public async Task Merged_migrations_converge_from_both_branch_histories(string history)
    {
        var database = "SalesHubMerge_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(
            $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True").Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            if (history == "empty")
            {
                // Exercise the deployable idempotent SQL script on a completely empty database.
                await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
                await RunIdempotentScriptAsync(db);
                await SeedMigrationDataAsync(db, hasCurrency: true, hasStamp: true);
            }
            else
            {
                await migrator.MigrateAsync("20260922084202_AddCustomers");
                await SeedMigrationDataAsync(db, hasCurrency: false, hasStamp: false);
                if (history == "main")
                {
                    // Recreate main's actual sparse migration history: no incoming migrations.
                    await ApplyHistoricalMigrationAsync(db, "20261111042806_AddSecurityStampToUser");
                    await ApplyHistoricalMigrationAsync(db, "20261111044216_AddCurrencyCodeToOrder");
                    await db.Database.ExecuteSqlRawAsync(
                        "UPDATE [Orders] SET [CurrencyCode] = N'USD' WHERE [Code] = N'OLD-USD';");
                    await db.Database.ExecuteSqlRawAsync(
                        "UPDATE [Users] SET [SecurityStamp] = N'existing-main-stamp' WHERE [Email] = N'first@migration.test';");
                }
                else
                {
                    await migrator.MigrateAsync("20261109031017_GuardNonnegativeInventory");
                }
            }

            // A valid historical snapshot must survive even if store metadata later changed.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE [Stores] SET [CurrencyCode] = N'EUR' WHERE [Name] = N'USD store';");
            await db.Database.MigrateAsync();

            var orders = await db.Orders.AsNoTracking().OrderBy(order => order.Code).ToListAsync();
            Assert.Equal(3, orders.Count);
            foreach (var order in orders)
            {
                Assert.Equal(order.Code[4..], order.CurrencyCode);
                Assert.Equal(12.50m, order.Total);
            }
            var users = await db.Users.AsNoTracking().OrderBy(user => user.Email).ToListAsync();
            Assert.Equal(2, users.Count);
            Assert.All(users, user =>
            {
                Assert.Equal("unchanged-password-hash", user.PasswordHash);
                Assert.False(string.IsNullOrWhiteSpace(user.SecurityStamp));
            });
            Assert.NotEqual(users[0].SecurityStamp, users[1].SecurityStamp);
            if (history == "main") Assert.Equal("existing-main-stamp", users[0].SecurityStamp);
            Assert.Equal(20, await db.Database.SqlQueryRaw<int>("""
                SELECT CAST(max_length AS int) AS [Value] FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Orders') AND name = N'CurrencyCode'
                """).SingleAsync());
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*) AS [Value] FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Users') AND name = N'SessionVersion'
                """).SingleAsync());
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*) AS [Value] FROM sys.check_constraints
                WHERE name = N'CK_InventoryItems_Quantity_Nonnegative'
                """).SingleAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());

            // Re-running either deployment method must preserve existing data and login stamps.
            var stamps = users.Select(user => user.SecurityStamp).ToArray();
            await db.Database.MigrateAsync();
            await RunIdempotentScriptAsync(db);
            Assert.Equal(stamps, await db.Users.AsNoTracking().OrderBy(user => user.Email)
                .Select(user => user.SecurityStamp).ToArrayAsync());
            Assert.Equal(3, await db.Orders.CountAsync());
        }
        finally
        {
            if (!Regex.IsMatch(database, "^SalesHubMerge_[0-9a-f]{32}$"))
                throw new InvalidOperationException("Unexpected scratch database");
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task ApplyHistoricalMigrationAsync(ApplicationDbContext db, string id)
    {
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!);
        var generator = db.GetService<IMigrationsSqlGenerator>();
        var model = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
        foreach (var command in generator.Generate(migration.UpOperations, model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        var insert = db.GetService<IHistoryRepository>().GetInsertScript(new HistoryRow(id, "8.0.0"));
        await db.Database.ExecuteSqlRawAsync(insert);
    }

    private static async Task RunIdempotentScriptAsync(ApplicationDbContext db)
    {
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        await db.Database.OpenConnectionAsync();
        try
        {
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static Task SeedMigrationDataAsync(ApplicationDbContext db, bool hasCurrency, bool hasStamp)
    {
        var currencyColumn = hasCurrency ? ", CurrencyCode" : "";
        var currencyValue = hasCurrency ? ", CurrencyCode" : "";
        var stampColumn = hasStamp ? ", SecurityStamp" : "";
        var firstStamp = hasStamp ? ", N'first-existing-stamp'" : "";
        var secondStamp = hasStamp ? ", N'second-existing-stamp'" : "";
        var sql = $"""
            INSERT INTO Users (FullName, Email, PasswordHash, AccountType, CreatedAt{stampColumn})
            VALUES (N'First', N'first@migration.test', N'unchanged-password-hash', N'Owner', GETDATE(){firstStamp}),
                   (N'Second', N'second@migration.test', N'unchanged-password-hash', N'Staff', GETDATE(){secondStamp});
            INSERT INTO Stores (Name, CreatedAt, CurrencyCode, InvoiceThankYou)
            VALUES (N'VND store', GETDATE(), N'VND', N'Thanks'),
                   (N'USD store', GETDATE(), N'USD', N'Thanks'),
                   (N'EUR store', GETDATE(), N'EUR', N'Thanks');
            INSERT INTO Orders (StoreId, Code, CustomerName, Status, PaymentStatus, PaymentMethod, Subtotal, Discount, Total, CreatedAt{currencyColumn})
            SELECT Id, CONCAT(N'OLD-', CurrencyCode), N'Historical customer', N'Completed', N'Paid', N'Cash', 12.50, 0, 12.50, GETDATE(){currencyValue}
            FROM Stores;
            """;
        return db.Database.ExecuteSqlRawAsync(sql);
    }

    private sealed class SqlServerTheoryAttribute : TheoryAttribute
    {
        public SqlServerTheoryAttribute()
        {
            if (Environment.GetEnvironmentVariable("SALESHUB_TEST_SQLSERVER") != "1")
                Skip = "Set SALESHUB_TEST_SQLSERVER=1 on Windows with LocalDB to test both migration histories.";
        }
    }
}
