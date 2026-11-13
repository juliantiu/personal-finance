# PaymentMethod domain entity refactor — verification

Date: **13 November 2026**. The user approved the concrete behavior-preserving refactor plan and implementation through completion. `docs/v1.0/` was resolved as the latest and only direct numeric version directory before implementation and documentation updates.

**Status: complete**, including red/green, full regression and serial Chrome/Edge verification.

## Implemented model and flow

`PaymentMethod` is a sealed Domain entity with read-only ID, profile ID, kind, nickname and optional `CardNumber`. Its private constructor is reachable through Cash and credit-card factories only. Creation requires a profile identity. Cash is constructed with nickname `Cash` and no number. Credit cards require a valid number; the existing `CardNumber` implementation still supplies canonicalization, length/Luhn rules and masked diagnostic output.

Nickname normalization is now private entity behavior: trim surrounding whitespace, convert blank to null, reject more than 100 characters after trimming, retain internal spacing and allow duplicate names. The separate static helper and its standalone tests were removed; the normalization coverage is preserved in entity construction tests. The entity has no public rename, number-edit or lifecycle mutation methods in this slice.

`BudgetProfile.Create` owns its initial Cash `PaymentMethod`. The existing `CashPaymentMethodId` is a derived property, not a second stored identity. The profile adapter persists Cash identity/profile/nickname from that domain entity.

After current account/access and request-shape checks, `SaveCardHandler` creates a credit-card `PaymentMethod`. Safe `PaymentMethodValidationException` failures become the existing Number/Nickname field errors without retaining attempted values. Application coordinates protection using the entity's profile, ID and validated number, then passes that same entity with the protected details to the write port. Infrastructure maps its metadata to `PaymentMethodRecord`, rechecks write access/key compatibility and preserves the existing transactional/uniqueness behavior. The card adapter rejects a Cash entity before opening a write transaction.

`PaymentMethodRecord` remains an Infrastructure persistence representation. Masked reads remain projections and do not hydrate/decrypt a domain card just to render its label. Domain does not reference EF, HTTP, encryption providers or client frameworks. JSON/UI contracts, existing migrations and the physical schema are unchanged by this refactor. No ordinary database operation, dependency, reveal/edit/retirement feature or unrelated cleanup is introduced.

## Observed red/green results

| Behavior | Red | Green |
| --- | --- | --- |
| Domain construction/invariants | All 15 initial entity cases failed against the unimplemented factories, including safe property failures and profile identity. | All 15 passed after the factories, constructor and entity-owned nickname normalization were implemented. |
| Profile ownership | The added profile-creation case failed because the new Cash entity property was not populated. | The profile now constructs its Cash entity; all 16 entity/profile cases pass, with Cash ID/profile/kind/default/number invariants checked. |
| Card adapter boundary | A focused LocalDB case expected Cash rejection but no exception was thrown; the generic entity input could be written through the card store. | After the kind boundary guard, the same test passes and only the original Cash row remains. |
| Application orchestration/regressions | Existing behavior checks were retained while the write port and fake changed to accept the entity; no compiler/setup failure is claimed as a red state. | All 53 Application cases pass, including assertions that protection, persistence and returned results use the same entity identity, canonical number and normalized nickname. |

The old seven nickname helper cases are represented by entity nickname cases (including both length boundaries), alongside the new identity, kind, number and profile tests. `CardNumber` source and its existing tests remain unchanged. Purpose-comment IDs are supplied for the new classes/methods, without restoring comments the user removed from earlier work.

## Final regression checks

| Check | Result |
| --- | --- |
| Domain suite | 49 passed; 0 failed/skipped |
| Application suite | 53 passed; 0 failed/skipped |
| Client suite | 84 passed; 0 failed/skipped |
| Integration / isolated LocalDB suite | 137 passed; 0 failed/skipped |
| Complete serial Chrome suite | 12 passed; 0 failed/skipped (approximately 1 minute) |
| Complete serial Edge suite | 12 passed; 0 failed/skipped (approximately 1m 10s) |
| Solution build | Succeeded; final incremental build: 0 warnings/errors |
| Release client publish | Succeeded |
| Static/documentation checks | Passed: unique purpose-comment IDs, UTF-8 text, removed-helper checks, 20 intact CSV use cases, local documentation links, changed/new-file whitespace and git diff --check |

The full Integration suite verifies baseline migrations/model consistency, profile creation/Cash defaults, current session/access handling, protected card persistence and purposes, canonical duplicates/concurrency, failure rollback, key readiness/restart, and nickname behavior. Test compilation can report the existing duplicate-using/cancellation-token warnings in unchanged account tests; the final incremental solution build is clean.

## Repeatable commands

Run from the repository root with the existing .NET SDK, isolated LocalDB test access and installed Chrome/Edge:

```powershell
dotnet build PersonalFinance.slnx --no-restore --verbosity minimal
dotnet publish src/PersonalFinance.Client -c Release --no-restore --verbosity minimal

dotnet test --project tests/PersonalFinance.Domain.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.Application.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.Client.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.IntegrationTests --no-build --no-restore

dotnet test --project tests/PersonalFinance.Domain.Tests --no-build --no-restore -- --filter-class '*PaymentMethodTests'
dotnet test --project tests/PersonalFinance.IntegrationTests --no-build --no-restore -- --filter-method '*Card_persistence_rejects_cash*'

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

Tests use synthetic numbers/accounts and test-owned keys/databases. No sensitive browser captures are enabled. No new migration is necessary; the previously implemented card/nickname migrations and key configuration requirements still apply when enabling those features in an ordinary environment. Renaming existing methods, reveal, retirement/restoration and actual transaction/import workflows remain in their planned scopes. This refactor does not begin those phases or claim deployed/cloud verification.

The approved entity refactor is complete. Work stops at this scope; the planned reveal, editing and retirement/restoration phases remain unstarted.
