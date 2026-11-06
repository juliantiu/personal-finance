# UC-002 — Phase 2 verification

Date: **6 November 2026**. Scope: [approved implementation plan, Phase 2](implementation-plan.md). Phase 2 is complete; Phases 3–6 have not started. Version directories were inspected again: `docs/v1.0/` remains the latest and only version.

## Delivered behavior and layer flow

The protected `/budget-profiles/create` page collects a required profile name, an explicitly selected GBP or USD main currency, and an optional alternative currency. No main currency is preselected. The profile list now offers Create first profile or Create another profile. Successful creation returns to the authorized list; opening the saved profile remains Phase 3 work.

The browser obtains fresh proof from the existing antiforgery endpoint, then sends a credentialed `POST /api/budget-profiles`. The Minimal API checks the existing opaque BFF session and verified account before dispatching `CreateProfileCommand` through Mediator and the FluentValidation pipeline. The handler resolves the current account, constructs the Domain setup, and calls the Application-defined `IProfileCreationStore` port. A posted owner/account ID cannot change ownership. Temporary Identity login tickets alone are insufficient. The API returns `201` with the saved ID/location, `400` for invalid input or antiforgery rejection, and `401` for unusable sessions; responses use `no-store`.

The Domain factory enforces a trimmed, nonblank name of at most 100 characters, supported main/additional currency choices, distinct alternatives excluding the main currency, and a real original owner. Duplicate profile names are allowed. It assigns stable GUIDs to the profile, Income/Savings/Debt categories, Cash payment method, and IOU/IOU REPAYMENT tags. Category kinds and tag roles are separate from display names. Setup collections cannot be changed through the caller's original currency array. Domain has no HTTP, EF, Identity, or UI dependency.

The Infrastructure adapter maps the complete setup and saves it with one transactional EF `SaveChangesAsync`. Migration `20261106110747_AddProfileCreation` adds `ProfileCurrencies`, `Categories`, `PaymentMethods`, and `Tags`, their foreign keys, the additional-currency composite key, and filtered uniqueness for system category kinds, Cash, and tag roles. The current category/payment-kind constraints cover only the creation types implemented in this phase. No card, subcategory, period, financial-entry, rename, deletion, or sharing command was introduced. Main-versus-additional currency exclusion is enforced by Domain/Application creation validation; it is not represented by a duplicated main-currency column in the membership table.

The form disables inputs/submission while a request is in flight, retains inputs after errors, shows server field feedback, and follows the existing login-return flow on session loss. A failed or unreadable successful POST response is treated as uncertain rather than retried automatically. The user must refresh accessible profiles, review the results, and explicitly choose Retry creation. A failed refresh does not enable retry. A confirmed `400` remains a rejected outcome even if its feedback body cannot be read.

`AGENTS.md` now exempts `src/PersonalFinance.Infrastructure/Persistence/` and all subdirectories, including migrations, from class/method purpose comments. New production classes/methods elsewhere retain unique comment IDs.

## Observed red/green evidence

- **Missing creation endpoint:** The first integration test failed with `405 Method Not Allowed` instead of `201 Created`. After the endpoint, command, domain, persistence, and migration were implemented, verified creation and list visibility passed.
- **Domain setup:** All **18** domain cases failed against the unfinished creation factory. Implementing validated construction made all **18 pass**, covering both main choices, zero/one additional currency, missing/blank/100/101-character names, trimming, duplicate names, invalid/repeated alternatives, owner presence, complete defaults, distinct IDs, and defensive collection copying.
- **Application behavior:** After correcting a test compilation ambiguity, all **8** new application cases failed because validation/orchestration were absent. They passed after the field rules and account/domain/store coordination were implemented. They verify current-account ownership, cancellation propagation, no writes for unauthenticated/invalid input, and persistence failures propagating without success.
- **Missing protected form:** All **9** initial component cases failed while the creation route/form was absent. The implemented route and form made them pass, including explicit main selection, contract/proof/credentials, field errors, in-flight submission guarding, session loss, and uncertain-outcome refresh/retry.
- **Confirmed rejection feedback:** A later focused pair had **1 passed, 1 failed**: malformed JSON in a `400` response incorrectly switched to uncertain-save feedback. Keeping the confirmed rejection while tolerating unreadable field feedback fixed the failure. The final client suite includes **12 creation cases** and **51 total passed**.
- **SQL rollback and constraints:** A real test-owned SQL CHECK constraint rejects the IOU REPAYMENT tag during default persistence. The API does not report success, and a fresh DbContext finds zero profiles, memberships, categories, payment methods, or tags. Removing the injected constraint permits an explicit retry. Additional provider tests prove duplicate system category kinds, Cash methods, tag roles, and additional memberships are rejected by SQL Server.
- **Migration and admission checks:** Upgrading the Phase 1 schema preserves a synthetic account, existing profile, current grant, and BFF session, then permits complete new creation. The model snapshot has no pending changes. Invalid names/currencies, supplied owner spoofing, missing/invalid antiforgery, anonymous/tampered/expired/revoked sessions, unverified/deleted accounts, and temporary Identity tickets are covered without unauthorized writes. Phase 1's migration test is pinned to its own migration so it continues to verify that intermediate schema separately.
- **Browser entry point:** Chrome uses the published client and actual HTTPS API/BFF. The new workflow signs in through PKCE with a protected creation return path, checks required inputs, creates GBP with USD enabled and USD without an alternative, returns to the list, and verifies ownership/default rows in SQL. The form is also checked at a narrow viewport for horizontal overflow. All **8** browser workflows passed against the final published client, including the existing registration, confirmation, profile-list, refresh, and sign-out regressions.

Setup failures are not claimed as intended red states: sandbox access to the user's NuGet configuration required elevated .NET execution; target-typed validation arguments needed explicit command types; a test used the wrong session-revocation method name; the expired-session fixture initially tried to create an already-expired session and was corrected to expire a persisted session; and MudBlazor's test state accessor required its extension namespace. No automatic approval rejection remained unresolved.

## Final results

| Check | Observed result |
| --- | --- |
| Domain tests | 18 passed, 0 failed, 0 skipped |
| Application tests | 41 passed, 0 failed, 0 skipped |
| Client component/routing tests | 51 passed, 0 failed, 0 skipped |
| Integration and SQL Server LocalDB tests | 92 passed, 0 failed, 0 skipped |
| Chrome browser workflows | 8 passed, 0 failed, 0 skipped |
| Total distinct tests | **210 passed**, 0 failed, 0 skipped |
| Final solution build | Succeeded; 0 errors, 0 warnings in the final incremental build |
| Release WebAssembly client publish | Succeeded; optional `wasm-tools` optimization workload remains absent |
| Migration consistency | Upgrade preservation and no pending model changes passed in LocalDB tests |
| Whitespace/comment checks | Changed-file whitespace check and `git diff --check` passed; all 131 production comment IDs were unique |

The earlier integration rebuild emitted the same 13 pre-existing warnings in unchanged account tests. The new test/code warnings were corrected. Counts above are distinct tests from the final applicable suites, not a sum of repeated runs.

## Repeatable commands

Run from the repository root in PowerShell with the installed .NET SDK `10.0.403`, SQL Server LocalDB, and Chrome. Tests use Microsoft.Testing.Platform, synthetic accounts, captured email, and uniquely named fixture-owned databases.

```powershell
dotnet build PersonalFinance.slnx --no-restore --verbosity minimal
dotnet publish src/PersonalFinance.Client -c Release --verbosity minimal
dotnet test --no-build --no-restore --project tests/PersonalFinance.Domain.Tests
dotnet test --no-build --no-restore --project tests/PersonalFinance.Application.Tests
dotnet test --no-restore --project tests/PersonalFinance.Client.Tests
dotnet test --no-restore --project tests/PersonalFinance.IntegrationTests
dotnet test --no-build --no-restore --project tests/PersonalFinance.EndToEndTests
git diff --check
```

Focused red/green commands used the Domain test project directly, or appended `-- --filter-class '*Profiles.CreateProfileTests'`, `-- --filter-class '*CreateProfilePageTests'`, `-- --filter-class '*CreateProfileBrowserTests'`, or `-- --filter-method '*Confirmed_rejection*'` to the corresponding test command. `PERSONAL_FINANCE_TEST_BROWSER` was unset for the Chrome runs.

The migration was scaffolded with:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__Identity = 'Server=(localdb)\MSSQLLocalDB;Database=PersonalFinance_DesignOnly;Integrated Security=true;TrustServerCertificate=true'
dotnet ef migrations add AddProfileCreation --project src/PersonalFinance.Infrastructure --startup-project src/PersonalFinance.Api
```

Scaffolding did not update a database. EF tools `10.0.0` warned that the runtime is `10.0.3`; no tool upgrade was made.

## Environment limits and checkpoint

- The ordinary development database was not migrated or seeded. Apply the checked-in migrations to an explicitly selected Development database before using the new workflow against it; no automatic migration-on-startup behavior was added.
- Chrome alone was run for Phase 2. Edge, Brave, Firefox, WebKit, and actual Safari are not claimed as verified here. Existing browser/deployment release gates remain unchanged.
- No Azure provisioning, production deployment/origin check, or live ACS email was performed.
- Profile opening, detail display, and switching remain Phases 3–4. Broader recovery and full acceptance remain Phases 5–6. Completing basic form recovery here does not mark those phases complete.

Phase 2 ends at its learning checkpoint. Continue to Phase 3 only after the user indicates readiness.
