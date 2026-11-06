# UC-002 — Phase 1 verification

Date: **5 November 2026**. Scope: [approved implementation plan, Phase 1](implementation-plan.md). Phase 1 is complete. The evidence below records its original checkpoint; see [Phase 2 verification](verification-phase-2.md) for the subsequent work completed on 6 November 2026.

## Delivered behavior and layer flow

`/budget-profiles` is now a protected Blazor route and the working UC-020 post-login destination. It shows loading, empty, populated, and recoverable-error states. Each listed profile displays its name, main currency, and current Owner/Editor/View only role. Failed loads are not presented as an empty list. A `401` during loading clears client authentication and returns to login with the original local return path. Creation and opening controls are deferred until their destinations exist in later phases.

The browser sends its opaque session cookie with `GET /api/budget-profiles`. The Minimal API dispatches `ListAccessibleProfilesQuery` through Mediator. Its Application handler asks `ICurrentAccount` for the acting account; the API adapter resolves the existing BFF session and rechecks that the account exists with verified email. Temporary Identity login tickets and caller-selected account IDs cannot authorize the query. Missing/invalid sessions return `401` without an HTML redirect. Responses use `Cache-Control: no-store`.

The Infrastructure reader projects profile summaries through EF Core: original ownership or a current editor/view-only grant permits inclusion. The query starts from profile rows so each accessible profile appears once; role resolution gives ownership precedence. It does not retain profile roles in browser/session claims. Removing a grant changes the next query result.

Migration `20261105164916_AddProfileList` adds only `BudgetProfiles`, `ProfileAccess`, and `Currencies`, with the GBP/USD catalog, account/currency foreign keys, composite grant key, role constraint, and lookup indexes. Existing Identity/OpenIddict/session tables are retained. Phase 1 uses Infrastructure persistence records and Application read projections; no unused financial Domain behavior or Domain test runner was introduced. Profile construction, additional-currency memberships, categories, Cash, and tags remain Phase 2 work.

## Observed red/green evidence

- **Missing endpoint:** The first focused integration run had **2 failed, 3 passed**. Anonymous access returned `404` instead of `401`; a real persisted BFF session returned `404` instead of `200` with an empty list. After endpoint, current-account resolution, persistence, and migration work, both behaviors passed.
- **Shared access:** With the initial ownership-only reader, the expanded integration run had **1 failed, 10 passed**. A fixture containing an owned profile, editor grant, and view-only grant returned **1** profile instead of **3**. Adding current-grant projection made the case pass, including duplicate display names, private-profile exclusion, ignoring a supplied account ID, and immediate exclusion after grant removal.
- **Missing page:** After correcting test-fixture setup, all **9** new component cases failed because `/budget-profiles` still resolved to the missing-page view. The new protected page made the empty/populated/loading/error/retry and login-routing checks pass. Final component regression: **39 passed**.
- **Accessible browser feedback:** The first complete Chrome run had **6 passed, 1 failed**. The new workflow reached the signed-in list, verified owned/shared records and grant removal, but could not find the load-failure feedback by its accessible alert role. Adding an explicit `role="alert"` made the final **7** browser workflows pass. Component error tests also assert the alert role.
- **Additional boundary checks:** Application tests verify current-account forwarding, cancellation propagation, rejection before querying for anonymous callers, and dependency failures propagating rather than becoming empty results. Integration tests cover tampered, expired, revoked, unverified, and deleted-account sessions; an actual temporary Identity login cookie; disabled authentication/backend configuration; and migration preservation. These strengthen the behavior first driven through the endpoint tests.

Setup failures were corrected and are **not** claimed as intended red states: sandbox denial reading NuGet configuration; the first EF design-time invocation without Development services; bUnit's async disposal and placeholder authorization-service requirements; a component stub missing the login page's antiforgery request; and Mediator dependency validation requiring the current-account service to be registered even when OpenIddict endpoints are disabled. The disabled-authentication regression now passes and profile routes remain unmapped in that configuration.

## Final results

| Check | Observed result |
| --- | --- |
| Application tests | 33 passed, 0 failed, 0 skipped |
| Client component/routing tests | 39 passed, 0 failed, 0 skipped |
| Integration and SQL Server LocalDB tests | 77 passed, 0 failed, 0 skipped |
| Chrome browser workflows | 7 passed, 0 failed, 0 skipped |
| Total | **156 passed**, 0 failed, 0 skipped |
| Solution build | Succeeded; 0 errors, 13 pre-existing warnings in unchanged integration tests |
| Release WebAssembly client publish | Succeeded; optional `wasm-tools` optimization workload remains absent |
| Migration consistency | No pending model changes; upgrading the previous schema preserved a synthetic account and protected BFF session |
| Whitespace and comments | `git diff --check` and an explicit new-file whitespace check passed; all 127 production class/method comment IDs were unique |

The Chrome run includes the six existing UC-001/UC-020 browser regressions plus the new profile-list workflow. The new workflow starts at an anonymous protected URL, performs a real verified-account PKCE sign-in, verifies the empty list, seeds synthetic owned/editor/view-only/private profiles, verifies a narrow viewport without horizontal overflow, removes a grant and reloads, interrupts the list request, and retries successfully. Existing workflows cover registration/confirmation, resend, form/connection recovery, server-held token refresh, sign-out isolation/revocation, and session recovery after server restart. All use the actual published client and API/BFF over local HTTPS.

## Repeatable commands

Run from the repository root in PowerShell with the installed .NET 10 SDK, the existing NuGet packages, SQL Server LocalDB, and Chrome available. This machine uses SDK `10.0.403`. Its test runner is Microsoft.Testing.Platform.

```powershell
dotnet build PersonalFinance.slnx --verbosity minimal
dotnet publish src/PersonalFinance.Client -c Release --verbosity minimal
dotnet test --no-build --no-restore --project tests/PersonalFinance.Application.Tests
dotnet test --no-build --no-restore --project tests/PersonalFinance.Client.Tests
dotnet test --no-build --no-restore --project tests/PersonalFinance.IntegrationTests
dotnet test --no-build --no-restore --project tests/PersonalFinance.EndToEndTests
git diff --check
```

`PERSONAL_FINANCE_TEST_BROWSER` was unset in the final run; the existing harness therefore launched Chrome. For focused checks, append `-- --filter-class '*ProfileListTests'` to the integration command or `-- --filter-class '*ProfileListPageTests'` to the client command. The test fixture applies migrations to unique test-owned databases, uses synthetic accounts and fake/captured email, and removes only its own data. The migration upgrade test first applies the previous migration, persists an account/session, then upgrades and verifies both remain available.

## Environment and remaining limits

- The ordinary local development database was **not** migrated or seeded. Running this page against that existing database requires applying the checked-in migration to the explicitly selected Development database. No automatic migration-on-startup was added.
- The migration was scaffolded with a design-only connection setting; scaffolding did not create or update that database. The installed EF tool `10.0.0` warned that it is older than EF runtime `10.0.3`; no tool/dependency upgrade was made for this phase.
- Domain construction tests start with Phase 2, when financial domain behavior is first introduced. No profile creation, editing, sharing management, or profile-detail route was implemented here.
- Only Chrome browser verification was run for this phase. The broader browser matrix belongs to final acceptance; existing Firefox startup and actual Safari limitations are unchanged. No Edge, WebKit, Brave, Firefox, or Safari pass is claimed for this phase.
- No Azure provisioning, production-origin cookie feasibility check, live ACS send, or production deployment was performed. Local HTTPS results do not resolve those existing release gates.

Phase 1 ends at the learning checkpoint in the approved plan. Continue to Phase 2 only after the user indicates readiness.
