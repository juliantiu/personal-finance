# UC-004 Phase 5 — Verification

**Status:** Approved, implemented, and verified 12 November 2026.

## Agreed rules

- Owners and editors may stop tracking ordinary categories. Income, Savings, and Debt categories remain protected by stable kind, including after rename.
- A category's timezone-aware tracking-stop timestamp is retained. Repeating deletion with its current row version succeeds without changing the original timestamp; a stale version conflicts.
- Parent deletion changes only the category lifecycle fact. Children remain visible, and their individual tracking-stop timestamps are preserved.
- A child is unavailable for new tracking if either its own timestamp or its parent's timestamp is set. Children of a deleted parent cannot be created or edited.
- Writers may explicitly stop an active child's tracking after the parent has stopped; that operation sets the child's independent timestamp.
- Parent retirement and child creation/metadata updates serialize on the parent category. A child is created only if that write commits before parent retirement.

## Red/green evidence

The focused Domain test was added before the category lifecycle behavior. Its initial build failed because `ProfileCategory.StopTracking` did not exist. After adding the ordinary-only operation and timestamp retention, all 6 category Domain tests passed; the full Domain suite passed with 33 tests.

The LocalDB/API tests verified protected categories before and after renaming, viewer denial, editor/owner access, antiforgery rejection, stale and repeated deletes, database enforcement of system-category protection, retained parent/child data, independent child timestamps, blocked child creation/editing, child deletion below a retired parent, migration preservation, and concurrent parent deletion/child creation. The focused category mutation class passed all 5 tests after the final assertions were added.

The component test verifies that parent-retired children have no edit/create controls while an active child still offers its own stop-tracking action. The Chrome workflow verified category and child status after reload. The historical subcategory migration fixture was changed to seed the old schema through SQL because the current EF model now includes the later category column.

## Commands and results

| Check | Result |
| --- | --- |
| `dotnet build PersonalFinance.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity:minimal` | Passed — 0 warnings, 0 errors |
| `dotnet tests/PersonalFinance.Domain.Tests/bin/Debug/net10.0/PersonalFinance.Domain.Tests.dll -noLogo` | Passed — 33 tests |
| `dotnet tests/PersonalFinance.Application.Tests/bin/Debug/net10.0/PersonalFinance.Application.Tests.dll -noLogo` | Passed — 50 tests |
| `dotnet tests/PersonalFinance.Client.Tests/bin/Debug/net10.0/PersonalFinance.Client.Tests.dll -noLogo` | Passed — 84 tests |
| `dotnet tests/PersonalFinance.IntegrationTests/bin/Debug/net10.0/PersonalFinance.IntegrationTests.dll -noLogo` | Passed — 109 tests |
| Focused LocalDB `ProfileCategoryMutationTests` after final test edits | Passed — 5 tests |
| Focused LocalDB `ProfileSubcategoryTests` | Passed — 4 tests |
| Focused LocalDB `ProfileCategoriesTests` | Passed — 6 tests |
| Focused LocalDB `CreateProfileTests` | Passed — 15 tests |
| Focused Chrome `ProfileCategoriesBrowserTests` | Passed — 3 workflows |
| `dotnet publish src/PersonalFinance.Client/PersonalFinance.Client.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity:minimal` | Succeeded; optional `wasm-tools` size optimization was unavailable |
| `git diff --check` | Passed |

The standalone integration-project rebuild reported 20 existing analyzer/duplicate-using warnings. The final solution build reported none. Browser verification used Chrome; other engines and deployed origins were not tested.

## Remaining boundaries

Phase 5 does not filter deleted items from budget-period selection or verify financial-history/report behavior. Those checks remain in the dependency-gated Phase 7 after the owning financial use cases exist.
