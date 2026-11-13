# Payment-method nickname amendment — verification

Implementation approved on **12 November 2026**. **Status: complete**, including final browser verification. The user approved the concrete nickname implementation plan after clarifying that renaming existing methods, including Cash, waits for the planned editing phase. The latest numeric documentation directory was resolved as `docs/v1.0/`; it remains the only direct version directory.

## Delivered scope

- Domain nickname rules trim surrounding whitespace, store empty/whitespace values as null and reject values over 100 characters after trimming. Internal spacing is retained. Duplicate names are allowed.
- Optional `Nickname` flows from add-card entry through the request/command, normalized validation and the existing atomic card store. Access checks run before nickname validation. Invalid nickname input does not reach protection or persistence.
- Save, payment-method list and profile-details responses include `Nickname`. Both UI lists show it beside the masked number, using the method label when absent. Razor encodes nickname text. There are no rename controls or mutation routes for existing methods.
- New profile creation assigns `Cash` to its Cash method. Migration `20261112164221_AddPaymentMethodNickname` adds nullable `nvarchar(100)`, defaults existing Cash rows to `Cash`, and leaves existing cards null. It does not change card IDs, protected numbers, fingerprints, matching keys or card versions. The isolated migration test also verifies session continuity.
- The form clears accepted nickname drafts, retains them alongside card input on recoverable errors/uncertainty, and disables both fields while saving. Route changes/disposal clear the draft; no browser persistence is introduced.
- Nicknames are ordinary labels and do not enter encryption purposes, matching fingerprints, number uniqueness or stable render keys.

No package, unrelated use case or ordinary development-database change is included. The existing uncommitted Phase 2 implementation and the user's comment removals were retained.

## Observed red/green cycles

| Behavior | Red | Green |
| --- | --- | --- |
| Domain normalization/length | The initial pass-through implementation failed 5 of 7 cases for whitespace/null normalization and trimmed boundaries. | All 7 cases pass. |
| Application rejection | The focused oversized-nickname case failed because no validation exception was thrown and saving proceeded. | The focused case and all 53 Application tests pass; validation is before protection/store writes. |
| API/persistence/defaults | All 5 initial HTTP cases failed because nickname properties/defaults were absent. | Save/list/details now return persisted normalized nicknames; null/blank inputs, boundary lengths and defaults pass. |
| Existing Cash migration | The isolated pre-nickname schema test failed because Cash remained null after the generated column-only migration. | Adding the Cash backfill passes preservation checks for existing Cash IDs, card identity/ciphertext/fingerprint/version and BFF session continuity. |
| Form submission | The focused component test failed because the nickname field was absent. | The field now submits the draft, displays the normalized returned label and clears on acceptance. Component checks also cover length feedback, duplicate-label display, HTML encoding, viewer restrictions and draft recovery. |

The new round-trip checks caught an encoding error introduced while editing existing Unicode masking/validation literals. Those four source literals were restored to UTF-8; the complete regression suites then passed. This transient defect was corrected without weakening the masking assertion.

## Final checks

| Check | Result |
| --- | --- |
| Domain suite | 40 passed; 0 failed/skipped |
| Application suite | 53 passed; 0 failed/skipped |
| Client suite | 84 passed; 0 failed/skipped |
| Integration / isolated LocalDB suite | 136 passed; 0 failed/skipped, including 32 card/nickname cases |
| Complete serial Chrome suite | 12 passed; 0 failed/skipped (final run approximately 1m 16s) |
| Complete serial Edge suite | 12 passed; 0 failed/skipped (fresh final run approximately 1m 13s) |
| Solution build | Succeeded; 0 warnings/errors on the final incremental build |
| Release client publish | Succeeded; optional wasm-tools optimization workload is absent |
| Documentation/IDs/whitespace | Passed: unique comment IDs, UTF-8 literals, local links, 20 intact CSV use cases, changed/new-file whitespace and git diff --check |

The expanded browser workflow verifies nickname entry, trimmed save, blank field after acceptance, reload, display on both profile and payment-method pages, and nickname retention through a deliberately lost accepted response and explicit refresh. It retains permission-downgrade and narrow-layout checks. Chrome and Edge run serially, using only synthetic data and test-owned persistent keys/databases; no screenshots, traces or video are enabled.

The initial full Chrome run passed 11/12 workflows, including the expanded nickname workflow, but timed out in the existing account-registration/resend test. That case passed unchanged on a focused retry, then the complete Chrome suite passed 12/12. An Edge run resumed after an overnight pause and reported 11/12, with an existing cross-site sign-in test failing after more than eight hours of recorded test duration. Edge was rerun with fresh hosts; the final result is listed above. No account production code or account tests were changed to bypass these failures.

## Repeating checks

From the repository root, using the existing .NET SDK, SQL Server LocalDB and installed browsers:

```powershell
dotnet build PersonalFinance.slnx --no-restore
dotnet publish src/PersonalFinance.Client -c Release --no-restore

dotnet test --project tests/PersonalFinance.Domain.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.Application.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.Client.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.IntegrationTests --no-build --no-restore

dotnet test --project tests/PersonalFinance.Domain.Tests --no-build --no-restore -- --filter-class '*PaymentMethodNicknameTests'
dotnet test --project tests/PersonalFinance.IntegrationTests --no-build --no-restore -- --filter-class '*SaveCardTests'

$env:XUNIT_MAXPARALLELTHREADS = '1'
$env:PERSONAL_FINANCE_TEST_BROWSER = 'chrome'
dotnet test --project tests/PersonalFinance.EndToEndTests --no-build --no-restore
$env:PERSONAL_FINANCE_TEST_BROWSER = 'edge'
dotnet test --project tests/PersonalFinance.EndToEndTests --no-build --no-restore

git diff --check
```

Tests require local-service permissions and own isolated `PersonalFinance_UC001_Test_*` databases. The ordinary development database remains untouched. Actual period/transaction/import integration, deployed origins and other browsers retain the previously documented boundaries. Applying the migration to ordinary environments remains a separate agreed step; existing card key configuration requirements remain unchanged.

The nickname amendment is complete. Renaming existing payment methods, including Cash, remains deferred to the planned editing phase. No later use-case phase was started.
