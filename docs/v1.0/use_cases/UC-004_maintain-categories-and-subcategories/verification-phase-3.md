# UC-004 Phase 3 — Verification

**Status:** Approved, implemented, and verified 11 November 2026.

## Scope verified

- Owners and editors create and edit subcategories under ordinary, Income, Savings, and Debt categories. Viewers read saved names and comments.
- Names are trimmed, nonblank, and at most 100 characters after trimming. Duplicate names remain distinct by subcategory ID. Optional comments are at most 2,000 characters; whitespace-only comments are absent and meaningful interior line breaks are preserved.
- A subcategory keeps its ID and parent when metadata changes. Requests recheck current writer access and require the parent to belong to the requested profile. Unavailable or foreign parents and subcategories return no resource data.
- Each edit uses a per-subcategory row version. A stale edit conflicts and the browser retains the draft until the writer reloads the current version.
- Creating a Savings subcategory also creates one empty SavingsPlan in the same database transaction. Goal and reset note stay unset. No savings goals, balances, corrections, allocations, or transactions are added.
- An uncertain create response requires a fresh review before the browser enables retry. Reads are no-store and mutations require antiforgery.

## Red/green evidence

The first focused Domain test build failed with `CS0103` because `ProfileSubcategory` did not exist. After adding the Domain behavior, the six focused cases passed: identity/parent stability, name/comment normalization and limits, and required parent identity.

The LocalDB integration tests first passed against the migration, then were extended to cover duplicate names under one parent and rerun against the final migration. The final focused Phase 3 integration run passed all 3 tests. It verified creation under all four category kinds, empty SavingsPlan creation, atomic rollback when a database trigger rejects plan insertion, viewer/editor access, antiforgery, field validation, mixed-profile parent rejection, row-version conflict, and migration preservation.

The component tests cover nested read-only review, create/reload, stale-edit draft preservation, and review before retrying an uncertain create. The focused category page class passed 19 tests. The Playwright category workflow passed in Chrome with a Savings subcategory create, edit, navigation away, and reload.

## Commands and results

Test assemblies were run directly because `dotnet test` could not connect to xUnit's Microsoft Testing Platform named pipe in this environment. LocalDB tests, the Blazor WebAssembly build, and browser automation ran outside the sandbox because their required LocalDB runtime, task host, and browser processes were unavailable inside it.

| Command | Result |
| --- | --- |
| `dotnet tests/PersonalFinance.Domain.Tests/bin/Debug/net10.0/PersonalFinance.Domain.Tests.dll -noLogo` | Passed — 29 tests |
| `dotnet tests/PersonalFinance.Application.Tests/bin/Debug/net10.0/PersonalFinance.Application.Tests.dll -noLogo` | Passed — 49 tests |
| `dotnet tests/PersonalFinance.Client.Tests/bin/Debug/net10.0/PersonalFinance.Client.Tests.dll -noLogo` | Passed — 82 tests |
| `dotnet tests/PersonalFinance.IntegrationTests/bin/Debug/net10.0/PersonalFinance.IntegrationTests.dll -noLogo` | Passed — 106 tests, including migration preservation and LocalDB rollback coverage |
| `dotnet tests/PersonalFinance.IntegrationTests/bin/Debug/net10.0/PersonalFinance.IntegrationTests.dll -class PersonalFinance.IntegrationTests.Profiles.ProfileSubcategoryTests -noLogo` | Passed against the final migration — 3 Phase 3 tests |
| `dotnet tests/PersonalFinance.EndToEndTests/bin/Debug/net10.0/PersonalFinance.EndToEndTests.dll -class PersonalFinance.EndToEndTests.ProfileCategoriesBrowserTests -noLogo` | Passed — 1 focused Chrome browser workflow |
| `dotnet publish src/PersonalFinance.Client/PersonalFinance.Client.csproj -c Release --no-restore -m:1 -nr:false -v:minimal` | Succeeded; publish reported that the optional `wasm-tools` workload was not installed |
| `dotnet build PersonalFinance.slnx --no-restore -m:1 -nr:false -v:quiet` | Succeeded — 0 warnings, 0 errors |
| `git diff --check` | Passed |

## Remaining boundaries

Phase 3 does not add deletion, reparenting, restoration, goal/reset-note editing, or financial activity. Goal precision remains unresolved; no goal values are accepted in this phase. Parent-retirement/child-write races and deletion concurrency remain gates for Phases 4–5. Historical balance/report verification remains dependency-gated by Phase 7.
