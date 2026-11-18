# UC-003 — Share a profile and change access — Implementation Plan

**Status:** Approved; Phase 1 complete. Phases 2 and 3 have not started.

**Confirmed behavior:** If a removal is requested for an account with no active grant, tell the owner that nothing was removed because the account has no active access to the profile. Do not imply a grant was removed.

## Scope and behavior

Implement profile sharing for existing registered accounts. The original profile owner can review current participants, grant editor or view-only access, change a participant's role, and remove a grant. The recipient is identified by the account's unique normalized email address. Only accounts with a verified email are eligible; a missing account and an unverified account both produce a generic “No eligible account found” result and no grant. Access changes never transfer ownership.

Owners retain full profile access. Editors can read and change profile content but cannot manage sharing. Viewers can read profile content but cannot change it or manage sharing. Users without a current grant cannot access the profile. The server checks current persisted access for each request, so a removed grant stops working on the next request even if the recipient's sign-in session remains active.

Recipient invitations, acceptance links, access requests, ownership transfer, account deletion, and profile deletion are outside this use case. No new role or profile-content workflow is introduced. The existing `ProfileAccess` relation already stores editor and view-only grants separately from profile ownership; no schema migration is expected unless implementation discovery shows a requirement the current model cannot represent.

## Acceptance criteria

- Only the original owner can list participants, grant access, change a role, or remove a grant. Editors, viewers, unrelated accounts, and unauthenticated requests cannot use sharing operations.
- The owner can identify a recipient by their unique normalized email address. Only an existing account with verified email is eligible. Missing and unverified accounts receive the same generic result, and neither creates or changes a grant.
- A successful grant gives the recipient exactly one of the supported roles: editor or view-only. Reassigning a role updates the existing grant rather than creating another participant record.
- The original owner's ownership and access cannot be changed through participant management. No owner role is written to `ProfileAccess`.
- A successful removal deletes the recipient's grant. Their next profile read or write request is denied; an existing authenticated session does not preserve profile permission.
- If the owner removes access for an account with no active grant, report that nothing was removed because the account has no active access to the profile; do not report a successful removal.
- Owners and editors can use existing profile write operations; viewers can read but cannot use existing profile write operations. No participant can manage sharing unless they are the original owner.
- The owner-facing sharing screen shows current participants and their roles, supports assigning or changing a role by email, supports removing access, and reports success or failure without claiming an unsuccessful change was saved.
- State-changing browser requests use the existing BFF cookie and antiforgery protections. Share-management responses follow existing no-store behavior for profile data.

## Affected layers

- **Domain:** No new financial domain concept is needed. Keep ownership distinct from participant grants; do not model owner as a grant role.
- **Application:** Add focused queries/commands for listing participants, resolving an eligible recipient, granting or changing a role, and removing a grant. Keep account identity and persistence details behind ports.
- **Infrastructure:** Implement participant reads and grant writes against the existing profile and Identity records. Resolve verified accounts using the Identity normalized email and check persisted profile ownership/current grants. Use the existing `ProfileAccess` key and role constraint unless implementation demonstrates a necessary schema change.
- **API:** Add owner-authorized sharing endpoints and request/response contracts. Validate the authenticated account and antiforgery token for mutations, avoid exposing account existence through different responses for missing versus unverified email, and return success only after persistence succeeds.
- **Client:** Add an owner-only profile sharing screen or section with the participant list, role selection, email entry, removal actions, and clear save/error states. The UI is a convenience boundary; the API remains authoritative.
- **Tests:** Use application tests for command/query orchestration, SQL Server LocalDB integration tests for ownership, verified-recipient resolution, grant persistence, authorization, and immediate removal, bUnit for sharing-screen states, and Playwright acceptance coverage for the end-to-end owner workflow. Use synthetic accounts and profile data.

## Testable implementation phases

Each phase uses a red/green cycle for each behavior: add a focused test, run it and confirm it fails because the behavior is missing, implement the smallest vertical slice, then rerun the test and relevant existing profile regressions. Refactor only while the phase tests remain green. Finish and verify a phase before starting the next.

1. **Owner-only sharing view and participant list — Complete.** The owner-only query and API response return the saved participants and roles, and the client sharing view displays them. Integration coverage verifies owner listing, editor/viewer/unrelated-account denial, unauthenticated denial, and no-store responses. bUnit covers loading, empty, populated, and non-owner states; browser coverage verifies the owner sees the sharing view and a viewer does not. Existing grants remain unchanged in this phase.
2. **Grant and change participant roles — Not started.** Add the application command, persistence adapter, API mutation, and client form to grant editor/view-only access to a verified account identified by normalized email or update that participant's existing role. Tests first cover a new grant, role change without a duplicate row, missing/unverified recipient with the same generic result and no write, non-owner denial, unsupported role rejection, persistence failure, and antiforgery rejection. Add focused client tests for submission and success/error feedback.
3. **Remove access and verify the full owner workflow — Not started.** Add grant removal through the application, API, and client. Tests first cover successful removal, repeated/unknown participant requests returning a “nothing was removed because this account has no active access” result without changing data, prevention of altering the original owner's ownership, and the removed user's next profile request being denied while a still-granted participant retains the correct read/write behavior. Add Playwright coverage for listing participants, granting, changing, and removing access, then run the relevant account and profile regression suites.

## Completion criteria

UC-003 is complete when all three phases and their corresponding tests pass, owner/editor/viewer permissions are enforced by the server for the implemented profile operations, removal takes effect on the next request, and the end-to-end owner sharing workflow is verified. Record the red and green results and any checks that could not run before marking any phase complete. Stop after UC-003; completion does not authorize beginning another use case.

## Phase 1 verification record

- **Red:** the owner participant-list integration test returned HTTP 500 because EF Core could not translate ordering by a constructed `ProfileParticipant`; the bUnit loading-state test showed the whole profile as loading instead of showing the participant loading state.
- **Green:** ordering by the email column before projecting fixed the integration failure; ending profile loading and rendering before awaiting the participant request fixed the bUnit state. Both focused tests passed after their fixes.
- **Regression results:** all 88 client tests, all 52 application tests, all 46 profile integration tests, and all 3 `ProfileListBrowserTests` passed.
- **Runner note:** the standard `dotnet test` invocation reported a Microsoft.Testing.Platform host handshake failure in this environment. The built xUnit v3 in-process test executables ran the listed suites successfully. Builds succeeded outside the sandbox where MSBuild could start the WebAssembly task host.
