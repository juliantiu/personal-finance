# UC-019 Phase 2 — Verification

Verified **10 November 2026** for configuring additional currencies after profile creation.

## Behavior covered

- Owners and editors can enable or disable the supported alternative; viewers cannot save, including through the API.
- The saved main currency is fixed. Zero additional currencies or one supported currency is valid; unsupported, multiple, or main-as-additional choices are rejected.
- Successful writes update the saved state and configuration version. Stale writes return a conflict without overwriting current settings.
- The PUT endpoint requires the existing BFF session and antiforgery proof, returns no-store responses, and does not expose inaccessible profiles.
- Membership and configuration-version changes are committed atomically. A synthetic SQL trigger failure leaves both unchanged.
- The client preserves the draft on failed or uncertain writes, reports conflicts, and reloads the server's state after success.

## Red/green evidence

- **Red:** Temporarily unmapped the new PUT route and ran the focused currency Integration class. The three write scenarios failed with **405 Method Not Allowed** instead of save, validation failure, or persistence failure. The existing read-only settings test remained green.
- **Green:** Restored the route and reran the focused class: **4 passed, 0 failed**. The profile Integration namespace later passed **33 tests**.
- Domain, Application, and client tests exercise validation, current-account orchestration, role-based controls, save/reload, and failure feedback.

## Commands and results

Tests were invoked through the xUnit v3 in-process runner because `dotnet test` could not connect to its test-host named pipe in the sandbox. LocalDB and Playwright runs used elevated execution; each database was uniquely owned by the test fixture.

| Command | Result |
| --- | --- |
| `dotnet tests/PersonalFinance.Domain.Tests/bin/Debug/net10.0/PersonalFinance.Domain.Tests.dll -noAutoReporters` | **22 passed** |
| `dotnet tests/PersonalFinance.Application.Tests/bin/Debug/net10.0/PersonalFinance.Application.Tests.dll -noAutoReporters` | **46 passed** |
| `dotnet tests/PersonalFinance.Client.Tests/bin/Debug/net10.0/PersonalFinance.Client.Tests.dll -noAutoReporters` | **65 passed** |
| `dotnet tests/PersonalFinance.IntegrationTests/bin/Debug/net10.0/PersonalFinance.IntegrationTests.dll -class PersonalFinance.IntegrationTests.Profiles.ProfileCurrencySettingsTests -noAutoReporters` | **4 passed** |
| `dotnet tests/PersonalFinance.IntegrationTests/bin/Debug/net10.0/PersonalFinance.IntegrationTests.dll -namespace PersonalFinance.IntegrationTests.Profiles -noAutoReporters` | **33 passed** |
| `$env:PERSONAL_FINANCE_TEST_BROWSER = 'edge'; dotnet tests/PersonalFinance.EndToEndTests/bin/Debug/net10.0/PersonalFinance.EndToEndTests.dll -class PersonalFinance.EndToEndTests.ProfileCurrencySettingsBrowserTests -noAutoReporters` | **1 passed** |
| `dotnet build PersonalFinance.slnx --no-restore --verbosity minimal -m:1 -nr:false` | **Succeeded, 0 errors**; 13 existing Integration test warnings (duplicate using and xUnit cancellation-token analyzer warnings) |
| `dotnet publish src/PersonalFinance.Client/PersonalFinance.Client.csproj -c Release --no-restore --verbosity minimal -m:1 -nr:false` | **Succeeded**; publish reported the optional `wasm-tools` workload was not installed and published without optimization |
| `git diff --check` | Passed; Git reported configured LF-to-CRLF normalization notices |

The new migration was exercised by the profile Integration and browser fixtures. The migration-upgrade regression verifies existing account, profile-access and settings data are preserved and the new version starts at zero. No non-test database was migrated.

## Limits

This phase does not enforce disabling rules against approved financial records or imported source currencies; those checks remain Phase 3 and require UC-009–UC-011 records. No conversion, historical relabeling, or main-currency change was added.
