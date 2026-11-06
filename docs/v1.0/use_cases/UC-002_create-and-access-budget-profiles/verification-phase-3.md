# UC-002 — Phase 3 verification

Date: **6 November 2026**. Scope: [approved implementation plan, Phase 3](implementation-plan.md). Phase 3 is complete; Phases 4–6 have not started. The v1.0 directory remains the latest and only version directory.

## Delivered behavior and layer flow

The protected /budget-profiles/{profileId:guid} page requests GET /api/budget-profiles/{profileId} with the credentialed BFF session. The endpoint dispatches GetAccessibleProfileQuery through Mediator. The Application handler resolves the current account and asks IProfileDetailsReader for that exact profile. The Infrastructure EF projection checks ownership or a current editor/view-only grant in the same query that returns the profile settings and initial setup records. It does not use the earlier list result or a cached role.

The browser-safe detail response includes profile identity/name, main and additional currencies, current role, categories and their stable kinds, payment-method kinds, and tags and their system roles. The landing page displays the name, role, currencies, Income/Savings/Debt defaults, Cash, and IOU tags. Accessible list names now link to their profile. After creation, the browser opens the returned profile ID.

Missing and inaccessible profile IDs return the same empty 404 response with no-store; the protected page presents one generic unavailable message and a link to the profile list. Anonymous API requests receive 401, without a login redirect. Detail responses are also marked no-store.

No migration or schema change was needed. Sharing management, profile switching/race handling, and the broader recovery work remain in later phases.

## Observed red/green evidence

- **Missing detail route:** The first focused LocalDB integration test failed as intended: GET /api/budget-profiles/{id} returned 404 instead of 200 for a seeded, currently accessible editor profile. After adding the authorized query, endpoint, and persistence projection, the test passed.
- **Current access:** The integration case lists the profile, opens it as an editor, changes the grant to viewer and confirms the returned role changes, opens an owned profile as owner, then removes the shared grant. The revoked profile and a random nonexistent ID both return 404 with identical bodies and no profile-name leakage. Anonymous detail access returns 401.
- **Profile contents:** The integration test verifies main/additional currencies and category, payment-method, and tag data. The browser workflow verifies all three initial categories, Cash, IOU REPAYMENT, the owner role, currency settings, creation-to-profile navigation, deep-link reload, and opening from the list.
- **Component behavior:** Two new bUnit tests pass for the credentialed detail load and generic unavailable state. The client suite passes with updated creation-navigation and list-link checks.
- **Environment correction:** The first browser attempt used a stale Release-published client and failed before reaching sign-in/profile behavior. Republishing the updated client resolved that setup issue; the targeted Playwright workflow then passed. The initial solution build also found stale Domain test restore assets; restoring existing declared packages resolved it without dependency changes.

## Final results

| Check | Observed result |
| --- | --- |
| Domain tests | 18 passed, 0 failed, 0 skipped |
| Application tests | 43 passed, 0 failed, 0 skipped |
| Client component/routing tests | 53 passed, 0 failed, 0 skipped |
| Integration and SQL Server LocalDB tests | 93 passed, 0 failed, 0 skipped |
| Focused Playwright creation/opening workflow | 1 passed, 0 failed, 0 skipped |
| Distinct tests across these final suites | **208 passed**, 0 failed, 0 skipped |
| Solution build | Succeeded; 0 warnings, 0 errors |
| Release client publish | Succeeded; optional wasm-tools optimization workload remains absent |
| Migration/model changes | None |

The integration build reported pre-existing warnings in unchanged account tests (one duplicate using and xUnit cancellation-token analyzer warnings). They do not appear in the final solution build.

## Repeatable commands

Run from the repository root using the installed .NET SDK, SQL Server LocalDB, and Chrome for the browser workflow:

- dotnet restore PersonalFinance.slnx --verbosity minimal
- dotnet build PersonalFinance.slnx --no-restore --verbosity minimal
- dotnet publish src/PersonalFinance.Client -c Release --no-restore --verbosity minimal
- dotnet test --no-build --no-restore --project tests/PersonalFinance.Domain.Tests
- dotnet test --no-restore --project tests/PersonalFinance.Application.Tests
- dotnet test --no-restore --project tests/PersonalFinance.Client.Tests
- dotnet test --no-restore --project tests/PersonalFinance.IntegrationTests
- dotnet test --no-restore --project tests/PersonalFinance.EndToEndTests -- --filter-method '*Signed_in_account_can_create_complete_profiles_with_explicit_currency_settings'
- git diff --check

The focused initial red and final access-removal check used:

- dotnet test --no-restore --project tests/PersonalFinance.IntegrationTests -- --filter-method '*Opening_a_profile_returns_current_role_settings_and_setup_defaults_only_while_access_is_current'
- dotnet test --no-build --no-restore --project tests/PersonalFinance.IntegrationTests -- --filter-method '*Opening_a_profile_returns_current_role_settings_and_setup_defaults_only_while_access_is_current'

## Checkpoint

Phase 3 ends here. Profile opening uses current server-side ownership/grants, displays the authorized profile's setup, and creation opens its saved landing page. Same-component profile route changes and switching consistency remain Phase 4.
