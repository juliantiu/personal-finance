# UC-005 — Phase 2 verification

Date: **12 November 2026**. Scope: [Phase 2 — Save validated encrypted cards](implementation-plan.md#phase-2-save-validated-encrypted-cards), explicitly approved by the user after the concrete plan was presented. `docs/v1.0/` was resolved again as the latest and only numeric version before implementation and reconciliation.

**Status:** Phase 2 complete. Phases 3–6 are not started.

## Implemented behavior and layer flow

- `CardNumber` enforces canonical text: remove spaces/hyphens, preserve leading zeros, require 12–19 ASCII digits and Luhn, without a brand whitelist. Its diagnostic representation is masked.
- The protected payment-method page submits one credentialed antiforgery-protected POST. The API translates safe input/output only. `SaveCardHandler` resolves the eligible BFF account, checks current profile role before card-specific FluentValidation/domain validation, then invokes protection and persistence ports. Owners/editors save; viewers receive a safe 403; inaccessible/nonexistent profiles receive a non-disclosing 404; invalid/ineligible sessions receive 401.
- Infrastructure encrypts with profile/method-bound Data Protection purposes and derives a keyed profile-scoped matching fingerprint. The SQL adapter uses a transaction-owned application lock and serializable access/key/duplicate checks through commit. It maps provider errors through an Application exception rather than exposing EF/SQL concerns in the endpoint. Only the named fingerprint unique-index failure becomes duplicate feedback; other failures never report success.
- `AddEncryptedCards` extends the existing table, preserving Cash IDs and filtered uniqueness. SQL shape checks enforce coherent Cash/card rows. Encryption, fingerprint, key ID, suffix and rowversion persist together. Retirement metadata uses `datetimeoffset`; no lifecycle mutation is introduced here.
- Save, list and profile-details responses contain only stable IDs/kinds, masked suffixes, rowversion and retirement metadata. Full numbers, ciphertext, fingerprints and key identifiers are absent. Handled card responses use `Cache-Control: no-store`.
- The form updates during typing, uses password-style entry with browser autocomplete disabled, prevents double submission and clears accepted input. Drafts remain only in component memory. Navigation, disposal and observed access/session loss clear them and invalidate delayed work. An unconfirmed response requires explicit authoritative refresh and a separate explicit retry; no automatic POST repeats occur.
- Missing, malformed, wrong-length, nonpersistent or failed protection configuration safely prevents saving without disabling registration/profile creation or masked reads. Incompatible stored/configured matching-key identifiers prevent writes. Persistent keys and fingerprints survive a host restart.
- The proposal, written UC-005/CSV, three Mermaid diagrams, applicable specification and logical-schema text are reconciled with confirmed decisions. [Protection operations](card-protection.md) document configuration, backups, retained decryption keys, safe downgrade behavior and a controlled matching-key reindex procedure.

No extra validation/cryptographic package, financial transaction/import table, deployment, real card, or ordinary development-database mutation is included.

## Observed red/green evidence

| Behavior | Observed red | Observed green |
| --- | --- | --- |
| Canonical number rules | Against the initial nonvalidating factory, 4 valid-input cases failed and 11 invalid-input cases passed. | All 15 number cases pass, including controlled 11/12/19/20-digit boundaries, formatting, leading zeros, checksum, mask/letters and non-ASCII digits. One initially mistyped synthetic checksum fixture was corrected; that fixture error is not counted as implementation evidence. |
| Application orchestration | Against the unavailable handler, 5 of 6 focused cases failed on missing authentication/access/save behavior. | All 6 cases pass: owner/editor identity forwarding, authorization before validation, safe invalid input and protection failure without writes. |
| SQL/HTTP save | All 6 initial cases failed with 405 because the POST route was absent; fixtures had migrated and seeded successfully. | All 23 focused card cases pass. They cover permissions/session/antiforgery, current access recheck, validation/duplicates, exact identity across profiles, concurrent equivalent saves, direct SQL uniqueness/shape checks, encrypted/resource-bound storage, rollback after SQL insertion, key readiness/mismatch, persistent restart and baseline migration preservation. Downgrade refusal is also verified without discarding the saved card. |
| Component form/recovery | 8 of the initial 20 cases failed because add-card controls were absent. Later field-feedback cases exposed an actual Razor error-text binding issue. | All 23 focused page cases pass, including submitted input, masked success/clearing, field feedback, pending submission, delayed route result, explicit uncertainty refresh and session/access-loss clearing. |
| Real browser | The new Chrome workflow failed at the absent add-card heading against the prior published client. | The freshly published workflow passes validation, save/reload, duplicate feedback, and a deliberately dropped response after the server accepted a save. Explicit refresh finds that card with exactly one POST; viewer downgrade removes entry controls. |

The initial sandboxed LocalDB attempt could not access the local SQL service and was stopped; it is not a red state. Tests were rerun with local service permissions. Design-time EF scaffolding initially lacked an isolated connection setting; supplying a nonsecret design-only setting generated the migration without connecting to a database. Compilation/setup failures are excluded from red evidence.

The browser cycle exposed a correction-click layout shift when field feedback cleared on blur. Updating input during typing and binding the draft explicitly fixed the workflow. The first full Edge run passed 11/12 but missed the post-sign-in page wait; the test now explicitly awaits callback/navigation completion. The focused Edge workflow then passed. Final serial suite results are recorded below.

## Final regression results

| Check | Result |
| --- | --- |
| Domain suite | 33 passed; 0 failed/skipped |
| Application suite | 52 passed; 0 failed/skipped |
| Client suite | 80 passed; 0 failed/skipped |
| Integration / LocalDB suite | 127 passed; 0 failed/skipped, including all 23 card cases |
| Complete serial Chrome suite | 12 passed; 0 failed/skipped (final run approximately 1m 27s) |
| Complete serial Edge suite | 12 passed; 0 failed/skipped (final run approximately 1m 29s) |
| Solution build | Succeeded; 0 errors. Recompilation reports 13 pre-existing warnings in unchanged account tests (duplicate using/cancellation-token analyzer). |
| Release client publish | Succeeded; optional wasm-tools optimization workload is absent |
| Purpose-comment IDs / CSV / links / whitespace | 126 unique IDs with no duplicates; 20 intact CSV use cases; local documentation links resolve; changed/new files pass whitespace checks and git diff --check |

## Repeatable commands

Run from the repository root using the existing .NET 10 SDK, installed Chrome/Edge and SQL Server LocalDB. Tests own uniquely named `PersonalFinance_UC001_Test_*` databases and temporary synthetic keys. Local service access is required.

```powershell
dotnet build PersonalFinance.slnx --no-restore --verbosity minimal
dotnet publish src/PersonalFinance.Client -c Release --no-restore --verbosity minimal

dotnet test --project tests/PersonalFinance.Domain.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.Application.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.Client.Tests --no-build --no-restore
dotnet test --project tests/PersonalFinance.IntegrationTests --no-build --no-restore

dotnet test --project tests/PersonalFinance.IntegrationTests --no-build --no-restore -- --filter-class '*SaveCardTests'

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

## Boundaries

The migration is verified only on isolated test databases. Ordinary environments still need the separately agreed migration step, persistent encrypted key configuration and stable `Cards:MatchingKey` before card saving becomes available. No real secrets are installed.

Reveal, correction, retirement/restoration and stale mutation handling remain later phases. Actual budget-period selection, historical transaction references and imported-number matching await those workflows and are not claimed complete here. Deployed origins/cloud recovery and browsers other than the actual Chrome/Edge runs remain outside this phase's verification. Browser captures/traces/video are not enabled for the card workflow.

Phase 2 is complete after all required local checks. Work stops at this phase; the next phase is explicit owner/editor card reveal.
