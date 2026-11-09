# Run from the repository root after building. Requires SQL Server LocalDB and sqlcmd.
$ErrorActionPreference = 'Stop'
$databaseName = 'SalesHubCurrencyTest_' + [Guid]::NewGuid().ToString('N')
$connection = "Server=(localdb)\MSSQLLocalDB;Database=$databaseName;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True"
try {
    dotnet ef database update 20261106142544_AddUserSessionVersion --project SalesManagement.Web --startup-project SalesManagement.Web --no-build --connection $connection
    if ($LASTEXITCODE -ne 0) { throw 'Could not create pre-migration database' }
    $seedSql = @'
INSERT INTO Stores (Name, CreatedAt, CurrencyCode, InvoiceThankYou)
VALUES ('VND store', GETDATE(), 'VND', 'Thanks'),
       ('USD store', GETDATE(), 'USD', 'Thanks'),
       ('EUR store', GETDATE(), 'EUR', 'Thanks');
INSERT INTO Orders (StoreId, Code, CustomerName, Status, PaymentStatus, PaymentMethod, Subtotal, Discount, Total, CreatedAt)
SELECT Id, CONCAT('OLD-', Id), 'Old customer', 'Completed', 'Paid', 'Cash', 12.50, 0, 12.50, GETDATE() FROM Stores;
'@
    sqlcmd -S '(localdb)\MSSQLLocalDB' -d $databaseName -E -b -Q $seedSql
    if ($LASTEXITCODE -ne 0) { throw 'Could not seed old orders' }
    dotnet ef database update --project SalesManagement.Web --startup-project SalesManagement.Web --no-build --connection $connection
    if ($LASTEXITCODE -ne 0) { throw 'Currency migration failed' }
    $verifySql = @'
IF (SELECT COUNT(*) FROM Orders) <> 3 THROW 51000, 'Old orders were lost', 1;
IF EXISTS (SELECT 1 FROM Orders o JOIN Stores s ON s.Id=o.StoreId WHERE o.CurrencyCode IS NULL OR o.CurrencyCode<>s.CurrencyCode)
    THROW 51000, 'Incorrect currency backfill', 1;
UPDATE Stores SET CurrencyCode='EUR' WHERE CurrencyCode='USD';
IF NOT EXISTS (SELECT 1 FROM Orders WHERE CurrencyCode='USD' AND Total=12.50)
    THROW 51000, 'Order snapshot or amount changed', 1;
SELECT 'PASS: VND/USD/EUR backfill preserves old orders and amounts' AS Result;
'@
    sqlcmd -S '(localdb)\MSSQLLocalDB' -d $databaseName -E -b -Q $verifySql
    if ($LASTEXITCODE -ne 0) { throw 'Backfill verification failed' }
} finally {
    # Only remove the scratch database created by this invocation.
    if ($databaseName -notmatch '^SalesHubCurrencyTest_[0-9a-f]{32}$') { throw 'Unexpected database name' }
    sqlcmd -S '(localdb)\MSSQLLocalDB' -d master -E -b -Q "IF DB_ID(N'$databaseName') IS NOT NULL BEGIN ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName]; END"
}
