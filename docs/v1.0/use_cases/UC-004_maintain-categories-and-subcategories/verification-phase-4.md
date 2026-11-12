# UC-004 Phase 4 — Verification

**Status:** Approved, implemented, and verified 12 November 2026.

## Agreed rules

- Owners and editors can stop tracking a subcategory under Income, Savings, Debt, or an ordinary category. Viewers cannot.
- Stopping tracking retains the subcategory, its parent, name, comment, and any SavingsPlan. Reads continue to include it and show its tracking-stop timestamp.
- Owners/editors cannot edit metadata after the subcategory has stopped tracking.
- Deletion requires the current subcategory row version. A stale request conflicts and changes nothing. Repeating deletion with the current version succeeds without changing the original timestamp.
- Tracking-stop time is stored as timezone-aware `datetimeoffset`. This phase does not implement parent deletion or budget-selection filtering.

## Red/green evidence

The first focused Domain test build failed because `ProfileSubcategory.StopTracking` did not exist. The Domain behavior was added, including retained-state metadata rejection and first-timestamp preservation on repeat; the final Domain suite passed.

The LocalDB/API tests verified the new migration, current-version deletion and repeated deletion, stale-version conflict, metadata-edit rejection, owner/editor success, viewer and antiforgery denial, foreign-profile rejection, persistence-failure handling, and retained data under all four parent kinds. The migration-upgrade test started at the prior subcategory migration, inserted existing child/settings rows, applied the new migration, and verified those rows and active status were preserved. A synthetic database trigger rejected a tracking-stop write; the API did not confirm success and the stored subcategory remained active.

The component and browser tests exercised the writer action, retained comment/status display, and fresh-read persistence after reload. Both category browser workflows passed together.

## Commands and results

| Check | Result |
| --- | --- |
| `dotnet build PersonalFinance.slnx --no-restore -m:1 -nr:false --verbosity:minimal` | Passed — 0 warnings, 0 errors |
| `dotnet tests/PersonalFinance.Domain.Tests/bin/Debug/net10.0/PersonalFinance.Domain.Tests.dll -noLogo` | Passed — 32 tests |
| `dotnet tests/PersonalFinance.Application.Tests/bin/Debug/net10.0/PersonalFinance.Application.Tests.dll -noLogo` | Passed — 49 tests |
| `dotnet tests/PersonalFinance.Client.Tests/bin/Debug/net10.0/PersonalFinance.Client.Tests.dll -noLogo` | Passed — 83 tests |
| `dotnet tests/PersonalFinance.IntegrationTests/bin/Debug/net10.0/PersonalFinance.IntegrationTests.dll -noLogo` | Passed — 107 tests, including isolated LocalDB migration, rollback, and failed tracking-stop checks |
| `dotnet tests/PersonalFinance.EndToEndTests/bin/Debug/net10.0/PersonalFinance.EndToEndTests.dll -class PersonalFinance.EndToEndTests.ProfileCategoriesBrowserTests -noLogo` | Passed — 2 Chrome workflows |
| `dotnet publish src/PersonalFinance.Client/PersonalFinance.Client.csproj -c Release --no-restore -m:1 -nr:false --verbosity:minimal` | Succeeded; optional `wasm-tools` optimization workload was not installed |
| `git diff --check` | Passed |

An earlier run of the existing category browser workflow timed out waiting for a newly created category to appear. A clean rerun passed, and the complete two-test category browser class passed afterward. No other browser engines or deployed origins were tested.

## Remaining boundaries

Phase 4 does not stop categories, block children through parent deletion, or filter deleted items from future budget periods. Parent/child deletion races remain a Phase 5 decision. Financial history and report retention remain gated by the later financial use cases.
