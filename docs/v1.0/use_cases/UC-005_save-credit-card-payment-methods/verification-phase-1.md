# UC-005 — Phase 1 verification

Date: **11 November 2026**. Scope: [approved implementation plan, Phase 1](implementation-plan.md#phase-1-review-profile-payment-methods). Phase 1 is complete. `docs/v1.0/` was resolved again as the latest and only version directory before implementation and documentation updates.

## Implemented behavior and layer flow

- The protected `/budget-profiles/{profileId}/payment-methods` page is reachable from profile details and directly through a deep link. Current owners, editors, and viewers can review the profile name, role, and existing Cash method. No default selection or mutation controls are introduced.
- `GET /api/budget-profiles/{profileId}/payment-methods` dispatches `ListPaymentMethodsQuery`. The Application handler resolves `ICurrentAccount`, which uses the existing eligible BFF account/session checks. The Infrastructure reader projects the requested profile and its method IDs/kinds under the current owner/grant predicate, rather than loading financial entities into the client.
- Inaccessible and nonexistent profiles return the same non-disclosing 404. Invalid/ineligible sessions return 401 without a login redirect from the API. Successful and handled unavailable/unauthenticated responses use `Cache-Control: no-store`. Caller-supplied account IDs do not replace the BFF identity. Roles and removed grants are read again on subsequent requests.
- Both the new list and the existing profile-details projection carry persisted method IDs. Both client lists render using method IDs rather than kinds.
- The client distinguishes loading, genuinely empty results, unavailable profiles, session loss, and failed requests. It clears previous content during route changes, cancels superseded requests, ignores late results, and invalidates pending responses on disposal. Malformed responses, missing method collections, and responses for another profile are recoverable load failures, never authoritative empty lists.

No Domain behavior is needed for this read-only projection. No schema/migration, dependency, card storage/protection configuration, or ordinary development-database change was made. Test hosts apply existing migrations only to uniquely named, test-owned LocalDB databases and use synthetic accounts and grants.

## Observed red/green evidence

| Behavior | Red | Green |
| --- | --- | --- |
| Access-scoped SQL/HTTP list | All 11 focused cases failed while the route was absent: expected 200/401 versus 404, or absent no-store handling. Fixtures successfully migrated and seeded LocalDB. | All 11 passed after the handler, EF reader, endpoint, contracts, and ID projection were connected. |
| Application orchestration | All 3 focused cases failed against the unimplemented handler: authentication and reader delegation were absent. | All 3 passed after current-account resolution and account/profile/token forwarding were implemented. |
| Protected page and states | All 12 initial component cases failed while the page route was absent. The existing profile-details deep-link case separately failed on the missing navigation link. | All 17 initial focused page/details checks passed after implementation. |
| Invalid successful responses | A missing collection caused a render exception; a response for another profile incorrectly displayed instead of reporting failure. The 4 existing failure/retry cases remained green. | All 19 focused page/details checks passed after validating profile identity and collection presence, including explicit retries. |
| Real browser deep link/navigation | After API composition was complete, the baseline published client failed the protected deep-link redirect because the payment-method page was absent. | The focused Chrome workflow passed after a fresh Release client publish: sign-in/return, Cash list, reload, profile navigation, narrow layout, and revoked access. |

The first sandboxed test command could not read the user NuGet configuration; it did not run tests and is not counted as red evidence. Verification was retried with approved local tool permissions. An initial browser attempt during incomplete handler composition stopped at a missing DI registration; that setup failure is also excluded from red evidence. The subsequent browser red reached the missing-page behavior described above.

## Regression results

| Check | Observed result |
| --- | --- |
| Domain suite | **18 passed**, 0 failed, 0 skipped |
| Application suite | **46 passed**, 0 failed, 0 skipped |
| Client suite | **71 passed**, 0 failed, 0 skipped |
| Integration / LocalDB suite | **104 passed**, 0 failed, 0 skipped |
| Focused Chrome payment-method workflow | **1 passed**, 0 failed, 0 skipped |
| Complete serial Chrome browser suite | **11 passed**, 0 failed, 0 skipped |
| Complete serial Edge browser suite | **11 passed**, 0 failed, 0 skipped |
| Solution build | Succeeded; 0 warnings, 0 errors |
| Release client publish | Succeeded; optional `wasm-tools` optimization workload is absent |
| Unique purpose-comment IDs | Checked; no duplicate class/method IDs in source |
| `git diff --check` | Passed; new files also passed a separate trailing-whitespace check |

The full Integration suite includes existing account registration/confirmation, BFF/session, profile creation/defaults, profile access, and migration/model-consistency checks. The ordinary developer database was not used. The final solution build was incremental; earlier integration compilation emitted existing duplicate-using and cancellation-token analyzer warnings in unchanged account tests.

Both complete browser runs used the fresh Release-published client and `XUNIT_MAXPARALLELTHREADS=1`. Chrome completed all 11 workflows in approximately 1 minute 51 seconds; Edge completed all 11 in approximately 1 minute 16 seconds. These include account/BFF and profile regressions as well as the new payment-method workflow. No captures of real financial data or live card numbers were involved.

## Repeatable commands

Run from the repository root with .NET SDK `10.0.403`, SQL Server LocalDB, and installed Chrome/Edge. This environment requires execution with access to the user's .NET configuration and local test services.

```powershell
dotnet build PersonalFinance.slnx --no-restore --verbosity minimal
dotnet publish src/PersonalFinance.Client -c Release --no-restore --verbosity minimal

dotnet test --no-build --no-restore --project tests/PersonalFinance.Domain.Tests
dotnet test --no-build --no-restore --project tests/PersonalFinance.Application.Tests
dotnet test --no-build --no-restore --project tests/PersonalFinance.Client.Tests
dotnet test --no-build --no-restore --project tests/PersonalFinance.IntegrationTests

dotnet test --no-build --no-restore --project tests/PersonalFinance.Application.Tests -- --filter-class '*ListPaymentMethodsTests'
dotnet test --no-build --no-restore --project tests/PersonalFinance.IntegrationTests -- --filter-class '*PaymentMethodListTests'
dotnet test --no-build --no-restore --project tests/PersonalFinance.Client.Tests -- --filter-class '*PaymentMethodsPageTests' --filter-class '*BudgetProfileDetailsPageTests'

$previousParallelism = $env:XUNIT_MAXPARALLELTHREADS
$previousBrowser = $env:PERSONAL_FINANCE_TEST_BROWSER
try {
    $env:XUNIT_MAXPARALLELTHREADS = '1'
    $env:PERSONAL_FINANCE_TEST_BROWSER = 'chrome'
    dotnet test --no-build --no-restore --project tests/PersonalFinance.EndToEndTests
    $env:PERSONAL_FINANCE_TEST_BROWSER = 'edge'
    dotnet test --no-build --no-restore --project tests/PersonalFinance.EndToEndTests
}
finally {
    $env:XUNIT_MAXPARALLELTHREADS = $previousParallelism
    $env:PERSONAL_FINANCE_TEST_BROWSER = $previousBrowser
}

git diff --check
```

## Scope and remaining gates

This phase verifies reviewing existing methods only. Saving, masking stored card identifiers, protection keys, reveal, editing, retirement/restoration, and real period/transaction/import integration remain in their documented phases. No new migration is needed for Phase 1. Browser results establish only the browsers actually run; Firefox, WebKit, Brave, actual Safari, deployed origins, and cloud provisioning are outside this phase's verification.

Phase 1 is complete and work stops at its checkpoint. Phase 2 has not started.
