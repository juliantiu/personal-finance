# UC-001 phase 6 verification

Verified on 30 October 2026. Scope: registration, email confirmation, resend, and their failure paths. OpenIddict sign-in and later profile/financial workflows remain outside this slice.

## Implementation and evidence

The API integration tests use the real controllers, Mediator pipeline, Identity adapter, EF Core migrations, and SQL Server LocalDB. Each host owns a uniquely named `PersonalFinance_UC001_Test_<guid>` database. Disposal checks the exact database name before deleting it. The fixture explicitly replaces the developer connection string, uses ephemeral Data Protection keys, and replaces email transport with in-memory capture. It does not send messages to smtp4dev or Azure.

The browser tests serve the published Release Blazor client and the real API on separate loopback origins. Playwright drives Chromium; real HTTP/CORS, client validation, Identity persistence, and confirmation run together. Only email delivery is replaced with capture. One browser test deliberately aborts a registration request to verify connection-error recovery.

| Suite | Passing cases | Evidence |
| --- | ---: | --- |
| Application | 28 | Existing validation, orchestration, acknowledgement, cancellation and error behavior |
| Client components | 10 | Existing form/confirmation checks plus two fragment-navigation regressions |
| Integration | 24 | 12 existing configuration/email cases and 12 API/LocalDB cases |
| Browser | 3 | Persisted registration/confirmation/replay; invalid-link resend recovery; narrow-screen validation, connection failure and keyboard retry |
| Total | 65 | No skipped cases |

New persistence checks cover stable account IDs, password hashing, initial unverified state, invalid-input rejection without writes, repeated/concurrent normalized-email registration, database-enforced uniqueness independent of username, and distinct dot/plus addresses. Confirmation checks cover valid links, replay, wrong-account/tampered/expired tokens, and rejection even after verification. Expiry uses a real protected Identity payload dated 25 hours ago, with a 23-hour positive control under the unchanged 24-hour configuration. Tests also cover retained accounts after delivery failure, resend recovery, generic acknowledgements for unknown/verified recipients, and unavailable routes when configuration is missing.

Confirmation responses do not issue authentication cookies; the browser remains without cookies. This is evidence for this slice, not an authorization test for financial/profile features that do not exist yet.

## Defect and red/green cycle

The browser recovery scenario exposed a confirmation page that processed its URL only during initialization. Opening a fresh confirmation link in the already-open page left the old result displayed.

Before the production fix, two focused bUnit cases failed because only one request was sent instead of two: invalid link followed by valid link, and valid link followed by invalid link. The component now handles new confirmation fragments, clears previous feedback, ignores stale responses, and unsubscribes from navigation events on disposal. Both regression cases and the real-browser recovery/replay scenarios passed after the fix. The API, Application, Domain, database schema, and email providers required no production changes.

Other new tests passed against existing behavior. Initial dependency/build issues, the empty test-project runner configuration, a missing ICU `.dat` MIME mapping in the browser test server, and incorrect feedback selectors were setup issues; they are not claimed as production red/green evidence. The unused Domain test placeholder now has discovery disabled until actual domain tests and a runner are added. The EndToEnd project now uses the selected xUnit v3 runner and Playwright.

## Reproduce

Prerequisites: Windows, .NET 10 SDK, SQL Server LocalDB with the `MSSQLLocalDB` instance available, and permission to create/drop the fixture-owned test databases. Package/browser downloads require network access on first setup. No developer user-secrets or live email settings are required.

Run from the repository root in PowerShell, stopping if any command fails:

```powershell
dotnet restore PersonalFinance.slnx
dotnet build PersonalFinance.slnx --no-restore
dotnet publish src/PersonalFinance.Client -c Release --no-restore
pwsh tests/PersonalFinance.EndToEndTests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test --solution PersonalFinance.slnx --no-build
```

Republish after any client change: browser tests intentionally exercise the published artifact. Set `PERSONAL_FINANCE_TEST_CLIENT` to an absolute published `wwwroot` path to test a different client artifact. Missing LocalDB, browser binaries, or published files fail visibly rather than silently skipping tests. Abrupt process termination can leave a fixture database behind; normal asynchronous disposal deletes it.

## Environment and limits

- Verified with SDK 10.0.401, runtime 10.0.12, and Playwright 1.56.0 Chromium on Windows.
- Full solution build succeeded with zero warnings/errors. Release client publish succeeded, resolving the previously recorded inability to complete a WebAssembly build on this host. Publishing notes that the optional `wasm-tools` workload is absent; native WebAssembly optimization was not verified.
- Browser coverage is selected Chromium workflows, including a 390 × 844 viewport and keyboard submission, not a comprehensive accessibility audit or cross-browser certification.
- Azure SQL, deployed hosting, managed identity permissions, and live ACS email delivery were not exercised. Existing production deployment gates still apply.
- No new financial/profile access or OpenIddict behavior was implemented or tested.
