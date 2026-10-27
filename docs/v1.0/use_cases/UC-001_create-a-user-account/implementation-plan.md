# UC-001 — Approved implementation plan

Approved by the user on 23 October 2026 for branch `v1.0_uc-001_create-a-user-account`.

## Scope and behavior

Open registration collects email, password, and password confirmation. Email is required, syntactically valid, and unique using ASP.NET Core Identity's normalized-email comparison, backed by a database uniqueness constraint. Passwords require at least 12 characters without uppercase, lowercase, digit, or symbol requirements. Password confirmation must match and is not persisted. Identity supplies password hashing and confirmation-token protection.

Registration creates a stable account identity with an unverified email. A confirmation link expires after 24 hours; successful confirmation verifies the email without signing the user in. First and last names belong to a later account profile page and are not registration inputs. Verified email remains a prerequisite for future financial/profile access.

This slice includes registration and email confirmation, Application orchestration and validation, Identity/EF Core SQL Server adapter code, account controller endpoints, and Blazor/MudBlazor screens. Development uses a local email sink; automated tests use a fake sender and synthetic accounts. Azure Communication Services Email integration and OpenIddict sign-in are separate implementation slices. Account profile editing, recovery, account deletion, and budget profiles are outside this slice.

Provider decision amendment, 27 October 2026: Azure Communication Services Email replaces the earlier Gmail selection for future production verification and recovery email. Azure resource provisioning and provider integration are not part of the completed LocalDB phase; continue using the development email sink and test fake until that separate slice is approved.

## Acceptance criteria

- Valid registration persists exactly one account with a stable ID, a hashed password, and an initially unverified email, and makes a confirmation link available through the local development email sink.
- Missing or malformed email, passwords shorter than 12 characters, and mismatched password confirmation produce validation feedback without creating an account. A valid 12-character password is accepted without requiring character classes.
- Repeated or concurrent registration with the same normalized email cannot create a second account. Email normalization does not introduce provider-specific dot or plus-address equivalence.
- A valid confirmation link verifies its intended account. Invalid, tampered, or expired links do not verify an account. Confirmation does not create an authenticated session or grant access to another account's data.
- The UI shows validation and recoverable failures, retains the email input, and does not report successful completion when the required operation failed. Passwords and confirmation tokens are excluded from logs and API error details.
- User amendment on 23 October 2026 initially deferred database connections, migration application, and live database integration. On 26 October 2026, the user superseded that deferral for local development: integrate the UC-001 Identity store with localhost SQL Server LocalDB, apply migrations, and verify persistence and uniqueness locally. Azure SQL integration remains deferred. Automated email tests must never send messages to real recipients.

Confirmed follow-up decisions: duplicate registration receives the same generic acknowledgement as a successful request. Known email-delivery failures retain the unverified account, and resend is included. The acknowledgement must not promise that an account was created or an email delivered; it is also used for unknown/already-verified resend recipients and known delivery failures. The sender adapter records delivery failures without recipient/token payloads. Unexpected errors and cancellation propagate. Replaying a still-valid confirmation link is harmless success; invalid or expired tokens must still fail, even for an already-verified account. Working OpenIddict sign-in and Azure Communication Services Email integration remain separate.

## Implementation order and learning checkpoints

1. Reconcile the current UC-001 documents and record the approved scope and acceptance criteria.
2. Implement registration, confirmation, and resend commands, validators, results, identity/email ports, and a Mediator-compatible validation pipeline. Use focused tests and fake ports to verify orchestration and failure behavior. Completed: 28 Application tests pass. Executable Mediator registration belongs to the API composition in phase 3.
3. Add Identity/EF Core SQL Server adapter code, the development email sink, controller endpoints, and composition. Keep account endpoints unavailable without database configuration. **Completed 26 October 2026 for localhost LocalDB:** configured the connection string with API user-secrets; added EF Core design-time support and Data Protection registration; scaffolded and applied `InitialIdentitySchema` to `personal-finance-db`; verified the four Identity tables and `__EFMigrationsHistory` in SSMS; verified the unique `EmailIndex` on `NormalizedEmail`; submitted a synthetic registration and confirmed its account was persisted with a password hash; repeated registration and confirmed it did not create a duplicate; confirmed the email through the API and verified `EmailConfirmed = 1`; and performed concurrent registration checks for one synthetic address. Azure database integration remains deferred. These were manual live checks; no automated test suite was run during this guided integration.
4. Build the MudBlazor registration workflow, server-error display, and recoverable-failure behavior with component tests.
5. Complete the confirmation UI and development-email workflow; verify valid/invalid/expired confirmation paths through the entry points.
6. Run the corresponding regression checks and selected real-browser checks using test fakes where appropriate. LocalDB persistence, uniqueness, registration, and confirmation were manually verified in phase 3; automated SQL Server integration tests and the full persisted browser workflow remain to be added/run as part of the relevant verification work. Report the scope of the evidence and any unavailable checks.

Each production behavior follows a genuine red/green cycle: write and run its focused test, verify failure because the behavior is absent, implement the minimum behavior, then rerun and refactor with green tests. Add only the setup needed for UC-001 and keep dependencies inward. Domain rules are introduced only where this use case warrants them; Identity types stay in Infrastructure.

After each phase, explain the changes, how the relevant layers work together, and the checks performed, then pause for the user's questions. Continue to the next phase when the user indicates they are ready. The approved implementation scope remains authorized; these pauses are learning checkpoints.
