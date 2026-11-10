# UC-004 Phase 1 — Category review verification

Verified locally on 10 November 2026 after explicit approval of the concrete Phase 1 implementation plan. `v1.0` was the only directory directly under `docs/`, so it was selected as the highest numeric version. This evidence covers the read-only category review phase only.

## Delivered behavior

- Profile details link to `/budget-profiles/{ProfileId}/categories`. The protected page displays saved category names separately from their Income/Savings/Debt system meanings and shows the participant's current effective role. Rows use saved category IDs as keys.
- `GET /api/budget-profiles/{profileId}/categories` resolves the current BFF account, dispatches through Mediator, and projects existing category storage through an Application-defined reader port. Results contain profile ID, current role, and category IDs/names/kinds.
- Current owners, editors, and viewers can read. Missing/inaccessible profiles return equivalent 404 responses without category data; absent or ineligible sessions return 401 without redirects. Successful, unavailable, and unauthorized responses use `Cache-Control: no-store`.
- The client distinguishes loading, successful empty results, generic unavailable results, and retryable errors. Session loss notifies client authentication state and redirects to sign-in with the category return path. Route changes clear old data and cancel prior requests; late responses cannot replace the selected profile or redirect its route.
- No schema migration, dependency change, new domain behavior, category mutation, subcategory/comment/lifecycle storage, or financial workflow was added. Ordinary-category rendering on the existing profile overview remains part of Phase 2 as planned.

## Observed red/green results

Before adding production behavior:

```powershell
dotnet test tests/PersonalFinance.IntegrationTests --filter FullyQualifiedName~ProfileCategoriesTests.Current_participants --no-build --no-restore
dotnet test tests/PersonalFinance.Client.Tests --filter "FullyQualifiedName~ProfileCategoriesPageTests|FullyQualifiedName~BudgetProfileDetailsPageTests.Deep_link" --no-restore
```

The API test reached the migrated synthetic database and failed with **expected OK, actual NotFound** because the endpoint was unmapped. All **14 selected component/navigation cases** failed: the page was not routed and the profile link was missing. These are the intended red results. The first sandboxed API attempt failed during LocalDB setup instead; that environment failure is not counted as red evidence. The successful setup/red API run used execution outside the sandbox.

After implementation, focused component execution passed 17 of 18 cases, with the remaining failure caused by the synthetic handler not handling the login page's antiforgery request. The subsequent full client run exposed that the test's explicit authentication re-query returned its always-authenticated fixture response. The fixture was corrected to serve the login dependency and assert the actual signed-out notification. The first six-case category API run passed five cases; the expired-session fixture incorrectly attempted to create an already-expired session, which the existing store rejects. It was corrected to expire the test-owned stored session after valid creation. These were fixture corrections; production session/authentication behavior was not changed or weakened. Final suites below all passed.

## Final commands and results

Commands were run from the repository root. LocalDB/API and browser execution used permission-reviewed execution outside the sandbox; the shared test host creates and removes only its own uniquely named synthetic databases. No migration was applied to the ordinary development database.

| Command | Observed result |
| --- | --- |
| `dotnet test tests/PersonalFinance.Domain.Tests --no-build --no-restore` | 18 passed, 0 failed/skipped |
| `dotnet test tests/PersonalFinance.Application.Tests --no-build --no-restore` | 46 passed, including 3 category query tests; 0 failed/skipped |
| `dotnet test tests/PersonalFinance.Client.Tests --no-restore` | 76 passed, including 13 new category-page cases and the profile navigation assertion; 0 failed/skipped |
| `dotnet test tests/PersonalFinance.IntegrationTests --no-build --no-restore` | 100 passed, including 6 category API/session cases; 0 failed/skipped |
| `dotnet build PersonalFinance.slnx --no-restore --verbosity minimal` | Succeeded; 0 errors and 13 existing warnings in account integration tests (CS0105 and xUnit1051) |
| `dotnet publish src/PersonalFinance.Client -c Release --no-restore --verbosity minimal` | Succeeded; emitted the existing optional `wasm-tools` optimization notice |
| `dotnet test tests/PersonalFinance.EndToEndTests --no-build --no-restore --filter "FullyQualifiedName~ProfileCategoriesBrowserTests\|FullyQualifiedName~ProfileListBrowserTests\|FullyQualifiedName~CreateProfileBrowserTests"` | 4 passed in Chrome, 0 failed/skipped; the filter uses literal `|` separators |
| `git diff --check` | Passed |

No duplicate purpose-comment IDs were found in `src/` after the change. New production types and methods have purpose comments with unique IDs, as required by `AGENTS.md`.

## Coverage and limits

- Application: forwards the authoritative current account, requested profile, and cancellation token without replacing returned category identity; rejects absent accounts before reading; preserves unavailable results.
- LocalDB/API: owner/editor/viewer access, identity/name/kind fidelity across fresh reads, profile isolation, unrelated and missing profiles, current role after downgrade, revoked grants, and missing/unknown/expired sessions or unconfirmed/removed accounts. No-store responses and lack of anonymous redirects are asserted.
- Components: role labels, saved names distinct from system meanings, read-only controls, back links, credential inclusion, loading/empty/unavailable states, recovery from server/network/invalid-JSON/null-body failures, signed-out notification/return path, immediate old-data removal on route change, and late success/unauthorized responses from the previous route.
- Chrome: actual sign-in from a category deep link, owner review, back navigation and profile link, reload, network failure/retry, viewer review, profile switching, narrow viewport without horizontal overflow, and unavailable results after grant removal. Existing profile-list/deep-link/history and profile-creation workflows also passed.

Other browser engines, the remainder of the account browser suite, and deployed origins were not exercised in this phase. The full integration suite supplies existing account/session and profile regressions. Subcategory management, writes, deletion, concurrency policies, and historical financial integration remain outside this phase and unverified here. Phases 2–7 remain pending.
