# UC-005 — Phase 3 verification

Dates: **13–15 November 2026**. Scope: [Phase 3 — Reveal a card with current writer permission](implementation-plan.md#phase-3-reveal-a-card-with-current-writer-permission), explicitly approved after presentation of its concrete plan. `docs/v1.0/` was resolved again as the latest and only numeric version.

**Status:** Phase 3 complete, including final local verification on 15 November 2026. Phases 4–6 remain not started.

## Implemented behavior and layer flow

- The API accepts only an explicit antiforgery-protected `POST /api/budget-profiles/{profileId}/payment-methods/{methodId}/reveal`. It resolves the eligible BFF account and returns `Cache-Control: no-store` for handled success and failure responses. Only a successful reveal response contains the number; ordinary list, profile-details and save projections remain masked.
- `RevealCardHandler` resolves the current account, requests an authorized encrypted lookup through `ICardRevealReader`, and only then calls `ICardRevealProtection`. Denials never reach decryption. Sensitive lookup/result/response classes have no generated value-dumping diagnostic representation. Lookup and decryption failures become safe availability results.
- `EfCardRevealReader` evaluates profile access, current owner/editor permission and the exact profile/method/card predicate in one SQL projection. It projects ciphertext only for writers. Known viewers receive a safe 403. Inaccessible/nonexistent profiles, Cash and missing/cross-profile method IDs receive an empty 404; invalid/expired/ineligible sessions receive 401.
- The existing protection adapter decrypts using the original profile/method-bound purpose chain and validates the recovered canonical number. It uses retained Data Protection keys and persistent protection readiness; reveal does not require the matching key used for writes. Corrupt ciphertext, unavailable keys and resource-purpose mismatches return a safe 503 without number, ciphertext or provider-detail leakage.
- Owner/editor card rows offer an explicit Reveal action. The dialog supports hide, a separate explicit re-reveal, Close, Escape and backdrop dismissal. Hiding/closing clears component-held plaintext and cancels/invalidates pending requests. Route changes, disposal, masked reloads and observed session/permission loss also clear sensitive references. Delayed responses cannot repopulate a newer context. There is no automatic reveal or automatic retry.
- Reveal performs no database write and adds no schema, package, financial workflow or retirement control. Phase 5 owns retired-card lifecycle verification. The ordinary development database is untouched; test migrations use uniquely named test-owned databases.

Managed strings cannot guarantee physical erasure from browser/runtime memory. The verified guarantee is removal of application-held references and rendered content, with no application browser storage, URL, diagnostic, trace, screenshot or video persistence of full numbers. Browser fixtures use synthetic data and do not enable sensitive captures.

## Observed red/green evidence

| Behavior | Observed red | Observed green |
| --- | --- | --- |
| Application authorization and orchestration | Against the unavailable handler stub, 4 of 5 initial tests failed on missing account resolution, scoped lookup or safe denial; the unavailable-result case passed. | All 6 focused application cases pass, including authorization before decryption and lookup/protection failure handling. |
| SQL/HTTP reveal | All 17 initial cases failed against the absent route after successful fixture migration and card saving: expected reveal/status/no-store behavior was absent. | The final suite covers current owner/editor access, viewer/removal/downgrade denial, eligible session and antiforgery, missing/Cash/cross-profile resources, no-store, immutable storage, masked ordinary reads, corrupt/wrong-purpose/missing-key protection, restart, matching-key independence and exact identity for cards sharing a suffix. |
| Component behavior | Owner/editor cases failed because Reveal controls were absent; the viewer case passed. | All 43 payment-method cases pass, including 16 reveal cases: explicit-only requests, hide/close, pending-response invalidation, navigation/sign-out cleanup, safe denials, proof/transport/JSON failures and no automatic retry. |
| Published browser | The new Chrome workflow failed against the previous published client because Reveal was absent. The first updated browser run then exposed loss of keyboard focus after replacing the hide/reveal button, leaving Escape ineffective. | Keeping one mounted toggle button preserves focus. The focused Chrome workflow passes reveal/hide/re-reveal, Escape, Close, navigation, observed downgrade, mobile width and browser-storage checks. Final complete browser results are recorded below. |

The initial sandboxed SQL attempt could not access LocalDB and was stopped; it is not red evidence. SQL/browser checks ran with local service access against synthetic fixtures. A test-setup compilation issue and a masked-read assertion that incorrectly required success after access removal were corrected; neither is counted as a production defect or red/green evidence.

The first full Chrome run passed 11/13: the Escape defect above and an existing cross-site account test timeout occurred. That run experienced an approximately four-and-a-half-hour execution pause. A later Edge run also experienced prolonged execution pauses and startup/interaction timeouts and was stopped before a fresh run. The fresh Edge suite passed 12/13, with a `Failed to fetch` error in the existing cross-site sign-out/sign-in regression. That unchanged test then passed in isolation (approximately 21s), followed by a clean complete Edge run. The existing account/browser fixtures were not changed to bypass these failures. Final serial results below determine acceptance.

## Final regression results

| Check | Result |
| --- | --- |
| Domain suite | 49 passed; 0 failed/skipped |
| Application suite | 59 passed; 0 failed/skipped |
| Client suite | 100 passed; 0 failed/skipped, rerun after the keyboard fix |
| Integration / isolated LocalDB suite | 156 passed; 0 failed/skipped, including all 19 reveal cases (approximately 1m 35s) |
| Complete serial Chrome suite | 13 passed; 0 failed/skipped (approximately 1m 43s) |
| Complete serial Edge suite | 13 passed; 0 failed/skipped (approximately 1m 32s) |
| Solution build | Succeeded; 0 errors; 13 existing warnings in unchanged account tests |
| Release client publish | Succeeded after the keyboard fix; optional wasm-tools optimization workload remains absent |
| Static/documentation checks | Passed: changed/new UTF-8 text, whitespace and local-document links; 115 unique purpose-comment IDs; git diff --check |

## Repeatable commands

Run from the repository root with the existing SDK, installed Chrome/Edge and SQL Server LocalDB. Local service access is required. Tests own temporary keys and `PersonalFinance_UC001_Test_*` databases.

```powershell
dotnet build PersonalFinance.slnx --no-restore --verbosity minimal
dotnet publish src/PersonalFinance.Client -c Release --no-restore --verbosity minimal
dotnet test --project tests/PersonalFinance.Domain.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.Application.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.Client.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.IntegrationTests --no-build --no-restore

# Focused checks
dotnet test --project tests/PersonalFinance.Application.Tests --no-build --no-restore -- --filter-class '*RevealCardTests'
dotnet test --project tests/PersonalFinance.IntegrationTests --no-build --no-restore -- --filter-class '*RevealCardTests'
dotnet test --project tests/PersonalFinance.Client.Tests --no-build --no-restore -- --filter-class '*PaymentMethodsPageTests'

$previousParallelism = $env:XUNIT_MAXPARALLELTHREADS
$previousBrowser = $env:PERSONAL_FINANCE_TEST_BROWSER
try {
    $env:XUNIT_MAXPARALLELTHREADS = '1'
    $env:PERSONAL_FINANCE_TEST_BROWSER = 'chrome'
    dotnet test --project tests/PersonalFinance.EndToEndTests --no-build --no-restore
    $env:PERSONAL_FINANCE_TEST_BROWSER = 'edge'
    dotnet test --project tests/PersonalFinance.EndToEndTests --no-build --no-restore
}
finally {
    $env:XUNIT_MAXPARALLELTHREADS = $previousParallelism
    $env:PERSONAL_FINANCE_TEST_BROWSER = $previousBrowser
}
git diff --check
```

## Verification boundaries

No deployment, real card, ordinary database migration or secret installation is included. Deployed key recovery and other browsers, including actual Safari, are not verified by these Chrome/Edge runs. Editing, nickname correction, retirement/restoration, stale mutations and actual period/transaction/import workflows remain in their later phases/use cases. Clearing occurs when context or access/session loss is observed; no background revocation monitor is introduced.
