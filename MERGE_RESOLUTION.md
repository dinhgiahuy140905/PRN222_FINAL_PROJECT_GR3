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

Pending implementation and verification.
