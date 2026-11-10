# UC-019 — Configure profile currencies — Implementation plan

Status: **Phase 1 completed 8 November 2026. Phase 2 completed 10 November 2026. Phase 3 remains pending the UC-009–UC-011 financial/source records.**

## Sources and planning baseline

- `docs/v1.0/` is the latest and only version directory directly under `docs/`, checked on 8 November 2026. Resolve the latest version again before implementation.
- [UC-019 written specification](../use-cases.md#uc-019), [flow](flow.md), [states](states.md), and [sequence](sequence.md).
- [UC-002 implementation plan](../UC-002_create-and-access-budget-profiles/implementation-plan.md), especially its initial currency setup and explicit exclusion of post-creation editing.
- [Technical specification](../../technical.specification.md) and [logical schema](../../migrations/schema-design.md), particularly profile ownership/access, Currency, ProfileCurrency, and schema policies A12/A13, which are resolved for this plan below.
- Current code already creates profiles with a selected GBP or USD main currency and zero or one additional currency. Profile details show the saved values and effective role. There is no currency settings editor, currency-specific query/command, or post-creation mutation endpoint.
- The current catalog contains GBP and USD. Profile writes use the existing authenticated BFF account and antiforgery patterns. Owners and editors are writers; view-only participants may review settings.

## Scope and behavior

Allow an authorized participant to review a profile's supported currency choices, main currency, and enabled additional currencies after profile creation. Owners and editors can configure the profile; view-only participants can review settings but cannot save. The main currency is explicitly chosen, and additional currencies are optional. Initially, supported choices remain GBP and USD. The main currency is implicitly enabled and is not duplicated as an additional-currency membership.

Main-currency amounts alone remain the calculation basis. Additional-currency values are manually entered descriptions of the same event, do not affect calculations or totals, and are never derived through exchange-rate conversion. UC-011 foreign-source approval continues to require a manually entered main-currency amount. Currency settings changes must not alter or relabel historical financial facts.

This plan covers the post-creation configuration and review slice of UC-019. Initial currency selection during profile creation is already implemented under UC-002. It does not add currencies beyond GBP/USD, exchange rates, automatic conversion, financial transaction entry, or import approval.

## Product decisions and remaining questions

### Confirmed by the user — 8 November 2026

- **A12 — main-currency lifecycle:** Do not change the main currency after it is established during profile creation. This applies even before financial facts exist. No relabeling, conversion, or revaluation behavior is needed because the main currency cannot be changed.
- **A13 — additional-currency lifecycle:** Prevent disabling an additional currency after financial records use it.
- **A13 — imported source currencies:** A source currency must be enabled in the profile before an imported transaction in that currency can be approved.
- **Currency use:** Only approved financial records prevent disabling an additional currency. A pending import in that currency does not; if it is disabled, the source remains pending until the currency is enabled again.
- **Re-enabling:** An additional currency that was disabled while unused can be enabled again later.
- **Concurrent edits:** Reject a stale settings save with a conflict; do not silently overwrite a newer configuration.

### Remaining decisions before affected implementation phases

No product-policy decisions currently block drafting the first implementation phase. Resolve any newly discovered ambiguity before implementing the affected behavior.

If an unresolved decision affects a phase, pause that phase and revise this plan after the decision. Do not substitute a silent default.

## Acceptance criteria

1. A participant with current access can retrieve and review the profile's supported currency choices, saved main currency, enabled alternatives, and effective role. An inaccessible or nonexistent profile reveals no settings.
2. Owners and editors can save a valid configuration; a view-only participant cannot change it, including by calling the API directly.
3. The main currency is explicitly selected from GBP/USD. Zero additional currencies is valid. Additional currencies must be supported, distinct, and different from the main currency.
4. Invalid or unauthorized writes leave the persisted settings unchanged and return safe, actionable feedback. The UI does not claim a failed or uncertain write was saved and preserves the user's draft when recovery is possible.
5. A successful save is visible on a fresh read and after navigating away and back. The displayed role and values come from the server's current access/settings, not stale client state.
6. Settings changes do not create exchange rates, convert monetary values, modify historical amounts, or change which amounts contribute to calculations.
7. The main currency selected during profile creation cannot be changed. Disabling an additional currency is blocked if an approved financial record uses it. A pending import alone does not block disabling, but its source remains pending and cannot be approved until that currency is enabled again.
8. An unused additional currency can be disabled and later re-enabled. It remains distinct from the main currency and does not change historical facts.
9. Concurrent settings saves use optimistic concurrency: a stale save is rejected with a conflict and does not overwrite the current configuration.
10. State-changing browser requests use the current opaque BFF session and antiforgery protection. No access/refresh token or financial data is exposed through the new contracts or logs.

## Affected layers

- **Domain:** Add or extend only the profile currency configuration behavior needed to validate a selected supported main currency and optional additional currencies. Keep catalog metadata, persistence, permissions, HTTP, and UI out of Domain. Enforce confirmed lifecycle invariants here where they are true domain rules.
- **Application:** Add a settings query and update command with explicit profile access/writer checks. Resolve the acting account from the existing current-account boundary. Validate request shape and coordinate the domain operation through focused ports; do not trust a client-supplied role or owner ID.
- **Infrastructure:** Read the GBP/USD catalog and current configuration from the existing tables. Persist main/additional updates atomically, maintain the implicit-main/no-duplicate-membership rule, and enforce confirmed usage/lifecycle policies against persisted references. Add only schema changes needed for an approved concurrency policy or required constraints.
- **API/BFF:** Add browser-safe read and update contracts and profile-scoped currency settings endpoints. Reuse current session resolution, antiforgery, no-store responses, safe validation/error handling, and non-disclosing inaccessible-profile behavior.
- **Blazor client:** Add a currency settings view/editor reachable from a profile. Show a read-only form for viewers and save controls only for owners/editors. Provide loading, validation, saved, failure, and uncertain-result states; refresh displayed settings from the server after a successful save.
- **Tests/composition:** Use the existing xUnit, bUnit, LocalDB, and Playwright setup. Add focused tests before each production behavior. Keep LocalDB and browser data synthetic and isolated. Add required unique purpose comments for new production classes and methods outside Infrastructure Persistence; that subtree is exempt under `AGENTS.md`.

## Smaller implementation phases and red/green verification

Each phase is a vertical, independently reviewable slice. For each behavior, write and run a focused test first, confirm the intended failure, implement the minimum across the required layers, then rerun to green and refactor while green. Complete and review a phase before starting the next. A phase approval does not authorize later phases.

1. **Completed 8 November 2026 — Review currency settings and supported choices.**
   - Add an authorized settings query that returns the current configuration, GBP/USD choices, and effective role. Add a profile settings view reachable from the existing profile page. All current participants can review; only owners/editors see the edit affordance planned for Phase 2.
   - Reuse existing tables and authorization boundaries; do not introduce a mutation or new currency policy in this phase.
   - **Red/green:** First demonstrate absent settings navigation/query behavior. Cover owner, editor, and viewer reads; catalog contents; missing/inaccessible IDs; no settings leakage; and load failures that are not presented as empty/default settings. Component tests cover loading, populated, failure, and role-appropriate affordances.
   - **Checkpoint outcome:** A participant can review the authoritative currency configuration and available choices, with access behavior established for the later write phase.
   - **Completion evidence:** Added an access-scoped query and no-store GET endpoint that return saved currencies, current role, and the GBP/USD catalog. Added a read-only currency settings route reachable from profile details, with distinct loading, unavailable, and load-error states. The focused API test first returned **404** while the endpoint was unmapped, then passed after implementation; the profile-navigation component test first failed because the link was absent, then passed after implementation. The full Client suite passed **63 tests**; all **30 profile Integration/LocalDB tests** passed; the focused currency-settings Integration test passed with owner/editor/viewer, inaccessible/missing IDs, and anonymous-session cases; the solution build succeeded with **0 warnings and 0 errors**. No migration or schema change was needed.

2. **Completed 10 November 2026 — Configure additional currencies after profile creation.**
   - Keep the main currency selected at profile creation read-only in the settings editor. Add the Domain behavior, Application update command and writer authorization, atomic Infrastructure update, API contract/endpoint, and client form/save flow for optional additional currencies. Support zero or one currently supported alternative and keep the main currency out of `ProfileCurrency` membership rows. Add a version token to the mutable profile configuration and reject stale writes with a conflict.
   - Do not add a broader conversion or historical-revaluation workflow.
   - **Red/green:** Cover no alternative, the valid alternative, duplicate/main-as-alternative, owner/editor success, viewer denial, stale concurrent update returning a conflict without overwriting the winner, antiforgery failure, persistence rollback/failure, safe response feedback, draft preservation, and refreshed settings after save. Use Domain/Application tests, LocalDB/API integration, bUnit, and a focused browser workflow as appropriate.
   - **Checkpoint outcome:** An owner/editor can safely update a valid configuration, and readers see the saved result while unauthorized or invalid writes leave it unchanged.
   - **Completion evidence:** Added fixed-main domain validation, an owner/editor-only update command and API endpoint, atomic membership updates, an optimistic configuration version, and a browser editor that preserves drafts on failure and reloads saved server state after success. The focused API test showed red with the PUT route unmapped (**405** instead of save); after mapping it, the focused currency Integration class passed **4 tests**. Additional evidence: Domain **22 passed**, Application **46 passed**, Client **65 passed**, all profile Integration/LocalDB tests **33 passed**, and the focused Edge browser workflow **1 passed**. The migration was applied only to isolated LocalDB test databases; the migration-upgrade regression preserved existing accounts, profile access and settings. The full solution build succeeded with **0 errors** and 13 pre-existing Integration test warnings; Release client publish succeeded. Exact commands and notes are in [Phase 2 verification](verification-phase-2.md).

3. **Enforce currency-use lifecycle at financial boundaries.**
   - This phase depends on the financial/source records from UC-009–UC-011 being present. Enforce the confirmed rule that used additional currencies cannot be disabled, while allowing an unused disabled currency to be re-enabled. A pending source record does not prevent disabling, but the UC-011 approval boundary must leave it pending until its source currency is enabled again. Coordinate that approval check without adding transaction entry or statement-import features to UC-019.
   - If UC-009–UC-011 are not implemented when this phase is reached, pause it and keep the lifecycle behavior explicitly pending; do not invent placeholder financial tables or claim reference checks are complete.
   - **Red/green:** Use LocalDB integration cases with synthetic historical references to reproduce the missing protection. Verify disabling is rejected when approved records use the currency; pending imports alone do not block disabling; the currency can be re-enabled; and a foreign import cannot be approved while its source currency is disabled. Verify rejected approval leaves the source pending, rollback/no partial writes, profile ownership scoping, and no historical amounts or financial outcomes changing after settings updates.
   - **Checkpoint outcome:** Confirmed currency lifecycle policies hold against actual persisted financial/source references, not only in request validation.

4. **Complete UC-019 acceptance and record evidence.**
   - Run the focused Domain, Application, Client, Integration/LocalDB, and browser suites for the completed scope plus relevant UC-002 and authentication/session regressions. Run the solution build and applicable publish/check commands used by the repository.
   - Verify the settings workflow through the browser with synthetic owner, editor, and viewer accounts; confirm deep-link access, navigation back to the profile, save/reload, session loss, access removal, validation failure, and recoverable persistence/network failure.
   - Record exact commands, observed red/green results, test counts, migration checks, environment constraints, and unrun gates in a UC-019 verification document. Update phase status only when its required implementation and checks have actually completed.
   - **Checkpoint outcome:** UC-019's approved post-creation currency configuration behavior has repeatable evidence, with unresolved or environment-limited items called out accurately.

## Verification rules

Use domain tests for currency invariants, Application tests for orchestration and access decisions, LocalDB integration tests for persisted references/constraints/atomicity, bUnit for edit and error states, and a small Playwright workflow for the authorized end-to-end settings interaction. A setup failure is not evidence of the intended red state. Use isolated test-owned databases; verify schema upgrades preserve existing accounts, sessions, profiles, and currency settings. Never apply a migration to a non-test database without a separately agreed implementation step. Do not claim browser, concurrency, or lifecycle coverage beyond the behaviors actually exercised.

Implementation status: **Phase 2 is complete and verified. Phase 3 is not authorized by this checkpoint and depends on the financial/source records from UC-009–UC-011.**

## Explicit exclusions and dependencies

- Initial currency selection during profile creation; already delivered under UC-002.
- Currency catalog expansion beyond GBP/USD, exchange-rate services/storage, automatic conversion, and historical revaluation/relabeling unless later specified and approved.
- Transaction entry/editing, statement import/review, foreign-source approval implementation, and calculations; those belong to UC-009–UC-011. Phase 3 only adds UC-019 lifecycle checks at those boundaries after their records exist.
- Profile sharing/grant-management workflows; this use case consumes current owner/editor/viewer access and does not create or change grants.

## Approval and phase checkpoints

The plan is a draft for review. The user's request to create this document authorizes the documentation change only; it does not authorize application implementation. Before implementation, present the concrete first-phase plan for approval. If implementation reveals a new ambiguity or required scope change, pause that affected work and revise this plan. After each approved phase, report the changes and actual verification evidence, then pause for the user's questions/readiness before proceeding to the next phase.
