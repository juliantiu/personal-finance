# UC-005 credit card payment methods implementation plan

Status: **Phase 1 completed on 11 November 2026. Phase 2 approved and completed on 12 November 2026, including local verification. Phases 3–6 are not started.**

## Sources and current baseline

`docs/v1.0/` is the latest numeric version and the only version directory directly under `docs/`, checked on 11 November 2026. Resolve the version again before implementation.

- [Proposal](../../PROPOSAL.md), [written UC-005](../use-cases.md#uc-005), and its [flow](flow.md), [states](states.md), and [sequence](sequence.md): profile payment methods, reuse across periods, explicit Cash, and truthful save failures.
- [Technical specification](../../technical.specification.md) and [logical schema](../../migrations/schema-design.md): Clean/Onion architecture, DDD, profile boundaries, PaymentMethod/CreditCard, resolved UC-005/A3, masked display, and sensitive-data handling.
- [UC-002 plan](../UC-002_create-and-access-budget-profiles/implementation-plan.md) and [UC-020 plan](../UC-020_sign-in-and-sign-out/implementation-plan.md): existing profile creation/access, BFF sessions, antiforgery, persistent protection keys, and local verification conventions.

At the user's request, `origin/develop` was fetched and merged into `v1.0/uc-005/save-credit-card-payment-methods`. The merge fast-forwarded to `089fb426325edf2557363e5c2ed0baa24fcbecdf` without conflicts. This is the implementation baseline; work present only on other feature branches is not assumed to be available.

The baseline already implements registration/confirmation, BFF sign-in/refresh/sign-out, profile creation, accessible-profile lists, and current owner/editor/view-only resolution. Sharing administration is separate; tests can seed grants. There are no card details or budget-period/transaction/import workflows yet.

Existing code reviewed includes `BudgetProfile`, the profile commands/queries and EF adapters, `BffCurrentAccount`, `ProfileEndpoints`, `AccountIdentityDbContext`, `PaymentMethodRecord`, `ProfileCreationConfiguration`, profile contracts/pages, protected BFF storage/key configuration, and the LocalDB/browser test fixtures.

Payment-method persistence currently permits only `cash`, with a four-character kind column. Supporting `credit_card` requires widening the column and changing its check constraint while preserving the filtered Cash uniqueness index. The profile detail DTO exposes only method kind, and the client keys rows by kind. Multiple cards require stable method IDs and identity-based rendering.

## Agreed scope and product decisions

UC-005 includes listing, saving, revealing, editing, retiring, and restoring profile cards. Owners and current editors can perform these actions. View-only participants can read masked identifiers, including retained history, but cannot reveal full numbers or make changes. Cash remains available, cannot be edited/retired through card operations, and is never implicitly selected as a default.

| Topic | Confirmed behavior |
| --- | --- |
| Input | Collect the full card number. Remove spaces and hyphens, require 12–19 ASCII digits, and require a valid Luhn checksum. Do not use a card-brand whitelist. Preserve leading zeros as text. |
| Nickname amendment | Optional trimmed nickname, blank to null, maximum 100 characters; duplicates allowed. Include storage/API, add-card input and masked lists. Existing/new Cash defaults to `Cash`; existing cards remain null. Renaming existing methods, including Cash, is deferred to Phase 4. |
| Dependencies | Implement normalization and Luhn using existing .NET capabilities without another validation library. No paid service or card-verification call is included. |
| Duplicates | A canonical number identifies at most one saved method in a profile, including retired methods. Reject an active duplicate with a clear field error. The same number may be saved independently in another profile. |
| Storage and reveal | Store the full number encrypted; ordinary responses/display remain masked. Only owners/editors may explicitly reveal the full number. Store no security code or banking credentials. |
| Editing | Permit number changes on the same saved method even after use. Keep its ID and references. Apply the same normalization, validation, and duplicate rules as creation. |
| Removal | Retire the method rather than physically deleting it. Retain its current encrypted number, identity, and historical references, including authorized reveal. Exclude retired methods from future transaction selection and automatic import matching. |
| Re-adding | Saving the same number as a retired method restores that method and ID rather than creating a new one. |
| Concurrency | Reject stale changes, preserve the draft, and require an explicit refresh before another submission. Never silently overwrite a newer accepted change. |

Editing a number changes the current card details reached through existing historical references. This deliberately permits past payments to display the corrected current number; no old-number snapshot or alias history is introduced. It does not rewrite original imported statement evidence. Editing does not itself change whether a method is retired.

Phase 2 reconciles the written UC-005 record, CSV, diagrams, proposal and applicable specification/schema text with these confirmed decisions. Unrelated use cases and proposed policies remain unchanged. Reconciliation records the confirmed full scope; it does not mark later implementation phases complete.

Luhn validates number consistency, not whether an account was issued or remains active. See [Cybersource's check-digit description](https://developer.cybersource.com/docs/cybs/en-us/test-data/developer/all/so/test-data/best_practices_intro/checking_digit_scheme.html) and [Authorize.net's explanation of its limits](https://support.authorize.net/knowledgebase/article/000001556/en-us). The 12–19 range is the user-selected supported-input policy.

## Acceptance criteria

1. Every request resolves the current eligible BFF account and current profile access server-side. Owners/editors can write and reveal; viewers can read only masked data. Removed grants and role downgrades take effect on subsequent requests.
2. Inaccessible profiles/methods and nonexistent IDs receive the same non-disclosing unavailable response. A method ID from another profile cannot authorize any read, reveal, edit, retirement, or restoration. A known viewer's forbidden write/reveal receives a safe permission response without card details.
3. Lists contain stable IDs, kind, masked identifier, active/retired state, version, and permitted actions. They contain neither full numbers, ciphertext, nor matching fingerprints. Cash remains exactly one existing option per profile.
4. Valid input saves exactly one profile card with encrypted canonical number and a stable ID. Invalid length, characters, checksum, or empty input produces field feedback with no persisted effects. Validation/error details never echo the full input.
5. Equivalent space/hyphen formatting is one identity. Distinct full numbers with the same last four digits remain distinct. Duplicate constraints hold for concurrent requests and direct provider-level persistence, not just an application precheck.
6. Card encryption, matching fingerprint, masked suffix, and lifecycle/version changes commit atomically. Known failures roll back; a lost response is shown as uncertain, with a refresh action before an explicit retry and no automatic repeat write.
7. An explicit owner/editor reveal returns only the requested card's current full number. Viewers, invalid sessions, cross-profile IDs, and removed/downgraded access cannot reveal. Retired cards retain this same reveal policy. The client clears revealed content on hide, dialog close, route change, disposal, or observed access/session loss, and ignores late responses.
8. Editing retains method ID/profile/kind/references, replaces the protected number/fingerprint/suffix together, and preserves retirement state. The prior number stops matching that method; the new number follows the profile uniqueness rule. A duplicate edit, including another method's retired number, changes nothing.
9. Retirement keeps the row and protected details, makes the method unavailable for new selection/matching, and leaves historical references intact. Repeated removal with an outdated version produces a conflict; after refresh, the UI shows the existing retired state rather than issuing another removal.
10. Saving a retired number restores its original ID and references and advances its version. Competing saves/restorations cannot create two methods or overwrite a simultaneous accepted change.
11. Edit/remove requests carry the expected version. Competing edits, edit/remove races, and stale changes after restoration produce a conflict with no partial update. Clients retain the draft in memory and require refresh; they do not automatically resubmit it.
12. Normal list/save/edit/retire/restore responses remain masked. Full numbers exist in browser memory only for entry/edit or explicit reveal, never in URLs, browser persistence, diagnostics, or test artifacts. Sensitive responses use `Cache-Control: no-store`; reveal also uses an explicit antiforgery-protected POST.
13. Migrations preserve existing Identity/OpenIddict/BFF/profile data, Cash IDs, Cash uniqueness, and profile creation. No unrelated financial tables or speculative transaction references are created.
14. Local tests demonstrate all implemented behavior through the applicable Domain, Application, SQL/HTTP, component, and browser boundaries. Tests against actual period/transaction/import workflows are required when those workflows exist; their absence is not represented as completed historical/import acceptance.

## Proposed implementation across layers

The following technical design is part of the plan for approval; the product decisions above are already confirmed.

- **Domain:** The approved 13 November 2026 refactor introduces `PaymentMethod`, owning stable ID, profile ID, `PaymentMethodKind`, normalized nickname and an optional validated `CardNumber`. Controlled Cash/card factories enforce construction rules. `BudgetProfile` owns its initial Cash entity and derives its Cash ID from it. Nickname normalization belongs to the entity; the separate static helper is removed. Keep the existing `CardNumber` wrapper for canonicalization, Luhn and masked diagnostics. Domain code stays independent of EF, HTTP, Identity, encryption providers and MudBlazor. Reveal, editing and retirement/restoration remain in their separately approved phases.
- **Application:** Add focused Mediator queries/commands for listing, saving/restoring, revealing, editing, and retiring cards. Reuse `ICurrentAccount`; add the minimal access/persistence/protection ports required by these behaviors. Authenticate and authorize the resource before exposing card-specific validation. Use FluentValidation for request shape and authoritative domain behavior for canonical number rules. Recheck current access and expected record state at the write boundary.
- **Infrastructure:** Extend existing payment-method persistence rather than add a second profile/payment store. The proposed physical mapping keeps card subtype columns on `PaymentMethods`: protected number, 32-byte keyed fingerprint, matching-key identifier, last four digits, nullable `DateTimeOffset` retirement time, and SQL `rowversion`. Cash has no card details and cannot be retired. Add shape constraints for Cash/card rows and a unique filtered index on `(ProfileId, NumberFingerprint)` for all cards, including retired cards. This physically colocates the logical CreditCard subtype and allows SQL Server to enforce profile uniqueness without a uniqueness constraint across a join. Preserve the existing filtered Cash index.
- **Protection and matching:** Reuse the already-referenced ASP.NET Core Data Protection provider with a dedicated card purpose chain bound to profile and method IDs. Store a separate HMAC-SHA-256 fingerprint over an unambiguous profile-ID/canonical-number encoding using a dedicated server-held random 256-bit matching key. Use existing .NET cryptographic APIs; add no package. Fingerprints support exact duplicate lookup without decrypting every row; suffixes support masked display only. No full number goes into a key purpose, exception, or log. Use a separate Application protection port so inner layers never reference Data Protection.
- **Key readiness:** Persistent card writes require a stable encrypted key ring and stable matching-key configuration; disposable isolated test hosts can explicitly use synthetic keys. Reuse UC-020's persistent key/certificate setup. Keep existing account/profile features available if card-specific configuration is missing, but make affected card operations safely unavailable. Retain decryption keys needed by stored cards; never regenerate the matching key on startup. Derive a nonsecret matching-key identifier from SHA-256 of the random key and store it with each card; reject card writes if stored identifiers disagree with the configured key. Do not include that identifier in the uniqueness index, which would allow duplicate numbers across key generations. Document backup/restore and a controlled fingerprint reindex procedure before any matching-key replacement. Changing the key without reindexing must fail readiness rather than silently bypass uniqueness. Microsoft documents [long-term protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/) and the [key lifetime/retention requirement](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings); HMAC is available in [.NET's existing cryptographic API](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha256.hashdata?view=net-10.0).
- **API and contracts:** Use finance Minimal APIs, the existing BFF cookie/session, exact-origin credentialed CORS, and antiforgery for mutations and reveal. Keep DTOs separate from domain/EF entities. Proposed routes are `GET/POST /api/budget-profiles/{profileId}/payment-methods`, `PUT/DELETE .../payment-methods/{methodId}`, and `POST .../payment-methods/{methodId}/reveal`. POST saves or restores based on the canonical number. PUT and DELETE carry the expected version using `If-Match`; missing/invalid versions receive validation feedback. Use `400` field errors, `401` session failure, `403` for an accessible viewer's forbidden action, non-disclosing `404`, `409` stale/duplicate conflicts with a safe field code where applicable, and safe dependency failures.
- **Client:** Add a protected payment-method destination from profile details. Show active and retired sections, masked identifiers, and role-appropriate actions. Edit/reveal use explicit number handling; ordinary list loading never fetches plaintext. Use stable IDs in both the management page and existing profile landing projection so multiple cards cannot collide under a kind-based render key. Preserve recoverable drafts only in component memory; clear sensitive state on navigation/session loss. Explain uncertain outcomes and require explicit refresh/retry.
- **Tests and composition:** Reuse xUnit v3, bUnit, isolated SQL Server LocalDB fixtures, and serial Playwright. Keep services scoped. Use only synthetic numbers/accounts and test-owned keys/databases; disable sensitive browser captures. Follow the repository's unique class/method purpose-comment requirements outside the Persistence exemption.

## Testable implementation phases

Each phase is a vertical slice. For each next behavior, write and run a focused test, observe failure because the behavior is absent or wrong, implement the minimum across its required layers, rerun to green, and refactor while green. Setup failures do not count as the red state. Finish the phase's verification and explain its layer flow before pausing at the learning checkpoint. Proceed to another phase only when the user indicates readiness.

### Phase 1 Review profile payment methods

**Status:** Complete on 11 November 2026. See [Phase 1 verification](verification-phase-1.md) for observed red/green evidence and passing regression results.

Add an access-scoped list query/EF projection, stable-ID DTO, no-store GET endpoint, protected payment-method page, and navigation from the profile. Initially show the existing Cash method. Adjust the existing profile payment-method projection/render keys to use IDs. Do not add card columns, protection configuration, or inactive mutation controls yet.

**Red and green checks:** Demonstrate the missing route/page/navigation first. Application/HTTP tests cover owner/editor/viewer reads, missing and cross-profile IDs, revoked grants, invalid/ineligible sessions, and no-store responses. Component tests cover loading, genuine empty versus failed load, unavailable/session-loss states, same-component route changes, and late responses. Add a focused browser navigation/deep-link check.

**Exit criterion:** Participants can review the authoritative existing methods without data leakage or stale profile content. No migration is required for this phase.

### Phase 2 Save validated encrypted cards

**Status:** Complete on 12 November 2026. See [Phase 2 verification](verification-phase-2.md) for observed red/green results, full regression counts and environment limits, and [card protection operations](card-protection.md) for backend configuration and recovery notes.

Introduce canonical number rules, save command, current owner/editor write authorization, protection/fingerprint adapters, the card persistence extension/migration, POST endpoint, and add-card form. Persist protected number/fingerprint/suffix atomically. Enforce profile duplicates in SQL and map the known constraint failure to safe field feedback. Update the list to display multiple masked cards. Add truthful save failure/uncertainty behavior immediately. Reconcile the baseline UC-005 documentation with the confirmed scope and input/storage decisions.

**Red and green checks:** Domain cases cover 11/12/19/20-digit boundaries with appropriate checksum controls, accepted spaces/hyphens, leading zeros, invalid checksums, letters/mask symbols, and non-ASCII digits. Application/HTTP tests cover validation without writes, owner/editor success, viewer denial, antiforgery, and access revoked before save. LocalDB tests verify migration from the baseline, Cash preservation, no plaintext at rest, duplicate enforcement independent of the handler, concurrent equivalent saves, same last four with different full numbers, and the same card in different profiles. Inject a failure to prove rollback. Test missing key configuration and protection failure without affecting existing registration/profile routes. Component/browser cases cover masked save/reload, field errors, double-submit prevention, connection uncertainty, and explicit refresh before retry.

**Exit criterion:** An owner/editor can save a valid encrypted card and reload its masked entry. Invalid, forbidden, duplicate, and failed requests create no partial method. No extra validation or cryptographic package is installed.

### Phase 3 Reveal a card with current writer permission

**Status:** Not started.

Add a dedicated application operation, exact scoped lookup/decryption adapter, antiforgery-protected reveal POST, and an explicit reveal/hide UI. Authorize before decrypting. Keep plaintext out of ordinary projections and success messages. Include retired-card reveal when retirement exists in Phase 5; do not create a premature retirement UI.

**Red and green checks:** First show that the explicit reveal operation is absent. Test current owner/editor success; viewer, cross-profile, removed/downgraded grant, and invalid-session denial; missing/invalid antiforgery; no-store; corrupted ciphertext and unavailable keys without plaintext/error leakage; and purpose binding so ciphertext copied to another method/profile does not decrypt. Component/browser tests verify no reveal request during list loading, clearing on hide/close/navigation/session loss, and rejection of a delayed reveal response after switching profiles. A persistent-key local host restart must still reveal the saved synthetic card.

**Exit criterion:** Only a current owner/editor can explicitly retrieve the requested full number, and the client does not retain it after the relevant UI context ends.

### Phase 4 Edit existing payment-method details

**Status:** Not started.

Add a versioned edit command/store operation, PUT contract/endpoint, and edit form using the existing role/reveal boundaries. Permit changing the number on the same method even after use. Update protected number/fingerprint/suffix/version together, keep the ID/profile/kind and retirement state, and exclude the same method from its own duplicate check. Do not add number-history/version entities or rewrite raw import records. Number entry and edited input follow Phase 2 validation. The approved nickname amendment also defers renaming existing methods, including Cash, to this phase; retain the optional trimmed 100-character rule and duplicate-name allowance. Cash remains protected from card-number changes and card retirement.

**Red and green checks:** Test persisted ID/profile preservation; formatting-only and same-number saves; replacement of suffix/ciphertext/fingerprint; old-number lookup no longer identifying this card; new-number duplicate enforcement including provider-level concurrent races; forbidden/cross-profile edits; and a fault leaving all old details intact. Two editors starting with one version must produce one accepted edit and one conflict. Component/browser cases verify validation, masked reread, reveal of the new number, preserved drafts on conflict, and refresh without automatic resubmission. The real after-use transaction scenario remains in the dependency checks below.

**Exit criterion:** A number correction updates one existing method atomically and stale requests cannot overwrite it. References will continue to target the unchanged method ID.

### Phase 5 Retire and restore cards

**Status:** Not started.

Add domain/application lifecycle behavior, conditional retirement writes, DELETE endpoint, active/retired sections and remove control, and restoration through the existing save operation when its canonical number belongs to a retired method. Preserve the original ID and protected details; advance the version on every lifecycle transition. Saving a retired number uses a conditional update against its observed version so a concurrent edit/removal/restoration cannot silently win. Editing a retired method preserves its retired state. Retired cards remain masked/revealable under the same role policy. Cash cannot enter these operations.

**Red and green checks:** Test retirement without physical deletion; timezone-aware retirement storage; retained owner/editor reveal; viewer masked reads; Cash protection; filtering retired cards from the active-option projection; restoration of the exact original ID; active duplicate rejection; same-number saves racing with restoration; editing a retired card without reactivation; and edit/remove/restore conflicts. Use failure injection to prove no half-applied state transition. Component/browser cases cover removal, retired display, re-saving/restoration, refreshed versions, and draft/conflict handling. Keep actual selection/import tests gated on their owning workflows.

**Exit criterion:** A card can stop future availability and later return with its original identity. Its history is retained, active uniqueness remains enforced, and conflicting requests leave an explicit recoverable state.

### Phase 6 Complete local acceptance and verification evidence

**Status:** Not started.

Exercise the full implemented workflow through the published Blazor client, actual BFF/API, and isolated LocalDB: save, reload masked list, reveal/hide, edit/reveal corrected number, duplicate error, retire, and restore the original method. Include owner/editor/viewer, another private profile, revoked/downgraded access, session loss, and representative network/uncertain-write failures. Add missing acceptance tests through focused red/green cycles rather than recreating the earlier suites.

Run the complete applicable Domain/Application/Client/Integration suites, solution build, Release client publish, serial Chrome/Edge browser acceptance, and `git diff --check`. Run existing registration/confirmation, BFF sign-in/refresh/sign-out, profile creation/defaults, opening, and switching regressions. Reuse the existing browser matrix where available; record WebKit failures, unavailable Brave, blocked Firefox startup, and actual Safari separately rather than claiming coverage from another browser.

Verify key persistence across restart, retention of old decrypting keys after normal protection-key rotation, stable fingerprints across restart, key-readiness failure on an unapproved matching-key replacement, and safe corruption/dependency responses. Document the matching-key reindex/backup/recovery procedure and remaining deployment checks. Add no cloud resources or automatic background migration/rotation job.

**Exit criterion:** Every implemented acceptance criterion has observed local evidence, a repeatable command, and clear environment limits. Record exact red/green results, test counts, migration/protection checks, and blocked/deferred checks in a UC-005 verification document. Mark each phase complete only after its work and required checks pass.

## Verification boundaries and future integration

Use synthetic accounts and Luhn-valid test numbers, fake/captured email, temporary synthetic protection keys, and uniquely named test-owned LocalDB databases. Migrations apply to isolated fixtures first; applying them to the ordinary development database remains an explicit implementation step for agreement. Never use real cards or developer data in tests or retained screenshots/traces.

Budget periods, transactions, and statement import are absent from this baseline. UC-005 can demonstrate profile-scoped persistence, identity continuity, lifecycle filtering, and exact canonical-number lookup without inventing financial tables or a public matching API. When UC-009/UC-010/UC-011 exist, their acceptance must verify:

- Multiple periods select the same profile method ID; another profile's method is rejected.
- An approved transaction retains its method reference after the number is edited and displays the method's current corrected details, without altering amount/date/period or original statement evidence.
- Retirement does not erase historical payments; retired methods cannot be newly selected or automatically matched. Restoration makes the original method available again.
- Exact full-number import lookup uses the current canonical number, current profile scope, and active state. Masked/partial statement formats and ambiguous matches require their own explicit import policy; this plan does not silently match by last four digits.

Until those workflows exist, these are tracked integration gates rather than completed end-to-end claims. UC-005's implemented local scope can be completed with those dependencies explicitly recorded.

## Exclusions and approval

This plan does not implement sharing administration, payment processing, live card-account verification, card expiry/security codes, default payment selection, permanent card erasure, manual restoration outside re-saving, transaction/import screens, bank-format extraction, cloud provisioning, or unrelated category/currency work. Existing account-erasure rules still govern eventual permanent erasure of profile-owned card data.

The user authorized the completed `origin/develop` merge and the original documentation task. After review of the concrete Phase 1 implementation plan, the user explicitly approved Phase 1 on 11 November 2026. That approval covers the read-only payment-method slice and its tests and verification documentation. It does not authorize starting another phase or changing the ordinary development database. No builds, test suites, or migrations were run while originally preparing this plan; subsequent implementation checks are recorded separately.

The user explicitly approved the concrete Phase 2 plan on 12 November 2026 and asked for implementation through completion. That approval covers validated encrypted saving, current write authorization, card protection/fingerprints and readiness, the persistence extension/migration, masked projections/form, truthful failure recovery, red/green and regression checks, and reconciliation/verification documentation. It does not authorize applying the migration to the ordinary development database or starting Phase 3.

**Checkpoint:** Phase 2 is complete and work stops at its learning checkpoint. Phases 3–6 remain not started and subject to the project's clarification and plan-approval process.

## Nickname amendment — 12 November 2026

**Status:** Complete, including red/green, migration, regression and Chrome/Edge verification. Renaming existing methods remains deferred.

The user approved the concrete nickname plan after confirming storage/API, add-card input, masked-list display, optional trimmed text (blank to null, at most 100 characters), duplicate names allowed and Cash default `Cash`. Renaming existing methods, including Cash, is explicitly deferred to the planned editing phase. This amendment does not start Phase 3 or apply a migration to the ordinary development database. See [nickname verification](verification-nicknames.md).

## PaymentMethod entity refactor — 13 November 2026

**Status:** Complete, including observed red/green cycles, 323 non-browser regression tests and 12 workflows in each of Chrome and Edge. No new migration was required and no later feature phase was started.

The user approved the concrete refactor plan after reviewing the distinction between a payment-method entity, a validated card-number value and nickname rules. The completed save/profile-creation slice now constructs `PaymentMethod` in Domain, normalizes nicknames there, and passes the entity through the card write port together with protected details. `PaymentMethodRecord` remains an Infrastructure representation; masked reads remain projections. No new migration, API/UI behavior or editing/reveal/retirement feature is part of this refactor. The existing ordinary database remains untouched. See [refactor verification](verification-payment-method-entity.md).
