# UC-002 — Phase 6 verification

Date: **7 November 2026**. Scope: [approved implementation plan, Phase 6](implementation-plan.md). Phase 6 is complete. `docs/v1.0/` remains the latest and only version directory.

## Acceptance coverage

The existing phase tests collectively cover the Phase 6 acceptance matrix through the actual client, API, and test-owned LocalDB databases:

- Empty and populated profile lists; owned, editor, and view-only access; exclusion of private profiles; and current role projection.
- GBP and USD main currency choices, optional additional currency, trimmed names, and the complete Income/Savings/Debt, Cash, IOU, and IOU REPAYMENT setup.
- Reopening and switching profiles, deep links, browser history, profile access revocation, and generic handling of inaccessible profiles.
- Invalid or missing antiforgery proof, rejected and uncertain creation outcomes, expired/revoked sessions, protected-content clearing, and sign-out behavior.
- Existing account registration/confirmation and BFF sign-in, refresh, and sign-out regressions.

The relevant cases were already introduced with red/green evidence in [Phase 1](verification.md), [Phase 2](verification-phase-2.md), [Phase 3](verification-phase-3.md), [Phase 4](verification-phase-4.md), and [Phase 5](verification-phase-5.md). Phase 6 found no missing acceptance case requiring additional implementation or tests. Integration fixtures use synthetic accounts and isolated LocalDB databases; migrations were not applied to the ordinary development database.

## Results

| Check | Result |
| --- | --- |
| Domain | **18 passed**, 0 failed, 0 skipped |
| Application | **43 passed**, 0 failed, 0 skipped |
| Client | **57 passed**, 0 failed, 0 skipped |
| Integration / LocalDB | **93 passed**, 0 failed, 0 skipped |
| EndToEnd / Chrome | **9 passed**, 0 failed, 0 skipped |
| EndToEnd / Edge | **9 passed**, 0 failed, 0 skipped |
| Solution build | Succeeded; 0 warnings, 0 errors |
| Release client publish | Succeeded; optional `wasm-tools` optimization workload is absent |
| `git diff --check` | Passed |

The first parallel run of the Domain, Application, Client, and Integration suites caused two LocalDB integration failures: one migration timeout and one HTTP 500. Running Integration alone passed all 93 tests. The first browser run also contended on LocalDB while xUnit ran browser hosts concurrently. With `XUNIT_MAXPARALLELTHREADS=1`, all 9 EndToEnd workflows passed in Chrome; the serial Chrome and Edge matrix runs each passed all 9 workflows.

Playwright WebKit 26.0 / build 2215 was initially absent and was installed for this verification. On its subsequent run, **4 of 9 passed and 5 failed** because the browser did not show the initial “Create your account” page; the fixture reported no page or console errors. This is not a WebKit pass. Brave is not installed, so it was not run. Firefox was not rerun because its startup failure is documented in the UC-020 verification; it remains an environment limitation. WebKit is browser-engine coverage and does not establish actual Safari coverage.

The first browser run against an older Release client produced two login-return failures. After a fresh Release publish, those failures changed to LocalDB timeouts in concurrently running browser hosts. The serial Chrome and Edge runs against the fresh client passed. Those initial failures are not treated as product regressions or red states.

## Repeatable commands

Run from the repository root in PowerShell with .NET SDK `10.0.403`, SQL Server LocalDB, and Chrome/Edge installed. The Microsoft.Testing.Platform runner requires execution outside this sandbox on this machine because it cannot connect to its named pipe inside the sandbox.

```powershell
dotnet build PersonalFinance.slnx --no-restore --verbosity minimal
dotnet publish src/PersonalFinance.Client -c Release --no-restore --verbosity minimal
dotnet test --no-restore --project tests/PersonalFinance.Domain.Tests
dotnet test --no-restore --project tests/PersonalFinance.Application.Tests
dotnet test --no-restore --project tests/PersonalFinance.Client.Tests
dotnet test --no-build --no-restore --project tests/PersonalFinance.IntegrationTests
$env:XUNIT_MAXPARALLELTHREADS = '1'
dotnet test --no-build --no-restore --project tests/PersonalFinance.EndToEndTests
./tests/Run-BrowserMatrix.ps1 -Browsers @('chrome', 'edge')
git diff --check
```

The attempted three-engine matrix was:

```powershell
$env:XUNIT_MAXPARALLELTHREADS = '1'
./tests/Run-BrowserMatrix.ps1 -Browsers @('chrome', 'edge', 'webkit')
pwsh -NoProfile -File tests/PersonalFinance.EndToEndTests/bin/Debug/net10.0/playwright.ps1 install webkit
$env:PERSONAL_FINANCE_TEST_BROWSER = 'webkit'
dotnet test --no-build --no-restore --project tests/PersonalFinance.EndToEndTests
```

The matrix script logs are written under ignored `TestResults/browser-matrix/`. Environment variables are process-local and were not persisted.

## Checkpoint and remaining gates

The UC-002 workflow is demonstrated locally through Chrome and Edge, including the existing account and BFF regressions. The database migration and model consistency cases are included in the passing Integration suite. No migration, application code, or test changes were needed for Phase 6.

Firefox startup, the WebKit page-startup failures, and unavailable Brave remain browser verification limits. Actual Safari, Azure provisioning, production-origin cookie feasibility, and other deployed-origin checks remain release gates outside this phase.
