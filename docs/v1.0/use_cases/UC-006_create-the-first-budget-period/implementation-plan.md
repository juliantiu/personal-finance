# UC-006 — Create the first budget period — Implementation plan

Status: **Draft. No implementation phase is approved or started. The product decisions listed below must be resolved before their affected phases, and each concrete phase plan requires approval before implementation.**

## Sources and planning baseline

The version directories directly under `docs/` were inspected for this planning task. `v1.0` is the only version and therefore the highest numeric version. All references below come from that version; historical snapshots were not used.

- [Proposal](../../PROPOSAL.md), [written UC-006](../use-cases.md#uc-006), [flow](flow.md), [sequence](sequence.md), and [states](states.md).
- [Logical schema](../../migrations/schema-design.md), particularly `BudgetPeriod`, `PeriodCategory`, `PeriodSubcategory`, D5/D11, and the proposed date/allocation constraints. It is a logical baseline, not evidence that these tables exist in the application.
- [Technical specification](../../technical.specification.md): Clean/Onion boundaries, DDD, Mediator, FluentValidation, EF Core, finance Minimal APIs, MudBlazor, SQL Server, and xUnit/bUnit/Playwright.
- [UC-002 implementation plan](../UC-002_create-and-access-budget-profiles/implementation-plan.md) and [UC-004 implementation plan](../UC-004_maintain-categories-and-subcategories/implementation-plan.md): phase checkpoints, vertically delivered behavior, resource-scoped access, isolated LocalDB verification, and red/green evidence.
- [UC-019 implementation plan](../UC-019_configure-profile-currencies/implementation-plan.md): profile main-currency configuration and its API/client boundaries.

The current code provides authenticated profile access, main-currency settings, category/subcategory reads and lifecycle, and owner/editor/viewer permissions. There is no budget-period domain behavior, period persistence/migration, period API contract or endpoint, or period client workflow. The current schema baseline models a period with dates and independent category/subcategory selections; subcategory allocations are per-period values in the profile main currency. A selected category may have no selected subcategory, and a selected subcategory requires its category selection. Income allocation is expected income (D5). No separate expected-income field is needed.

## Scope and intended behavior

A current owner or editor with access to a profile can create that profile's first budget period. The writer enters period dates, selects currently trackable categories and subcategories, enters a main-currency allocation for each selected subcategory, reviews category sums, and saves. Successful creation makes the period available for later financial activity and review. A participant who is a viewer may not create it.

- Use the profile main currency already configured by UC-019. Do not convert currency or accept alternative-currency allocations.
- Persist period dates and its own category/subcategory selection and allocation rows. Later changes to one period must not rewrite this period's saved allocations.
- Derive a category allocation as the sum of its selected subcategory allocations; do not persist a duplicate category total. Zero is a selected allocation value, distinct from omission.
- Exclude explicitly stopped categories, stopped subcategories, and subcategories whose parent category has stopped tracking from new selections. Omission from this period does not stop tracking and permits later selection.
- Recheck current profile access and item/profile relationships on every write. Do not trust client-supplied role, category, currency, or profile facts.
- Do not implement later-period copying, period review/correction, transactions, balances, reports, sharing management, restoration, or currency conversion.
- A failed or uncertain save is never shown as confirmed. Preserve the draft where safe and require an explicit retry after refreshing or rechecking an uncertain outcome.

## Decisions to resolve before affected implementation

These questions are not answered by the current UC-006 specification. Schema proposals and technical-specification suggestions remain proposals unless the user confirms them.

| Decision | Question to resolve | Gate |
| --- | --- | --- |
| Period dates | Must the start date be on or before the end date? Are both dates inclusive? The schema proposes `start_date <= end_date` and an inclusive end date, but the UC marks date-validation details open. | Phases 2–3 |
| Allocation domain and precision | Are allocations required to be nonnegative, and may they be zero? The UC distinguishes zero from omission, while schema A11 proposes nonnegative allocations. What decimal precision/scale and excess-fraction behavior should apply? The technical specification suggests `decimal(19,2)` and rejection rather than rounding, but that is also a proposal. | Phases 2–3 |
| Empty selections | May a period have no selected categories? May a selected category have no selected subcategories? The logical schema permits the latter, while the UC leaves empty-selection rules open. | Phases 2–3 |
| First-period race | If two authorized writers submit a first period concurrently, confirm that exactly one succeeds and the other receives a recoverable conflict, leaving no duplicate periods or partial selection rows. | Phase 2 |

Overlapping-period behavior is not needed to create the first period in isolation and remains in UC-007's scope. Do not invent a general overlap prohibition or a date-order tie-break here.

## Acceptance criteria

1. Current owners and editors can open the first-period workflow only for a currently accessible profile. Viewers can read existing profile information but cannot create a period. Anonymous, ineligible, revoked, and unrelated accounts cannot read candidates or write a period.
2. The setup view identifies the profile's authoritative main currency and lists only its currently selectable categories/subcategories. Deleted items and children of deleted categories are unavailable. Loading failure is not represented as an empty selection list.
3. A valid draft can set dates, select/deselect categories and subcategories, enter each selected subcategory allocation in the main currency, and show derived category sums. A zero allocation remains distinguishable from an omitted item.
4. Server-side validation follows the user's resolved date, allocation, precision, and empty-selection rules. Invalid or cross-profile selections produce no saved period or partial selection/allocation rows.
5. Successful save atomically persists one period and its independent category/subcategory/allocation rows, scoped to the profile. Expected Income equals the selected Income subcategory allocations. No duplicate expected-income or category-total field is stored.
6. Only one first period can be created for a profile, including concurrent attempts. A losing attempt receives a safe recoverable result and cannot leave partial rows.
7. New selections cannot include items whose tracking stopped before the write is accepted. Access and lifecycle state are rechecked at the write boundary; a stale client page cannot authorize a write.
8. Successful state is visible after a fresh read. Known failures are not reported as saved; uncertain outcomes retain the user's draft and offer an explicit, non-automatic recovery path.
9. The first period can be opened from the profile workflow after save and is ready for later UC-008 review and UC-009/011 activity. No financial activity is fabricated by period creation.
10. Focused domain/application, LocalDB/API, component, and browser tests demonstrate behavior in the phase where it is introduced. Relevant existing profile, access, authentication, and navigation regressions remain green.

## Affected layers and implementation order

- **Domain:** Introduce only the period identity, dates, selected categories/subcategories, and allocation rules needed by UC-006. Keep date, item-selection, allocation, and derived category-sum behavior independent of EF, HTTP, access roles, and UI. Apply only the date/amount/empty-selection rules confirmed for this plan.
- **Application:** Add focused queries/commands and ports for loading first-period choices and creating the first period. Resolve the acting account from the existing session boundary, authorize current owner/editor access, validate the request, recheck item lifecycle/profile relationships, and coordinate one atomic save. Treat the one-first-period invariant as a write-time invariant, not a page-load check alone.
- **Infrastructure:** Add EF persistence, mappings, indexes/constraints, and incremental migration for `BudgetPeriod`, `PeriodCategory`, and `PeriodSubcategory` only as needed. Scope joins by profile, preserve independent per-period allocations, enforce relationships, and make first-period creation atomic under concurrent requests. Use isolated test-owned LocalDB databases; do not apply migrations to ordinary development data during planning.
- **Contracts and API:** Add browser-safe DTOs and finance Minimal APIs for candidate loading and creation, dispatched through the existing Mediator and BFF session. Mutations use existing antiforgery protection; reads use no-store responses. Return safe validation, access, conflict, and persistence outcomes without exposing other profiles' data.
- **Client:** Add a first-period entry point from the profile landing page, a protected form, and saved-period navigation. Provide real loading, empty, validation, saving, failure, and uncertain-outcome states. Key items by stable IDs; show allocations and category sums in the profile's main currency.
- **Tests and composition:** Reuse xUnit, bUnit, isolated SQL Server LocalDB, and Playwright. Add only necessary registrations. New production classes/methods need repository-unique purpose comments using `CLASS-###` / `METHOD-###`, except files under Infrastructure Persistence, which are exempt under `AGENTS.md`.

Within each phase, write a focused failing test for the next behavior, confirm the intended red result, implement the minimum across all required layers, rerun to green, and refactor while green. Do not build all domain code before the delivery path or postpone meaningful tests to the final phase. Each phase is separately reviewed and approved before its implementation begins.

## Testable implementation phases

### Phase 1 — Review first-period setup choices

- Add an access-scoped query and browser-safe response for the profile's main currency and currently selectable category/subcategory identities and names. Respect stopped tracking at both the item and parent-category levels.
- Add a protected, useful first-period setup destination reachable from the profile page. Show the available choices and main currency with honest loading, unavailable, and retryable failure states. Keep it read-only until the create command exists; do not show an inert Save control.
- Do not add period tables or migration in this read phase. A profile with no selectable items must not be confused with a failed load; the copy for an empty result follows the resolved empty-selection decision.

**Red/green evidence:** First show the missing setup destination/query behavior. Cover owner/editor/viewer reads, no-access and removed-grant cases, session loss, profile scoping, filtering of stopped categories/subcategories, main-currency projection, and no-store behavior. Component tests cover loading versus empty versus failure and stale route responses. Use a focused browser deep-link/navigation check.

**Exit criterion:** An authorized writer can inspect authoritative first-period choices and the main currency; a viewer cannot proceed to a write; inaccessible profiles reveal no choices. No schema migration is required.

### Phase 2 — Create and save a valid first period end to end

**Gate:** Resolve date validation/boundaries, allocation sign and precision, empty-selection behavior, and the concurrent first-period result before presenting this phase for approval.

- Add the minimal domain period behavior, application command/validation, atomic persistence port, EF mappings and migration, API contract/POST endpoint, and client form/save flow.
- Support editing the draft dates, selections, and allocations before saving; derive category sums from selected subcategory allocations. Persist only selected categories and subcategories and their allocations, with all amounts in the profile main currency.
- Recheck owner/editor access, no-existing-period status, current category/subcategory lifecycle, and same-profile relationships at write time. Ensure concurrent submissions cannot create duplicate first periods or partial associations.
- On successful save, read back the saved period and make it available from the profile. Keep the form state truthful when validation, authorization, conflict, or persistence prevents creation.

**Red/green evidence:** Demonstrate the absent save workflow first. Domain/application tests cover category sums, expected Income, selected zero versus omission, date and allocation boundaries, empty choices, and invalid references under the resolved rules. LocalDB/API tests cover atomic persistence/rollback, current writer authorization, viewer denial, profile isolation, item stopped before save, migrations preserving existing account/profile data, and simultaneous first-period attempts. Component/browser tests cover selection editing, calculated sums, field feedback, successful save/open, and server rejection.

**Exit criterion:** An authorized writer can create one complete first period through the client; its independent selections/allocations survive a fresh read, while invalid or concurrent attempts cannot create partial or duplicate data.

### Phase 3 — Complete recovery and acceptance through the entry point

- Close any remaining user-visible failure paths across candidate loading, form submission, profile navigation, session/access loss, stale item state, and uncertain POST outcomes. Preserve the draft and offer explicit recovery; never automatically repeat a potentially committed creation.
- Verify the full UC-006 flow from profile entry through saved period availability, including category sums and Income expected allocation. Keep later-period copying and period editing out of scope.
- Add only acceptance cases still missing from Phases 1–2 and record exact test/build/migration evidence in a phase verification document. Run relevant existing account/profile/navigation regressions.

**Red/green evidence:** Use controlled API/network failures, lost responses, rejected antiforgery, expired sessions, revoked access, and a second concurrent create to demonstrate failures before corrections. Verify that a reload/list check can identify an already-created period before a user explicitly retries. Run the applicable serial browser workflow and tests/build checks agreed for this phase; record environment-limited checks without claiming them as passed.

**Exit criterion:** The complete first-period flow is demonstrated locally through the browser, with saved data and recoverable failures behaving truthfully, and each acceptance criterion has observed evidence.

## Explicit exclusions and dependencies

- UC-007 later-period creation/copying, predecessor choice for overlapping/equal dates, and zero starting spending.
- UC-008 period review/correction and protection against orphaning transaction/correction references when selections change.
- UC-009/011 transactions, imports, payment methods, split conservation, balances, reports, and activity totals.
- Category/subcategory maintenance, sharing/grant management, profile currency editing, additional currency valuations, conversion, and restoration.
- General overlapping-period restrictions or arbitrary date tie-breaks.
- Production deployment, Azure database provisioning, and deployed-origin verification.

## Approval

This document is a proposed implementation plan only. Resolve the gated product decisions and review/approve a concrete Phase 1 plan before any implementation. Approval of this overall plan alone does not authorize implementation under the repository's `AGENTS.md` process.

Planning verification: **No application code, tests, builds, migrations, or schema changes were run or made while drafting this plan.**

