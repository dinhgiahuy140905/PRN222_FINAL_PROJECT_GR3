# Branch integration and conflict resolution

## Baseline and scope

- Integration branch: `codex/integrate-unmerged-fixes`.
- Main baseline: `887c139` (`origin/main`, fetched on 2026-11-11).
- Incoming tip: `ae07dce` (`origin/dinhgiahuy140905/fix-role-ui-actions`).
- The incoming branch already contains `fix-password-sessions` (10 commits) and
  `fix-audit-issues` (52 additional commits), followed by 16 role/UI commits.
  Merge this tip once to preserve all 78 commits and avoid repeated conflicts.
- Keep source branches and remote `main` intact while validating the integration.

## Resolution decisions

| Area | Decision | Verification |
| --- | --- | --- |
| Authentication | Keep main's `SecurityStamp` as the single active session and reset-token version. Use one cookie validation handler; adapt incoming password recovery and tests to the same stamp. | Password changes and recovery revoke other sessions; the current session survives an ordinary password change; reset links expire and cannot be reused. |
| Order currency | Keep one `Order.CurrencyCode` property and one assignment when creating orders. Preserve currency snapshots, currency-specific reports, and guards against changing a populated store's currency. | Build, order/report integration tests, historical-currency tests. |
| Database history | Retain both branches' migration IDs. Guard the overlapping currency additions, backfill only missing currency values, and add a reconciliation migration to converge schema and retire `SessionVersion`. | Isolated SQL Server databases upgraded from a clean database, the main history, and the incoming history; no application database changes. |
| Order validation | Preserve main's visible item errors and incoming monetary precision/formatting, with the existing safe product-option rendering. | Invalid-order and browser regression tests. |
| Display settings | Enable incoming date/number preferences because the incoming branch implements their renderers; keep validation and currency guards. | Display-preference and settings tests. |
| Roles and notifications | Preserve incoming permission checks, payment filters, help, and store-scoped notifications. Retain main's low-stock-count fix. | Role, notification, dashboard, and payment tests. |

## Execution and completion criteria

1. Commit this plan before merging.
2. Merge the incoming tip without automatically committing; resolve each textual
   conflict and inspect automatically merged authentication, order, and schema code.
3. Build and run the application tests; verify migrations against disposable
   databases and run available browser checks.
4. Commit the coherent merge, add any focused regression coverage, and record the
   actual verification results here.
5. Confirm both source tips are ancestors of the integration branch, no conflict
   markers remain, and `git status --porcelain` is empty.

## Verification results

Completed on 2026-11-11:

- `dotnet build PRN222_SalesManagement.sln --nologo --verbosity minimal`:
  passed with zero warnings and errors.
- Full suite with `SALESHUB_TEST_SQLSERVER=1` and `SALESHUB_TEST_BROWSER=1`:
  **45 passed, 0 failed, 0 skipped**. Results are generated under
  `SalesManagement.Tests/TestResults/` (ignored by Git).
- Real Chrome role checks cover Owner/Staff actions, permission boundaries,
  notifications, enabled display preferences, and the disabled language selector.
- Standalone Chrome display-preference checks and order-form regression checks
  for VND, USD/en-US, and EUR/vi-VN all passed, including safe product rendering,
  add/remove/reindex operations, totals, and discounts.
- `dotnet ef migrations has-pending-model-changes --project SalesManagement.Web
  --startup-project SalesManagement.Web --no-build`: no pending model changes.
- Three isolated SQL Server migration cases passed: empty database via idempotent
  SQL, main-only history, and incoming history. Existing order currencies/amounts,
  password hashes, and valid stamps are preserved. The schema converges to one
  nonnullable `nvarchar(10)` order currency column, the stock constraint, and no
  runtime `SessionVersion` column. Repeated migrations/scripts preserve data.
- `SalesManagement.Tests/verify-currency-migration.ps1`: VND/USD/EUR backfill and
  historical amounts passed. Every database used for validation was disposable;
  the application's configured database was not migrated.
- Added regression tests ensure changing a password in Settings invalidates an
  already issued recovery link and that dashboard low-stock counts match the
  product filter at zero/threshold/above-threshold and inactive-product boundaries.

## Migration compatibility and rollout

Both existing migration IDs for order currency remain in history. Their additions
now check whether the column already exists; only missing/blank snapshots are
backfilled. `AddOrderCurrencySnapshot` owns column removal on rollback;
`AddCurrencyCodeToOrder.Down` no longer removes a column that an earlier migration
still requires. Historical designer files remain unchanged.

`20261111054258_ReconcileBranchMigrations` aligns the final schema, initializes
blank security stamps, and drops the obsolete session-version counter. It preserves
nonempty stamps and every nonempty currency snapshot. New password recovery tokens
use a separate SecurityStamp-based Data Protection purpose. Sessions and recovery
links from the SessionVersion branch must be reissued after upgrading; valid main
sessions with an existing nonempty stamp are preserved.

Apply the complete migration chain before starting this version of the app. A
currency snapshot missing from older data can only be filled from the store's
current currency; amounts are never converted. See `CURRENCY_VALIDATION_NOTES.md`
for that existing historical-data limitation.

## Commits and handoff

- `a9a899b`: record the integration plan and resolution decisions.
- `624bb86`: merge the incoming branch and reconcile overlapping main changes.
- The regression-coverage commit adds migration convergence tests, merge-specific
  behavior checks, the browser assertions, and this verification record.

After verification, fast-forward local `main` to the integration branch, keeping
the integration branch as a named reference. No remote branch is pushed, merged,
or deleted by this integration task.
