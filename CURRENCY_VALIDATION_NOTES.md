# Currency snapshots and order validation

Orders store the shop's currency at creation. Submitted currency values are ignored.
Invoices, order details/lists and recent orders use that snapshot. Product prices and
the order form use the shop's current currency. VND displays zero decimals; USD/EUR
display two, using Vietnamese number separators.

Changing currency is rejected once a shop has any products (including inactive ones)
or orders. Empty shops can still change currency. Reports let users select one
currency, scoped to the current shop. Dashboard revenue only sums completed orders
in the shop's current currency, which is shown beside the revenue label.

## Migration

Apply EF migrations before starting the updated app:

```powershell
dotnet ef database update --project SalesManagement.Web --startup-project SalesManagement.Web
```

The migration fills existing orders from each shop's current currency. It does not
convert amounts. If a shop changed currency before this migration, historical currency
cannot be reconstructed from the existing schema. Confirm those orders' currencies
against business records before migrating, then correct the snapshots if necessary.
Back up production data before applying schema changes.

## Verification

```powershell
dotnet test
node SalesManagement.Tests/Browser/order-create-xss.cjs
powershell -ExecutionPolicy Bypass -File SalesManagement.Tests/verify-currency-migration.ps1
```

The standalone migration check requires LocalDB/sqlcmd. It creates a temporary database
at the previous migration, seeds VND/USD/EUR shops and orders, applies the new migration,
checks the backfill and unchanged amounts, then removes only its temporary database.

Order validation uses an All summary outside the product-availability branch. Invalid
POSTs keep the form, unavailable selections, quantities and customer details visible.
Integration tests verify errors and unchanged orders/stock, followed by a valid retry.
