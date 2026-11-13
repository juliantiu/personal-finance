# UC-004 — Maintain categories and subcategories — Implementation plan

Status: **Phases 1–6 approved, completed, and verified by 12 November 2026. Phase 7 remains dependency-gated and not started. Decisions below remain unresolved for their affected phases.**

## Sources and current baseline

The version directories directly under `docs/` were inspected for this planning task. `v1.0` is the only version and therefore the highest numeric version. All project references below come from that version; historical snapshots were not used.

- [Proposal](../../PROPOSAL.md): profile-scoped categories and subcategories, optional comments, stable rename identity, protected system categories, deletion versus omission, and retained financial history.
- [Written UC-004](../use-cases.md#uc-004), [flow](flow.md), [sequence](sequence.md), and [states](states.md): participant reads, owner/editor writes, failure feedback, independent child deletion, and no restoration feature.
- [Logical schema](../../migrations/schema-design.md): Category, Subcategory, SavingsPlan, D12/D13, and the explicitly unresolved policies. This is a logical baseline, not evidence of implemented tables.
- [Technical specification](../../technical.specification.md): Clean/Onion boundaries, DDD, Mediator, FluentValidation, EF Core, finance Minimal APIs, MudBlazor, and xUnit/bUnit/Playwright verification.
- [UC-002 implementation plan](../UC-002_create-and-access-budget-profiles/implementation-plan.md) and [UC-019 implementation plan](../UC-019_configure-profile-currencies/implementation-plan.md): existing profile access, BFF/session boundaries, navigation, isolated LocalDB tests, and phase checkpoints.

Existing code reviewed includes `BudgetProfile` and `InitialCategory` in Domain; the accessible-profile query and EF projection; the profile-creation store; `CategoryRecord`, its EF configuration, and `AccountIdentityDbContext`; profile endpoints/contracts; the profile details page; profile integration/component tests; and the shared LocalDB test host.

The current application creates Income, Savings, and Debt with stable IDs. Category persistence currently has only ID, profile ID, name, and kind; its check constraint permits only the three system kinds, and kind is limited to seven characters. Supporting `ordinary` requires both the constraint and length to change. There is no subcategory storage, comment storage, or tracking-stopped timestamp. The profile detail contracts omit category IDs, and the client keys category rows by kind; multiple ordinary categories therefore require identity-based rendering as part of their introduction.

Budget periods, financial entries, savings/debt corrections, and reports are not implemented. Their historical guarantees cannot yet be verified through real workflows. They remain an explicit dependency gate rather than a reason to create speculative financial tables.

## Scope and intended behavior

Implement one UC-004 phase at a time through every necessary layer. Owners and current editors may maintain profile items; current viewers may review them and their comments. Sharing/grant management itself stays outside UC-004 and uses synthetic grants in tests.

- Add ordinary categories and subcategories under a category. Category kind is stable; creating an ordinary category does not create another Income, Savings, or Debt category.
- Edit names and optional comments using the existing item ID. Renaming a system category does not change its system meaning or deletion protection.
- Explicit deletion stops new tracking while retaining the item. A subcategory keeps its own deletion fact; a deleted parent additionally makes every child unavailable for new tracking without rewriting those child facts.
- Protected Income, Savings, and Debt categories cannot be deleted, including after rename. Their subcategories may be deleted.
- Retained items remain reviewable. Period omission remains separate from deletion. Restoration, reparenting, category-kind changes, and movement between profiles are not introduced.
- Failed or uncertain writes are not reported as saved. Every request resolves current server-side access and scopes the item to the requested profile.

The word “delete” describes the product operation. Internally, a timezone-aware tracking-stopped timestamp represents that operation; it does not physically remove the row. Account erasure is a separate use case.

## Decisions to resolve before affected implementation

Recommendations here are proposals for discussion, not accepted requirements or silent defaults. Phase 1 can be reviewed independently; mutation phases stay gated by their relevant decisions.

| Decision | Question and proposed treatment | Gate |
| --- | --- | --- |
| Names | **Resolved by the user for Phases 2–3:** require a nonblank name after trimming, maximum 100 characters after trimming, and allow duplicate display names identified by record ID. | Applied in Phases 2–3 |
| Comments | **Resolved by the user for Phases 2–3:** optional plain text, maximum 2,000 characters, normalize empty/whitespace-only input to absent, and preserve meaningful interior line breaks. | Applied in Phases 2–3 |
| Concurrent changes | **Resolved for Phases 2–5:** optimistic concurrency per edited category or subcategory record; stale edits/deletes conflict without changing data. Category and Phase 4 subcategory deletion require the currently displayed row version. A transaction-owned category lock serializes parent deletion with child creation/metadata updates; creation commits only before parent deletion or is rejected. | Phases 2–5 complete |
| Editing retained items | **Resolved for Phases 4–5:** owners/editors cannot change a subcategory's name/comment after its own tracking stops. They also cannot edit any child after its parent category stops tracking, even if the child remains individually active. | Phases 4–5 complete |
| Repeated deletion | **Resolved for Phases 4–5:** repeating deletion with the current row version succeeds without changing the original timestamp; a stale version conflicts without a change. Explicitly deleting a child under a deleted parent sets the child's own timestamp. The user confirmed the same current-version repeat policy for categories on 12 November 2026. | Phases 4–5 complete |
| Savings setup boundary | **Resolved for Phase 3:** create an empty SavingsPlan atomically with each Savings subcategory, with goal and reset note unset. Goal/reset-note editing and balances stay in UC-014. The migration uses EF's SQL Server default decimal mapping for the nullable goal column; UC-014 must resolve goal precision before accepting values. | Phase 3 complete |

Reparenting and restoration remain excluded, so they do not need new product rules for this plan. Goal precision, financial timestamps, correction deletion, and report policies must be resolved in their owning use cases before the historical verification phase depends on them.

## Acceptance criteria

1. A current owner, editor, or viewer can review the profile's implemented item details. Missing or inaccessible profiles/items reveal no other profile's data. Loading, unavailable, and failed-load states are distinct.
2. Owners/editors can perform approved create, rename, comment, and deletion operations. Viewers, anonymous/ineligible sessions, revoked participants, and writers targeting another profile cannot write. Client-supplied role, kind, or profile relationships cannot bypass these boundaries.
3. Input follows the explicitly agreed name/comment rules. Invalid input or failed persistence leaves stored state unchanged and gives actionable feedback without claiming success.
4. Category creation supports multiple ordinary categories while preserving exactly one system category of each kind per profile. Existing profile creation/defaults and navigation continue to work.
5. Renaming retains the item ID, category kind, profile/parent relationship, and deletion facts. Once financial workflows exist, it also retains their references, allocations, and financial outcomes.
6. Every subcategory belongs to a category in the requested profile. The SavingsPlan prerequisite, if approved, commits or rolls back with its Savings subcategory. No goal, opening balance, transaction, or allocation is invented.
7. Any subcategory can stop tracking. Only ordinary categories can stop tracking. Deletion retains rows/comments and uses timezone-aware timestamps; protected category deletion is rejected on the server as well as unavailable in the UI.
8. Parent deletion makes all children unavailable for new tracking without writing their individual deletion timestamps. Independently deleted children remain independently deleted.
9. Successful changes survive a fresh read/reload. Agreed concurrency and repeat-operation policies hold against persisted state, including parent/child races. Lost responses do not cause automatic duplicate creates or false success feedback.
10. After dependent use cases exist, deleted items are excluded from new period selections/copies while existing selections remain correctable, and relevant reports retain balances and unpaid obligations. Period omission does not delete an item or prevent later selection of an active item.

## Affected layers and implementation order

- **Domain:** Introduce only the category/subcategory behavior required by each approved phase. Keep identity, immutable kind/relationships, and tracking rules independent of EF, HTTP, permissions, and UI. Reconcile mutable category behavior with the current `InitialCategory` creation model without unrelated profile refactoring. Do not load an entire profile's financial history to edit an item.
- **Application:** Add focused queries/commands and explicit read/write ports. Resolve the current account, enforce resource-scoped access, validate agreed input with FluentValidation, coordinate domain behavior, and commit the required records atomically. Recheck affected relationships/access at the write boundary; do not authorize solely from an earlier page load.
- **Infrastructure:** Extend EF records, mappings, constraints, and migrations only when a phase needs them. Preserve existing IDs/defaults. Implement access-scoped projections, atomic saves, and the agreed concurrency policy. Use timezone-aware storage for tracking-stopped timestamps.
- **Contracts and API:** Return browser-safe identity-based projections and permitted actions. Add finance Minimal APIs using the existing opaque BFF session, antiforgery protection for mutations, no-store responses, and safe error translation. Inaccessible item/profile results must not disclose private record details.
- **Client:** Add a category-management destination from profile details; progressively add forms/actions as their server behavior exists. Use stable IDs for rows and requests. Preserve truthful loading, validation, conflict, save-failure, and unknown-outcome states; discard stale data during profile navigation.
- **Tests and composition:** Reuse xUnit, bUnit, isolated LocalDB, and Playwright. Add only the registrations and dependencies necessary for the approved behavior. New production classes/methods require purpose comments with repository-unique IDs, except under Infrastructure Persistence as exempted by `AGENTS.md`.

Within each phase: resolve its decisions, present the concrete phase plan for approval, write a focused failing test for the next behavior, implement the minimum across its required layers, rerun to green, and refactor while green. Repeat for the next behavior in that phase. Do not build all domain code before delivery adapters or defer all testing to the final phase.

## Testable implementation phases

### Phase 1 — Review current categories through a dedicated destination

**Status:** Complete — approved, implemented, and verified 10 November 2026. See [Phase 1 verification](verification-phase-1.md).

- Add an access-scoped category query, contract, GET endpoint, and protected management page reachable from profile details. Return category IDs, names, stable kinds, and the current effective role.
- Review the three existing system categories and distinguish their meaning from display names. Keep the page read-only until mutation phases land; do not expose inert save/delete controls.
- Reuse existing category storage. Do not create empty subcategory, comment, financial, or lifecycle tables/columns solely for this read phase, and do not imply those features are complete.
- Support loading, success, generic unavailable, retryable failure, and session-loss states. Apply the existing protection against showing an old profile's result under a new route.

**Red/green evidence:** First demonstrate the absent page/navigation or GET behavior. Cover owner/editor/viewer reads; unrelated/missing profile IDs; removed grants; missing/ineligible sessions; no-store behavior; and identity-preserving projection. Component tests cover loading, error versus empty, route changes/late responses, and navigation back. Use a focused browser read/deep-link smoke check.

**Exit criterion:** A participant can open the management page and review authoritative existing categories, while an inaccessible profile yields no category data. No schema migration is required.

**Completion evidence:** Added an authenticated, access-scoped Mediator query, EF projection, browser contract, and no-store categories GET endpoint. The protected read-only category page is reachable from profile details, keys rows by category ID, and displays saved names separately from stable system meanings. Loading, empty, unavailable, retryable failure, session loss, and profile-route changes are covered. The API test first returned **404** instead of **200**; component/navigation tests first failed because the destination/link was absent. Final verification passed **18 Domain, 46 Application, 76 Client, 100 Integration/LocalDB, and 4 Chrome browser tests**. Solution build and Release client publish succeeded; `git diff --check` passed. The build reported 13 pre-existing integration-test warnings. No database schema, domain behavior, dependencies, or mutation functionality was introduced.

### Phase 2 — Create ordinary categories and edit category metadata

**Status:** Complete — approved, implemented, and verified 10 November 2026. See [Phase 2 verification](verification-phase-2.md).

- Add category creation plus rename/comment updates across Domain, Application, Infrastructure, contracts/API, and the client. Permit owner/editor writes only. Allow system category name/comment edits while keeping their kinds fixed.
- Extend category persistence for ordinary kind, comments, and the agreed version mechanism. Widen the kind column to accommodate `ordinary`, update its check constraint, and retain filtered uniqueness for system kinds. Do not impose an unapproved name uniqueness constraint.
- Return stable category IDs through affected detail projections and key the existing profile overview by ID so multiple ordinary categories render correctly. Preserve current defaults/profile creation.
- Provide field errors, in-flight submission guards, read-after-save refresh, explicit failure recovery, and an unknown-create-outcome path that requires review before retrying.

**Red/green evidence:** Exercise the agreed input boundaries; multiple ordinary categories; system and ordinary rename/comment changes; immutable ID/kind/profile; owner/editor success; viewer denial; cross-profile ID substitution; forged fields; antiforgery failure; and the agreed stale-edit conflict. Use LocalDB rollback and migration-upgrade tests preserving existing account/session/profile/default data. Verify browser create/edit/reload and component error/draft behavior, including a lost create response without automatic replay.

**Completion evidence:** Added the category Domain behavior, validated Mediator commands, access-scoped EF writes, row-version concurrency, category comments, ordinary-category persistence, no-store/antiforgery-protected API writes, and editable category management UI. The profile overview now renders by stable category ID. Existing system category IDs and kinds remain fixed; duplicate names are allowed. See [Phase 2 verification](verification-phase-2.md) for red/green and regression results.

**Exit criterion:** An authorized writer can create and edit categories through the UI, and a viewer can read saved metadata. Failed, unauthorized, invalid, and stale writes leave accepted data intact under the agreed policy.

### Phase 3 — Create and edit subcategories under their category

**Status:** Complete — approved, implemented, and verified 11 November 2026. See [Phase 3 verification](verification-phase-3.md).

- Introduce subcategory identity, fixed parent relationship, name/comment behavior, persistence/migration, queries/commands, endpoints, and nested management UI. Extend the review destination to show children and their comments for all participants.
- Support children under ordinary, Income, Savings, and Debt categories. Resolve parent/profile membership on the server, including when IDs from two accessible profiles are mixed.
- If the proposed Savings prerequisite is approved, add only its minimal settings record with optional values unset and save it atomically with the child. Keep savings forms, goals, reset notes, balances, and corrections in UC-014.
- Carry forward the agreed validation, concurrent-edit, draft-preservation, and uncertain-create behavior from Phase 2 without introducing a generic framework for future use cases.

**Red/green evidence:** Cover creation under each category kind; agreed blank/length/duplicate/comment cases; stable identity/parent after edits; viewer/revoked access denial; foreign-profile parent/child IDs; nonexistent parent; and stale updates. Use real LocalDB rollback injection for child/settings creation if applicable, migration preservation checks, component nested-list/form tests, and a browser add/edit/reload workflow.

**Exit criterion:** Owners/editors can create and edit subcategories and viewers can read them. Parent/profile isolation and required Savings setup hold atomically. No financial activity is created.

**Completion evidence:** Added stable subcategory identity and metadata rules, profile-scoped reads and writes, per-subcategory row-version concurrency, and nested review/create/edit controls. Children can be created under ordinary and all three system category kinds; duplicate names remain distinct by ID. Savings child creation and its empty SavingsPlan commit together. Integration coverage verifies access, validation, profile isolation, antiforgery, stale updates, migration preservation, and rollback when SavingsPlan insertion fails. All tests and the browser add/edit/reload workflow passed; see [Phase 3 verification](verification-phase-3.md).

### Phase 4 — Stop tracking an individual subcategory

**Status:** Complete — approved, implemented, and verified 12 November 2026. See [Phase 4 verification](verification-phase-4.md).

- Add the child's independent timezone-aware tracking-stopped fact and its Domain operation, persistence, command/API, and UI action. Support deletion beneath each category kind.
- Retain the child, its parent relationship, name, comments, and any required settings. Show its deleted status when reviewing items; do not make it disappear through a global read filter.
- Enforce the agreed repeated-deletion and post-deletion metadata rules. Deletion must not imply restoration or clear prior deletion facts. Reject stale operations according to the agreed version policy.
- Introduce only the active-versus-individually-deleted eligibility behavior needed here. Its later consumption by budget selection is tracked in Phase 7.

**Completion evidence:** Added independent `DateTimeOffset` tracking-stop state, an optimistic-concurrency delete command/API, and a writer-only page action. Deleted subcategories remain in reads with their name, comment, parent, and SavingsPlan; metadata updates are rejected. A current-version repeat is idempotent and preserves the original timestamp; stale deletes conflict. Focused LocalDB tests cover all four parent kinds, access, antiforgery, profile scoping, migration preservation, retained data, and repeated/stale requests. Component and Chrome tests verify status display and reload. See [Phase 4 verification](verification-phase-4.md) for red/green and regression results.

**Red/green evidence:** Cover each parent kind; retained identity/data; independent timestamp; no parent/other-child changes; repeat deletion and stale version; authorized versus rejected writes; antiforgery; and persistence failure. Verify the visible retained status, metadata policy, and reload behavior through components and a focused browser workflow.

**Exit criterion:** A subcategory can be explicitly deleted through the UI while remaining reviewable. Its independent deletion fact is persisted and other items remain unchanged. New-period exclusion is not yet claimed as verified.

### Phase 5 — Stop tracking an ordinary category and block its children

**Status:** Complete — approved, implemented, and verified 12 November 2026. See [Phase 5 verification](verification-phase-5.md).

- Add category tracking-stopped persistence and behavior. Enforce protected system-category deletion in Domain and at the database boundary, based on kind rather than name.
- Deleting an ordinary parent changes the parent's deletion fact only. Compute child availability from both parent and child facts; distinguish individually deleted children from children blocked only by the parent.
- Reject new child creation under a deleted parent. Apply the agreed policy for metadata changes and independent child deletion under that parent. A concurrent parent deletion/child creation must produce a coherent committed result, using the agreed transaction/concurrency boundary.
- Update the management UI to explain retained status and protection without offering restoration or changing child deletion facts.

**Completion evidence:** Added a timezone-aware category tracking-stop fact and migration, ordinary-only Domain behavior, database protection for system kinds, access-scoped versioned DELETE API, and retained-status/effective-child-availability projections. Parent retirement and child create/metadata writes share a transaction-owned SQL application lock; child creation is rejected after parent retirement. The management page shows retained category status, blocks child edits and creation below it, and still lets writers stop an active child's tracking. LocalDB tests cover system categories before/after rename, owners/editors/viewers, antiforgery, stale and repeated deletes, child timestamp retention, blocked writes, migration preservation, database protection, and coordinated parent-delete/child-create requests. The browser flow verifies parent and child status across reload. See [Phase 5 verification](verification-phase-5.md) for red/green and regression results.

**Red/green evidence:** Attempt deletion of Income/Savings/Debt before and after rename; delete an ordinary parent with zero/multiple children; include a child already independently deleted and assert its timestamp is unchanged. Verify an individually active child's timestamp stays absent while its effective availability becomes false. Exercise parent-delete/child-create races against LocalDB with coordinated requests, unauthorized writes, rollback, and post-deletion policies. Check protected and retained states through components/browser reload.

**Exit criterion:** Protected categories cannot be deleted; ordinary parent deletion blocks new tracking for the whole subtree while preserving every child's independent state. No rows or historical references are physically removed.

### Phase 6 — Complete local category-management acceptance and evidence

**Status:** Complete — approved, implemented, and verified 12 November 2026. See [Phase 6 verification](verification-phase-6.md).

- Join the delivered flows with synthetic owner/editor/viewer accounts: review, create, rename, comment, individual deletion, and parent deletion. Include direct links, route switching, reload, and navigation back to the profile.
- Verify current access after a grant is removed or downgraded, session loss, antiforgery rejection, validation/conflict recovery, dependency/network failure, and a response lost after a committed mutation. Preserve permitted drafts without presenting uncertain results as saved or automatically replaying creates.
- Check keyboard operation and a narrow viewport for the actual forms/actions. Retest multiple ordinary categories on the profile overview and existing profile creation, currency review, and relevant authentication flows.
- Run the Domain/Application/Client/Integration suites, applicable serial Playwright workflows, solution build, Release client publish, and `git diff --check`. Use the working browser configuration; record actual results and unavailable engines without claiming unrun coverage.
- Record exact commands, intended red failures, green results, migration preservation/rollback evidence, environment limits, and remaining dependency gates in a UC-004 verification document. Earlier phases must already have their own focused verification; this phase does not replace it.

**Red/green evidence:** For any newly discovered behavior gap, first reproduce the missing behavior with a focused test, then fix it within the approved scope. Existing passing behavior needs regression execution rather than artificial failing tests.

**Exit criterion:** Category-management functionality has repeatable local acceptance evidence. Mark only the completed phases complete; keep UC-004's historical integration obligations explicitly pending until Phase 7 is verified.

**Completion evidence:** Added browser acceptance assertions for keyboard category editing at a 390 px viewport, multiple same-name ordinary categories on the profile overview, and confirmed subcategory persistence before navigation. The three category-management Chrome workflows passed. Profile creation/currency, profile-list, and account browser regressions passed serially. Domain, Application, Client, and full LocalDB Integration suites passed; solution build and Release client publish succeeded; `git diff --check` passed. See [Phase 6 verification](verification-phase-6.md) for exact commands, coverage, and environment limits. No production behavior gap or schema change was introduced; Phase 7 remains dependency-gated.

### Phase 7 — Verify retained history through dependent financial workflows

**Status:** Dependency-gated; not started. Do not implement prerequisite use cases under this phase's approval.

**Dependencies:** UC-006/007 period selection/copy, UC-008 correction, UC-009/011 approved financial activity, applicable UC-013/014/015 obligations/corrections, and UC-016/018 reports. Resolve their own open decisions and obtain their own implementation approvals first.

- When each dependent workflow is implemented, ensure it consumes the stable item identities and effective tracking availability defined here. Reassess this phase against the actual contracts before presenting its concrete implementation plan.
- Verify active items can be omitted and later selected again; deleted items and children of deleted parents cannot be newly selected or copied; existing period selections remain available for authorized historical correction.
- Rename items referenced across periods and confirm references, allocations, classifications, and derived balances remain attached to the same IDs. No new historical-name snapshot requirement is introduced.
- Verify deletion/omission leaves historical activity, savings/debt balances, and unpaid obligations available to their relevant reports. Include ordinary and system-category children and independently deleted children.
- Add integration fixes only where those workflows require UC-004 behavior, with a fresh scope review if their implementation would expand this use case. Do not add placeholder financial schemas or claim synthetic item-only checks prove report correctness.

**Red/green evidence:** Use real implemented period/transaction/correction/report entry points and synthetic LocalDB fixtures to reproduce any missing exclusion, correction, or aggregation behavior; implement the scoped fix and rerun to green. Add a small browser history workflow and relevant financial regressions when those routes exist. If protections were already delivered in their owning use cases, run them and link their evidence rather than manufacturing a red failure.

**Exit criterion:** All applicable stable-identity, new-tracking exclusion, historical correction, and retained-obligation scenarios pass against actual financial workflows. Until then, the management portion may be complete while UC-004 overall remains partially verified.

## Verification and data rules

- A red result must fail because the target behavior is absent or incorrect; unrelated environment/setup failures are not a red/green demonstration.
- Match tests to meaningful outcomes: Domain invariants, Application authorization/orchestration, SQL/HTTP/atomicity/concurrency boundaries, bUnit states, and selected browser workflows. Avoid empty coverage for unused layers.
- Use test-owned isolated LocalDB databases, synthetic accounts/grants, and captured/fake email only. Run LocalDB browser hosts serially where required by the existing test setup.
- Verify incremental migrations against existing synthetic account/session/profile/default data, not just an empty database. Apply no migration to the ordinary development database without a separately agreed implementation step.
- Report tests actually executed, results, warnings, and blocked/unrun checks. A successful build is not proof of deletion, authorization, concurrency, or financial-history behavior.

## Exclusions

- Sharing or permission-management UI; profile rename/deletion, ownership transfer, and permanent account erasure.
- Restoration, hard deletion of retained items, category-kind changes, reparenting, and cross-profile moves.
- New budget periods, allocation entry, financial transactions/imports, savings/debt management, IOU operations, or report implementations. Phase 7 verifies their integration after separate delivery.
- Automatic selection of newly created items into existing periods, retroactive changes to saved period selections, financial recomputation without its owning use case, and generic audit/event-history infrastructure.
- Unrelated currency settings changes, new frontend frameworks, new persistence technologies, Azure provisioning, and deployed-origin verification.

## Approval and phase checkpoints

The user requested this implementation plan and its testable phases on 10 November 2026. That authorizes this documentation change only. It does not approve application code, tests, migrations, scaffolding, dependencies, or refactors.

On 10 November 2026, the concrete Phase 1 plan was presented in chat, covering the query/reader, GET contract, protected review destination and navigation, acceptance criteria, red/green verification, and evidence updates. The user explicitly approved it: “Approved. Go ahead and implement this plan”. That approval was used for Phase 1 only. Its verification is recorded in [verification-phase-1.md](verification-phase-1.md); the open decisions for later phases remain unresolved.

Before each phase begins, resolve its open questions, present the concrete implementation scope/acceptance criteria/layer changes/order/red-green approach, and ask for explicit approval as required by `AGENTS.md` and the `uc-phase-implementation` skill. Proceed within the approved scope without repeatedly asking for the same approval. Pause affected work if new ambiguity or a scope change appears.

Complete and verify one phase, record its evidence and status, and pause for the user's questions/readiness before moving on. Readiness to continue does not replace approval of a concrete plan when that plan has not yet been presented and approved.

Original planning-only verification, before Phase 1 approval: **Only the documentation draft was created during that planning task. No application code, tests, migrations, dependencies, builds, or test runs were changed or executed for the original planning task.**
