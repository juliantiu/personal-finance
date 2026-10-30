# UC-020 — Sign in and sign out — Draft implementation plan

Status: **Planning draft.** The product decisions below were confirmed by the user on 30 October 2026. This plan is for review before application implementation.

## Scope and behavior

UC-020 lets a registered account holder sign in to the Blazor application and sign out from the current browser session. Sign-in uses the account's verified email and password through the existing ASP.NET Core Identity account store and the selected OpenIddict authorization-code flow with PKCE. Email verification remains required for application access. Password reset, MFA, email changes, account profile editing, and account deletion are outside this use case.

The Blazor application is a public OAuth client and contains no client secret. It starts authorization with PKCE and returns the user to the page that initiated sign-in after a successful callback. Redirect URIs must match configured client URIs; the client must not accept arbitrary return URLs. The API accepts only audience-appropriate access tokens, never ID tokens, and checks the active account and OpenIddict token/authorization entries on authenticated requests.

Use the selected non-persistent session baseline: 15-minute JWT access tokens held in browser memory only, no persistent browser refresh token, and a non-persistent HttpOnly/Secure login cookie on the authorization-server origin. Do not add a persistent “remember me” option. Reloads or token expiry may send the user through the authorization endpoint again. Browser sign-out ends the current authorization session, revokes its applicable grant/token chain, clears client authentication state, and clears the authorization-server cookie. It does not sign out the account's other devices or sessions. Account deletion remains responsible for invalidating all of that account's sessions and tokens.

All sign-in failures use the same generic user-facing message, including unknown email, incorrect password, unverified email, locked-out account, and throttled attempts. Apply bounded Identity failed-login lockout and independent server-side sign-in rate limits. Select and document safe finite thresholds and lockout duration during implementation; do not expose account-specific lockout or verification details in the response. Do not log passwords, tokens, authorization codes, or sensitive query values.

## Acceptance criteria

- A confirmed account with valid credentials can complete authorization-code sign-in with PKCE from the Blazor client and reaches the page that initiated authorization.
- Unverified accounts cannot receive usable application access. Unknown email, incorrect password, unverified email, lockout, and throttled attempts produce the same generic sign-in failure message.
- The Blazor client is configured as a public client, sends no client secret, uses exact registered redirect/logout URIs, validates authorization state/nonce and PKCE, and rejects arbitrary return URLs. An ID token alone cannot authorize a finance API request.
- Access tokens expire after 15 minutes, remain in memory only, and are not persisted in browser storage. The client does not persist a refresh token or offer a persistent sign-in option.
- The authorization-server login cookie is non-persistent, HttpOnly and Secure, and is scoped to the authorization server. It does not authenticate direct API calls.
- Authenticated API requests validate issuer, audience, signature, expiry, client/scope, active subject, and current token/authorization state. Revoked current-session tokens stop working on subsequent requests.
- Signing out revokes the current session's applicable grant/token chain, clears browser authentication state and the authorization-server cookie, and prevents that session from obtaining further access. A separate session on another device remains signed in.
- Bounded failed-login lockout and independent server-side rate limits are configured. Their selected finite values and enforcement behavior are documented with the implementation; throttled attempts use the same generic sign-in failure message and do not disclose account existence.
- Authentication state, credentials, authorization codes, access tokens, and refresh tokens are not written to logs, API error details, or browser persistent storage.
- Sign-in, callback, expiry, failed sign-in, throttling, sign-out, and revocation failures do not falsely report a successful authenticated state. Recoverable client errors leave the user with a clear next action without revealing account-specific failure details.

## Affected layers

- **Domain:** No new financial domain concept or rule is required.
- **Application:** Add only the identity/session boundary needed for current-account sign-in policy and account-state checks; keep ASP.NET Identity, OpenIddict, and HTTP types outside the inner layers.
- **Infrastructure:** Configure Identity/OpenIddict integration, authorization and token persistence, signing/encryption credentials, token/authorization-entry validation, active-account checks, bounded lockout, and sign-in rate limiting. Persist required keys appropriately for the deployment environment.
- **API:** Enable the OpenIddict authorization, token, logout, and discovery endpoints. Apply exact client redirect/logout registrations, HTTPS, CORS, and cookie/antiforgery protections. Do not add a competing bearer scheme that bypasses revocation checks.
- **Blazor client:** Add sign-in, authorization callback, authenticated request handling, protected-route redirect, and sign-out interactions. Keep access tokens in memory; use top-level authorization redirects and validate the initiating return location.
- **Contracts/composition:** Add only protocol/client configuration and boundaries needed by this use case. Keep OAuth protocol responses in OpenIddict rather than wrapping them in finance API DTO conventions.

## Implementation order and red/green verification

1. Add focused Application and API tests for verified-account eligibility, uniform generic failures, active-account checks, and current-session sign-out behavior. Run each test and confirm it fails because the behavior is missing before implementing it.
2. Configure Identity/OpenIddict authorization-code flow with PKCE, exact public-client registrations, token/authorization storage, issuer/audience/scope validation, active-subject checks, and current-session revocation. Use LocalDB integration tests to exercise persisted grants and immediate revocation.
3. Implement finite failed-login lockout and independent sign-in rate limiting. Select safe thresholds during this phase, record them in configuration documentation, and verify enforcement and non-enumerating responses with integration tests.
4. Add the Blazor sign-in/callback and authenticated-request behavior. Use bUnit for callback, redirect, memory-only token handling, generic error display, and sign-out state; verify actual cross-origin authorization redirects and API calls with Playwright.
5. Complete red/green end-to-end checks for sign-in, unverified and invalid credentials, token expiry/reacquisition, current-device sign-out, another-device session preservation, and rejection of revoked tokens. Run relevant account regression checks after the focused use-case checks.

Use the selected xUnit.net v3, bUnit, Playwright for .NET, and API/SQL Server integration-test stack. Tests use synthetic accounts and local/test keys; no real email is needed for this use case. Do not claim production-host feasibility from LocalDB tests. Verify .NET 10/OpenIddict runtime support, HTTPS callbacks, persistent production signing/Data Protection keys, CORS, and database connectivity on the selected Azure hosting combination as a release gate.

## Explicit exclusions and dependencies

- UC-001 supplies registration, verified email, and the Identity account store; sign-in does not recreate those workflows.
- Password reset and recovery, MFA, email change, profile editing, and permanent account deletion remain separate work.
- UC-020 signs out only the current browser session. The existing account-deletion requirement continues to invalidate every session and token for that account.
- Account/profile authorization still resolves current profile grants on the server; token claims do not become durable profile permissions.
- Exact lockout and rate-limit numbers are intentionally selected and recorded during implementation as authorized by the user's 30 October decision to use safe bounded limits.
- Production hosting, ACS email provisioning, and account recovery/erasure release gates in the technical specification remain separate from this implementation slice.
