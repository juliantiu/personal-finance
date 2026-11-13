# UC-004 Phase 6 — Local acceptance verification

**Status:** Approved, implemented, and verified 12 November 2026. `docs/v1.0/` was the highest numeric version directory. This phase verifies the category-management behavior delivered in Phases 1–5; it does not verify the financial-history obligations gated by Phase 7.

## Acceptance coverage

- The category browser workflows cover owner review, category create/edit, subcategory create/edit/stop, ordinary-category stop, retained status after reload, profile navigation, retry after a network failure, viewer review, and denial after a grant is removed.
- The primary category workflow creates two ordinary categories with the same display name, then verifies both render in the profile overview while retaining all five category identities. It also verifies the system category rename and subcategory edit survive reload.
- At a 390 × 844 viewport, the category edit form has no horizontal overflow. The category name is saved by keyboard navigation: Tab reaches the save button and Enter activates it.
- The subcategory edit browser flow waits for a successful API response before navigating away, then verifies the saved value after a fresh page load.
- Existing profile creation/currency, profile-list, and account browser workflows were rerun. Integration tests cover profile access changes, session and antiforgery boundaries, validation/conflicts, failed writes, migration preservation, rollback, and parent/child concurrency as recorded in the earlier phase verification documents.

## Results

| Check | Result |
| --- | --- |
| Domain test assembly, `-noLogo` | Passed — 33 tests |
| Application test assembly, `-noLogo` | Passed — 50 tests |
| Client test assembly, `-noLogo` | Passed — 84 tests |
| Full Integration test assembly, `-noLogo` | Passed on final run — 109 tests |
| `ProfileCategoriesBrowserTests` | Passed — 3 Chrome workflows |
| `CreateProfileBrowserTests` | Passed — 1 Chrome workflow |
| `ProfileListBrowserTests` | Passed — 2 Chrome workflows |
| `AccountBrowserTests` | Passed — 7 Chrome workflows |
| Solution build | Passed — 0 warnings, 0 errors |
| Release client publish | Succeeded; optional `wasm-tools` optimization workload is not installed |
| `git diff --check` | Passed |

Commands were run from the repository root. Test-host commands were run outside the sandbox because the xUnit Microsoft Testing Platform named pipe is blocked inside it. Browser workflows and LocalDB integration tests used synthetic accounts and isolated test databases; no development database was migrated.

```powershell
dotnet tests/PersonalFinance.Domain.Tests/bin/Debug/net10.0/PersonalFinance.Domain.Tests.dll -noLogo
dotnet tests/PersonalFinance.Application.Tests/bin/Debug/net10.0/PersonalFinance.Application.Tests.dll -noLogo
dotnet tests/PersonalFinance.Client.Tests/bin/Debug/net10.0/PersonalFinance.Client.Tests.dll -noLogo
dotnet tests/PersonalFinance.IntegrationTests/bin/Debug/net10.0/PersonalFinance.IntegrationTests.dll -noLogo
dotnet test tests/PersonalFinance.EndToEndTests --no-restore --filter FullyQualifiedName~ProfileCategoriesBrowserTests --verbosity minimal
dotnet tests/PersonalFinance.EndToEndTests/bin/Debug/net10.0/PersonalFinance.EndToEndTests.dll -class PersonalFinance.EndToEndTests.CreateProfileBrowserTests -noLogo
dotnet tests/PersonalFinance.EndToEndTests/bin/Debug/net10.0/PersonalFinance.EndToEndTests.dll -class PersonalFinance.EndToEndTests.ProfileListBrowserTests -noLogo
dotnet tests/PersonalFinance.EndToEndTests/bin/Debug/net10.0/PersonalFinance.EndToEndTests.dll -class PersonalFinance.EndToEndTests.AccountBrowserTests -noLogo
dotnet build PersonalFinance.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity:minimal
dotnet publish src/PersonalFinance.Client/PersonalFinance.Client.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity:minimal
git diff --check
```

## Red/green and limits

Phase 6 is an acceptance and regression phase. Existing behavior was exercised without manufacturing a production failure. The browser test was strengthened to wait for the subcategory update response before checking a fresh read; an initial assertion counted the nested subcategory along with categories and was corrected to reflect that locator scope. No production behavior gap was found, and no production code or schema changed.

The first full Integration run had 108 passing tests and one LocalDB startup failure in `AccountInfrastructureTests`. That isolated class then passed all 7 tests, and the complete Integration suite passed 109/109 on rerun. A sandboxed `dotnet test` attempt also failed before running tests because the runner could not connect to its named pipe; direct test-host execution outside the sandbox worked. These setup failures are not counted as intended red results.

Browser verification used Chrome only. Other browser engines and deployed origins were not tested. Phase 7 remains dependency-gated until its owning budget-period, transaction, obligation, correction, and reporting use cases are implemented and approved.
