# UC-002 — Phase 4 verification

Date: **7 November 2026**. Scope: [approved implementation plan, Phase 4](implementation-plan.md). Phase 4 is complete; Phases 5–6 have not started. The `docs/` version directories were inspected before the work; `docs/v1.0/` remains the latest and only version.

## Delivered behavior

The protected profile details route now reloads when its `ProfileId` parameter changes, including navigation within an already-mounted details component. Starting a load clears the previous profile and shows the existing loading state. A new request cancels the previous request; a generation check prevents an older response or failure from replacing the selected profile's content. The route remains the source of profile selection, with no persistent profile preference or API/schema change.

The existing Back to profiles interaction remains available on the profile landing page. The added browser workflow covers opening a seeded shared profile from a deep link, reloading it, removing its grant and confirming the next request shows the generic unavailable page, returning to the list, opening an owned profile, and using browser back/forward while confirming that the displayed name, currency, and role match the URL.

## Observed red/green evidence

- **Same-component route update:** The new component test initially failed because changing the route from the first to the second profile did not issue a second detail request or display the second profile. After the route-parameter lifecycle change, it passed and verified the prior name was cleared, the new currency/role displayed, and both profile IDs were requested.
- **Outdated response and transition content:** The new delayed-response component test initially failed because the second profile never loaded while the first request remained pending. After adding per-load cancellation and generation checks, it passed. It verifies that the first profile's content is cleared while the second profile is pending, and completing the first response afterward does not change the selected profile's content or role.
- **Browser workflow:** The new Playwright test passed for a signed-in account switching between owned and seeded view-only profiles through deep links, reload, the profile list, and browser history. After the shared grant is removed, reloading the profile shows the unavailable state and does not retain its previous content.

## Final results

| Check | Observed result |
| --- | --- |
| Focused profile detail component tests | **4 passed**, 0 failed, 0 skipped |
| Complete Client component/routing suite | **55 passed**, 0 failed, 0 skipped |
| Complete EndToEnd browser suite | **9 passed**, 0 failed, 0 skipped |
| Solution build | Succeeded; 0 errors and 13 pre-existing warnings in unchanged integration-test account files |
| `git diff --check` | Passed |
| API, database schema, migrations | No changes |

The .NET test runner required execution outside the sandbox because the sandbox denied its local named-pipe connection. The focused and complete test runs succeeded with that access. The solution warnings were existing duplicate-using and xUnit cancellation-token analyzer warnings in unrelated account tests.

## Repeatable commands

Run from the repository root:

- `dotnet test --no-restore --project tests/PersonalFinance.Client.Tests -- --filter-method '*BudgetProfileDetailsPageTests*'`
- `dotnet test --no-restore --project tests/PersonalFinance.Client.Tests`
- `dotnet test --no-restore --project tests/PersonalFinance.EndToEndTests`
- `dotnet build PersonalFinance.slnx --no-restore --verbosity minimal`
- `git diff --check`

## Checkpoint

Phase 4 ends here. Profile switching follows the URL, and responses from earlier routes cannot overwrite the selected profile. Phase 5's cross-flow failure recovery has not started.
