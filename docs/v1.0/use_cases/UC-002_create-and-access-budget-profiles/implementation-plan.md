# UC-002 — Create and access budget profiles — Implementation plan

Status: **Plan approved and Phase 1 completed on 5 November 2026. Phases 2–6 have not started.** Product decisions below were confirmed by the user during planning on 5 November 2026. The user approved the presented plan and explicitly requested Phase 1 implementation; later phases retain their learning checkpoints. See [Phase 1 verification](verification.md) for observed red/green results and environment limits.

## Sources and current baseline

- `docs/v1.0/` is the latest and only version directory directly under `docs/`, checked on 5 November 2026. Resolve the latest version again before subsequent implementation work.
- [Proposal](../../PROPOSAL.md), [written UC-002 and UC-019 specifications](../use-cases.md), UC-002 [flow](flow.md), [states](states.md), and [sequence](sequence.md), and [UC-019 currency flow](../UC-019_configure-profile-currencies/flow.md).
- [Technical specification](../../technical.specification.md) and [logical schema](../../migrations/schema-design.md), particularly BudgetProfile, ProfileAccess, Currency, ProfileCurrency, Category, PaymentMethod, and Tag. Proposed schema policies remain proposed unless explicitly resolved below.
- [UC-001 plan](../UC-001_create-a-user-account/implementation-plan.md) provides the phased learning/checkpoint format. Its six phases are complete.
- [UC-020 plan](../UC-020_sign-in-and-sign-out/implementation-plan.md) records all six phases complete for the approved local Development scope. Its current completion record supersedes the older progress summary in the use-case index. Azure/deployed-origin checks, actual Safari, and the blocked Firefox run remain separate release gates.
- The merged code supplies Identity/OpenIddict, opaque BFF sessions, eligibility checks, antiforgery issuance, exact-origin credentialed CORS, protected Blazor routing, and sign-out. `/budget-profiles` is the configured post-login destination but has no page yet. There are no financial domain implementations or profile tables; the Domain test project has no runner or tests yet.

## Scope and behavior

A verified, signed-in account holder can list accessible budget profiles, create a profile, open an owned or shared profile with its current role, and switch between profiles. An account with no profiles sees an invitation to create its first profile. The server obtains the current account from the existing BFF session; a submitted owner/account ID cannot select the acting account.

Creation collects a name, an explicitly selected main currency (GBP or USD), and optional additional currency selection from the same catalog. There is no preselected main currency. The main currency is implicitly enabled and is not duplicated as an additional membership. This is the setup portion of UC-019 only.

The current account becomes the original owner, independently of sharing grants. Creation supplies exactly one Income, Savings, and Debt category, one Cash payment method, and the IOU and IOU REPAYMENT tags. Stable IDs and category kinds/tag roles identify these records; display names are not identity keys. Cash is available, with no payment-choice default introduced. No subcategories, cards, periods, or financial transactions are created.

Opening a profile shows its name, currency configuration, current role, and initial setup records. It provides a useful profile landing page without fabricated balances, reports, or controls for unimplemented workflows. The final creation flow opens the newly created profile. Returning to the list and selecting another profile changes the route and displayed profile; it does not modify profile ownership or account settings.

### User-confirmed decisions — 5 November 2026

- A profile name is required, trimmed before validation/storage, limited to 100 characters after trimming, and may duplicate another profile name. Empty or whitespace-only names are invalid. This replaces the schema's proposed optional label for this implementation.
- Profile creation is atomic. A failure rolls back the profile, additional currencies, protected categories, Cash, and tags together; no partial-creation recovery workflow is needed.
- An authenticated request for an inaccessible profile receives the same non-disclosing `404` as a nonexistent profile. The UI gives a generic unavailable message and a route back to accessible profiles.

## Acceptance criteria

1. Anonymous, expired, revoked, and ineligible account sessions cannot read or create profiles. Protected-page visits use UC-020's login/validated-return-path flow; API requests return `401` rather than an HTML login page. A temporary Identity login ticket alone is insufficient.
2. The list includes only profiles owned by the current account or granted through a current editor/view-only association. It includes each accessible profile once, identifies its effective role, and shows a genuine empty state when none exist. A failed load is not displayed as an empty list.
3. Valid creation persists the trimmed name, original owner, selected main currency, optional additional currency, three protected categories, Cash, and two default tags as one atomic operation. Owner access derives from ownership, without an owner ProfileAccess row.
4. Missing/blank/overlength names, missing or unsupported main currencies, and invalid additional memberships produce field/model feedback without persisted side effects. An additional currency cannot equal the main currency or be repeated. Duplicate profile names remain valid.
5. A successful final creation workflow opens the saved profile. A known creation failure never reports success. A lost response is treated as an uncertain result: preserve inputs, explain uncertainty, and offer a list refresh before an explicit retry; do not automatically repeat the POST.
6. Opening uses current ownership/grants on every request. Owners, editors, and viewers can read; other accounts receive the same `404` as a nonexistent ID. A grant removed after the list loaded cannot authorize opening through a stale link or a cached client role.
7. Switching profiles displays only the selected profile's data and role. Late responses for a previous route cannot overwrite the current profile, including navigation within an already-mounted component.
8. All profile writes require the existing antiforgery proof and credentialed BFF session. Missing/invalid antiforgery proof causes no mutation. Browser code receives no access or refresh tokens, and no session/token material appears in new contracts or logs.
9. Recoverable API/network failures show an explicit error and retry/back action. Form input survives recoverable creation errors. Session loss removes protected content and follows the existing login behavior. A subsequent request after access removal clears previously displayed profile content.
10. Domain, application, LocalDB integration, component, and browser checks demonstrate the relevant behavior as it is introduced. The final regression verifies existing registration, confirmation, sign-in, refresh, and sign-out behavior.

## Approved implementation across layers

The following implementation approach was approved with this plan on 5 November 2026.

- **Domain:** Introduce only the profile identity/name/currency/access concepts and initial-record construction required here. Enforce the agreed name, currency, ownership, and initial-default invariants without EF, HTTP, Identity, or UI dependencies. Define the minimal stable category kinds, payment kind, and tag roles needed for defaults; do not add editing/deletion behavior from future use cases.
- **Application:** Add Mediator queries/commands for listing accessible profiles, creating a profile, and opening a profile, with FluentValidation for request shape. Add explicit current-account, profile persistence/query, and atomic creation boundaries. Resolve ownership/current grants server-side. No generic repository framework or authorization claims containing durable profile roles.
- **Infrastructure:** Extend the existing SQL Server database and `AccountIdentityDbContext` with feature-specific EF configurations and incremental migrations, preserving its Identity/OpenIddict/BFF data. Use GUID identities consistent with existing accounts. Add only the seven logical tables needed here: BudgetProfile, ProfileAccess, Currency, ProfileCurrency, Category, PaymentMethod, and Tag. Store GBP/USD catalog metadata, separate non-owner grants, and additional-currency memberships. Map account foreign keys in Infrastructure so Domain has no Identity dependency. Enforce appropriate required fields, lengths, membership/grant keys, and filtered uniqueness for initial system kinds/roles. Commit creation within one transaction/unit of work. Use timezone-aware storage if any timestamps are needed; no speculative audit model.
- **API/BFF:** Add profile Minimal APIs dispatched through Mediator: proposed `GET /api/budget-profiles`, `POST /api/budget-profiles`, and `GET /api/budget-profiles/{profileId}`. Add a small reusable boundary that resolves the existing opaque session and active verified account; do not authenticate finance requests with the temporary login cookie or create a second sign-in implementation. Invoke co-hosted application handlers directly without a loopback JWT request. Reuse current antiforgery issuance/validation, CORS, and safe Problem Details handling. Return `201` with the saved ID/location on creation, `400` for validation, `401` for absent/invalid sessions, and the agreed `404` for missing/inaccessible profiles.
- **Contracts/client:** Add browser-safe profile DTOs and protected MudBlazor pages at proposed `/budget-profiles`, `/budget-profiles/create`, and `/budget-profiles/{profileId:guid}`. Send cookies using the existing credentialed request pattern. Provide list/empty, create, profile landing, loading/error, and switching interactions. Avoid sharing domain/EF entities or server-only validators with WebAssembly.
- **Tests/composition:** Reuse the existing xUnit, bUnit, LocalDB factory, and Playwright setup. Enable the Domain test project's xUnit runner only when the first domain behavior is added. Keep services scoped and profile endpoints unavailable when their required backend/authentication configuration is unavailable. Add the repository-required unique `CLASS-###` and `METHOD-###` purpose comments for new production classes/methods.

## Implementation order and learning checkpoints

Each numbered phase is a bounded slice of UC-002. Tests are written and run before the behavior they verify; Phase 6 consolidates evidence rather than deferring all testing. Finish a phase and its checks, explain the layer flow, then pause for the user's questions. Start the next phase only when the user indicates readiness.

1. **Completed 5 November 2026 — accessible-profile list and session boundary.**
   - Add the minimum profile/currency/grant persistence and migration required to query owned/shared profiles. Grant management remains excluded; tests use synthetic fixtures.
   - Implement current-account resolution, list query/projection, GET endpoint, DTOs, and protected `/budget-profiles` page with loading, empty, populated, and load-failure states. Initially the page is a read-only list; creation/opening navigation is added when its destination is implemented.
   - **Red/green:** First demonstrate the missing protected destination/list behavior. Add focused application/integration cases for zero profiles, owned/editor/viewer inclusion, unrelated-account exclusion, deduplication, missing/expired/ineligible sessions, and unavailable configuration. Add component coverage for distinguishing load failure from a true empty list. Run existing authentication/routing regressions relevant to the new boundary.
   - **Checkpoint outcome:** A real sign-in reaches a working authorized list without exposing another account's profiles.
   - **Completion evidence:** Added `BudgetProfiles`, `ProfileAccess`, and GBP/USD `Currencies` storage; migration `20261105164916_AddProfileList`; an opaque-session/verified-account boundary; Mediator list query; credentialed GET endpoint with `no-store`; and the protected read-only MudBlazor list. Domain construction/defaults remain Phase 2 work. Final checks: Application **33 passed**, Client **39 passed**, Integration **77 passed**, Chrome **7 passed** (**156 total**, no failures/skips). Solution build and Release client publish succeeded. The migration was applied only to isolated test databases, including an upgrade preserving existing accounts/sessions; the ordinary development database was not changed. See [verification evidence](verification.md).

2. **Not started — atomic profile creation through the UI.**
   - Add name/currency/default construction in Domain, the create command and validation, atomic persistence plus remaining default tables/constraints, POST contract/endpoint, and create form. Wire Create first/another profile from the list. On successful save, return to the list during this phase; automatic opening is completed in Phase 3.
   - Require explicit main selection and optionally enable the other supported currency. Disable submission while it is in flight; require antiforgery proof. Use the BFF account as original owner.
   - **Red/green:** Verify missing/blank/100/101-character names, trimming, allowed duplicate names, GBP/USD main choices, zero/one alternative, invalid memberships, and complete defaults. Run a real LocalDB failure-injection test proving an error during persistence leaves no profile or default rows. Verify posted owner spoofing cannot assign ownership and invalid antiforgery causes no write. Add bUnit valid-submit/field-error cases and a browser creation smoke test.
   - **Checkpoint outcome:** A signed-in user can create a complete profile and see it in their list; known failures leave no partial records.

3. **Not started — open a profile with current permissions.**
   - Implement the authorized open query, GET endpoint, landing DTO/page, and links from the list. Display saved settings/defaults and the server-resolved owner/editor/viewer role. Complete successful creation by opening the new profile.
   - Recheck access on each open request and use the agreed unavailable response. Sharing itself is not implemented; editor/viewer checks use seeded grants.
   - **Red/green:** Cover all three roles, nonexistent/unrelated IDs, no profile-detail leakage, and a grant removed between listing and opening. Component/browser checks verify deep links, post-create navigation, settings/defaults, and the generic unavailable page. Demonstrate that an account's own profile does not confer access to another profile ID.
   - **Checkpoint outcome:** A profile opens only under its current ownership/grant, and creation reaches its saved landing page.

4. **Not started — switching and navigation consistency.**
   - Add a clear Back to profiles/switch interaction. Reload profile data when route parameters change, cancel or ignore outdated loads, and clear previous profile content during transitions. Keep selection in the route without adding an account preference or persistent last-profile setting.
   - **Red/green:** Use two profiles with different names, currencies, and roles; verify list-to-profile-to-list-to-other-profile navigation. Exercise direct deep links, reload, browser back/forward, same-component route changes, delayed responses, and an access removal observed on the next request. Add focused component tests first and a browser workflow across owned and seeded shared profiles.
   - **Checkpoint outcome:** Switching cannot display one profile's content or role under another profile's URL.

5. **Not started — failure recovery across the completed flow.**
   - Complete consistent recovery for dependency/network failures, expired sessions, rejected antiforgery, stale access, and a creation request whose response is lost. Preserve creation inputs, offer explicit retry/back/list-refresh actions, and avoid success claims or automatic duplicate creation.
   - Keep basic failure handling in the earlier phases; this phase joins the recovery paths across navigation and submission rather than leaving earlier slices unprotected.
   - **Red/green:** Reproduce each missing recovery behavior using controlled component/HTTP faults and targeted integration/browser cases. Verify unknown outcomes are distinguished from confirmed rollbacks, a second click cannot submit while the first is running, and protected content is cleared after the server reports session/access loss.
   - **Checkpoint outcome:** Each recoverable failure leaves a truthful state and a usable next action.

6. **Not started — complete local acceptance and record evidence.**
   - Complete the acceptance matrix through the actual client and API using synthetic accounts and isolated LocalDB data: no profiles, create GBP/USD configurations, required defaults, reopening, multiple profiles, seeded shared roles, revoked access, session loss, and sign-out.
   - Run Domain/Application/Client/Integration tests, the applicable serial Playwright browser matrix, solution build, Release client publish, and `git diff --check`. Reuse UC-020's working browser configuration; report Firefox startup limitations and do not claim actual Safari coverage from WebKit. Do not expand into Azure provisioning or production-origin verification.
   - Run existing registration/confirmation and BFF sign-in/refresh/sign-out regressions. Add only missing acceptance cases with a genuine red/green cycle; retain the already-written per-phase tests.
   - Record exact commands, observed red/green evidence, counts, environments, migration checks, and blocked checks in a UC-002 verification document. Mark phases complete only after their required work/checks actually finish.
   - **Checkpoint outcome:** The complete UC-002 workflow is demonstrated locally, with repeatable evidence and explicit remaining deployment gates.

## Verification rules

For each behavior: write a focused test, run it and confirm failure because the behavior is absent/incorrect, implement the minimum across the necessary layers, rerun to green, and refactor within scope while green. A setup or tool failure is not the intended red result. Match test type to the behavior: domain invariants, application orchestration, SQL/HTTP/session boundaries, component states, and browser entry-point workflows. Do not add redundant tests solely to cover a layer.

Use isolated test-owned LocalDB databases and fake/captured email; never developer data or real email recipients. Verify migrations preserve existing account/authentication records. Apply migrations to the ordinary local development database only as an explicit agreed implementation step, after isolated migration checks. Record any unavailable environment checks without reporting them as passed.

Planning verification: **No builds, tests, migrations, or application implementation were run or changed when this plan was drafted.**

## Explicit exclusions and unresolved future policies

- UC-003 sharing/grant-management UI and commands; grants are read for current access and seeded only in tests here.
- Category/subcategory editing, saved cards, tag management, budget periods, financial entries, IOU links, reports, and balance calculations. Creating the required initial records does not implement those later use cases.
- Post-creation currency editing and A12/A13 history/revaluation/disable rules. UC-019 is included only to select and read initial settings; there is no edit endpoint/UI in this slice.
- Profile rename/deletion/restoration, ownership transfer, account deletion, persistent last-profile preference, and automatic POST retry/idempotency infrastructure.
- Default-tag lifecycle, category reparenting, additional currency catalogs, and unrelated monetary/date policies remain unresolved for their own use cases.
- Azure deployment, live ACS delivery, actual deployed-origin cookie feasibility, actual Safari verification, and the previously blocked Firefox execution remain existing release gates.

## Approval

The user approved this plan and authorized Phase 1 on 5 November 2026: “Approve and implement phase 1.” Implement Phase 1 only and pause at its learning checkpoint; proceeding to later phases requires the user's readiness signal.
