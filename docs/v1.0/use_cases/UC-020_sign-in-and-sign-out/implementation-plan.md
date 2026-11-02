# UC-020 — Sign in and sign out — Implementation plan

Status: **Implementation in progress; Phase 1 completed on 30 October 2026.** The product decisions below were confirmed by the user on 30 October 2026. Later phases remain unstarted.

## Scope and behavior

UC-020 lets a registered account holder sign in to the Blazor application and sign out from the current browser session. Sign-in uses the account's verified email and password through the existing ASP.NET Core Identity account store and the selected OpenIddict authorization-code flow with PKCE. Email verification remains required for application access. Password reset, MFA, email changes, account profile editing, and account deletion are outside this use case.

The Blazor WebAssembly app uses a Backend-for-Frontend (BFF). It never receives or stores JWT access tokens or refresh tokens. The API/BFF completes the OpenIddict authorization-code flow with PKCE and keeps the resulting token set server-side. The browser receives only an opaque session identifier in an HttpOnly/Secure cookie. Protected API requests use that cookie to reach the BFF; the server resolves the session and uses its server-held token set where a JWT-authenticated resource call is required.

Any unauthenticated visit to a protected page redirects to `/login` with a validated local return path. After successful sign-in, return to that page. When the user visits `/login` without a return path, send them to `/budget-profiles`. UC-020 configures this destination but does not implement the budget-profile index page; that page belongs to a separate use case. Redirect URIs must match configured client URIs, and arbitrary return URLs are rejected.

Enable refresh tokens and persist the server-side OpenIddict token/authorization records in the identity database, protected at rest. Rotate refresh tokens after use, enforce a bounded server-side lifetime selected and documented during implementation, and revoke the current grant/token chain on sign-out. The browser session cookie contains only an opaque session identifier, never an access or refresh token. Keep the session cookie non-persistent, with no “remember me” option in UC-020; if a later use case adds “remember me,” it must specify a bounded persistent session lifetime and corresponding server-side token expiry. Account deletion remains responsible for invalidating all of that account's sessions and tokens.

Use exact-origin credentialed CORS for browser-to-BFF requests and antiforgery protection for state-changing cookie-authenticated requests. Do not use wildcard credentialed CORS. The BFF uses 15-minute JWT access tokens server-side for protected resource/API calls and refreshes them using its server-held refresh token; no JWT is sent to browser code or in browser API requests.

All sign-in failures use the same generic user-facing message, including unknown email, incorrect password, unverified email, locked-out account, and throttled attempts. Apply bounded Identity failed-login lockout and independent server-side sign-in rate limits. Select and document safe finite thresholds and lockout duration during implementation; do not expose account-specific lockout or verification details in the response. Do not log passwords, access/refresh tokens, authorization codes, or sensitive query values.

## Acceptance criteria

- A confirmed account with valid credentials can complete authorization-code sign-in with PKCE from the Blazor client and reaches the page that initiated authorization.
- Any unauthenticated visit to a protected page redirects to `/login`; after sign-in the user returns to the originally requested local page. A direct visit to `/login` without a return path redirects to `/budget-profiles`. UC-020 does not implement that destination page.
- Unverified accounts cannot receive usable application access. Unknown email, incorrect password, unverified email, lockout, and throttled attempts produce the same generic sign-in failure message.
- The Blazor client is configured as a public client, sends no client secret, uses exact registered redirect/logout URIs, validates authorization state/nonce and PKCE, and rejects arbitrary return URLs. An ID token alone cannot authorize a finance API request.
- Access tokens expire after 15 minutes and are retained server-side by the BFF. They are never included in browser responses, cookies, or client-side storage.
- Refresh tokens and their OpenIddict token/authorization records persist in the identity database, protected at rest. The BFF rotates refresh tokens, enforces a documented bounded lifetime, and revokes the token chain on sign-out.
- The browser receives only an opaque, non-persistent session identifier in an HttpOnly/Secure cookie. It sends this cookie to the BFF; no access or refresh token is exposed to browser code.
- Cookie-authenticated state-changing requests use antiforgery protection. Credentialed CORS allows only the configured client origin; wildcard origins are not allowed.
- The session cookie is scoped to the BFF host and has appropriate SameSite settings for the deployed origins. The BFF session store maps the opaque identifier to the authenticated account and server-held token set.
- BFF-to-resource API calls validate issuer, audience, signature, expiry, client/scope, active subject, and current token/authorization state. Revoked current-session tokens stop working on subsequent requests.
- Signing out revokes the current session's applicable grant/token chain, deletes the server-side session and clears the session cookie, preventing that session from obtaining further access. A separate session on another device remains signed in.
- Bounded failed-login lockout and independent server-side rate limits are configured. Their selected finite values and enforcement behavior are documented with the implementation; throttled attempts use the same generic sign-in failure message and do not disclose account existence.
- Authentication state, credentials, authorization codes, access tokens, refresh tokens, and session identifiers are not written to logs or API error details. JWT access and refresh tokens are stored only in the protected server-side token store; the browser holds only the opaque session cookie.
- Sign-in, callback, expiry, failed sign-in, throttling, sign-out, and revocation failures do not falsely report a successful authenticated state. Recoverable client errors leave the user with a clear next action without revealing account-specific failure details.

## Affected layers

- **Domain:** No new financial domain concept or rule is required.
- **Application:** Add only the identity/session boundary needed for current-account sign-in policy and account-state checks; keep ASP.NET Identity, OpenIddict, and HTTP types outside the inner layers.
- **Infrastructure:** Configure Identity/OpenIddict integration, authorization and refresh-token persistence, protected server-side token/session storage, signing/encryption credentials, token/authorization-entry validation, active-account checks, bounded lockout, and sign-in rate limiting. Persist required keys appropriately for the deployment environment.
- **API/BFF:** Enable the OpenIddict authorization, token, logout, and discovery endpoints; complete OIDC callbacks on the server; create/revoke BFF sessions; attach server-held access JWTs to resource requests; apply exact client redirect/logout registrations, HTTPS, exact-origin credentialed CORS, and antiforgery protection. Do not send token values to the browser.
- **Blazor client:** Add `/login`, protected-route redirects, safe local return path, `/budget-profiles` default post-login target, session-aware API requests, and sign-out interactions. Send only the session cookie with API calls; do not handle JWTs or refresh tokens.
- **Contracts/composition:** Add only protocol/client configuration and boundaries needed by this use case. Keep OAuth protocol responses in OpenIddict rather than wrapping them in finance API DTO conventions.

## Smaller implementation phases and red/green verification

Each phase is a vertical slice with a focused red test first, the minimum implementation to make it green, and a refactor while green. Finish and verify a phase before starting the next.

1. **Complete — BFF session foundation.** Configure API-hosted OpenIddict authorization-code flow with PKCE, OpenIddict EF token/authorization storage, Data Protection token formats for authorization codes and refresh tokens while retaining JWT access-token format, protected BFF session/token records, SQL Server migration, local development issuer and ephemeral signing/encryption credentials. LocalDB integration tests verify the OpenIddict/session tables, protected token persistence and session revocation; API discovery advertises code flow and S256 PKCE.
2. **Credential sign-in policy.** Add verified-email eligibility, generic failure responses, bounded Identity lockout, and independent server-side sign-in rate limiting. Choose finite lockout/rate-limit settings and refresh-token lifetime values, record them in configuration documentation, and verify invalid, unverified, locked, throttled, and valid credentials against LocalDB.
3. **Login routing and return behavior.** Add `/login` and protected-route handling. Test that anonymous protected-page visits retain only safe local return paths, successful sign-in returns to the requested page, and a direct login-page visit defaults to `/budget-profiles`. Keep the budget-profiles page itself outside UC-020.
4. **BFF session cookie and request path.** Issue only a non-persistent opaque session cookie with HttpOnly/Secure and deployment-compatible SameSite settings. Configure exact-origin credentialed CORS and antiforgery checks. Verify the browser cannot read the cookie or token set, and an authenticated browser request reaches the BFF successfully.
5. **Server-side access-token and refresh lifecycle.** Keep 15-minute JWT access tokens and refresh tokens in protected server-side storage. Attach access JWTs only to server-to-resource requests; refresh and rotate tokens server-side, enforcing bounded expiry and replay/revocation checks. Verify persistence, rotation, expiry, active-account validation, and revoked-token rejection with LocalDB integration and Playwright.
6. **Current-session sign-out.** Revoke the current grant/token chain, delete its server-side session, clear the browser session cookie, and show a signed-out state. Verify the former session cannot refresh or call protected resources while a second device/session remains active.
7. **Release-origin feasibility and regression.** Test configured client/BFF origins in supported browsers, including production cookie, credentialed CORS, and antiforgery behavior, HTTPS callbacks, and persisted signing/Data Protection keys. If the selected Azure origins do not support the required browser session cookie flow, pause deployment and resolve the hosting/domain design before release. Run UC-020 integration/browser coverage and relevant existing account regression checks.

Use the selected xUnit.net v3, bUnit, Playwright for .NET, and API/SQL Server integration-test stack. Tests use synthetic accounts and local/test keys; no real email is needed for this use case. Do not claim production-host feasibility from LocalDB tests. Verify .NET 10/OpenIddict runtime support, HTTPS callbacks, protected persistent server-side token storage, exact-origin credentialed CORS, browser session-cookie acceptance, antiforgery, and database connectivity on the selected Azure hosting combination as a release gate.

Phase 1 red/green record: the migration-table test failed against the Identity-only schema; the discovery test returned 404 with the OpenIddict server disabled; and the session-protection test found plaintext token data when protection was temporarily removed. After the new migration, server configuration, and Data Protection storage were applied, all three focused tests passed. In the full integration run, 27 tests passed and one unrelated existing SMTP transport test failed (`Smtp4dev_transport_maps_connection_failures_to_safe_delivery_errors`); existing account persistence coverage passed.

## Explicit exclusions and dependencies

- UC-001 supplies registration, verified email, and the Identity account store; sign-in does not recreate those workflows.
- Password reset and recovery, MFA, email change, profile editing, and permanent account deletion remain separate work.
- UC-020 signs out only the current browser session. The existing account-deletion requirement continues to invalidate every session and token for that account.
- Account/profile authorization still resolves current profile grants on the server; token claims do not become durable profile permissions.
- Exact lockout, rate-limit, and refresh-token lifetime values are selected and recorded during implementation as authorized by the user's 30 October decisions to use safe bounded limits and database-backed refresh tokens.
- The BFF issues only an opaque session cookie; the Blazor client never receives OAuth access or refresh tokens. Cookie behavior must be tested against actual client/BFF origins and supported browsers.
- Production hosting, ACS email provisioning, and account recovery/erasure release gates in the technical specification remain separate from this implementation slice.
