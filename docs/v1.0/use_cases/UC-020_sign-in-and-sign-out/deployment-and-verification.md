# UC-020 configuration and development verification

On 4 November 2026, the user approved configuration wiring and Development-only testing. Azure provisioning, deployment, actual deployed-origin feasibility, and actual Safari verification are deferred. The user subsequently chose to retain Playwright 1.56.0 and record Firefox execution as blocked on this Windows machine.

## Configure a future deployment

[authentication-settings.example.json](authentication-settings.example.json) lists the authentication settings. It is a reference file, not automatically loaded configuration. Replace its `.invalid` origins and example paths, then supply the values through backend host configuration. On Linux use absolute Linux paths. Do not deploy this example unchanged or put private certificates/passwords in the repository or client assets.

| Setting | Purpose |
| --- | --- |
| `Authentication:OpenIddict:Enabled` | Enables the BFF and authorization server. |
| `Authentication:OpenIddict:Issuer` | Public API/BFF root HTTPS URL, including the trailing `/`. Its registered callback is exactly `<issuer>api/auth/callback`. |
| `Authentication:OpenIddict:ClientOrigin` | Frontend origin used for callback return navigation; must be included in the CORS origins. If omitted, the first allowed origin is used. |
| `Authentication:OpenIddict:ClientId` | Public PKCE client identifier; defaults to `personal-finance-client`. No OAuth client secret is used. |
| `Cors:AllowedOrigins:0` | Exact frontend origin, without a trailing `/`. Additional indexed origins must each be exact HTTPS origins. |
| `Authentication:KeyPersistence:Enabled` | Set `true` for persistent credentials. Required whenever OpenIddict is enabled outside Development; also usable in Development to exercise the same wiring locally. |
| `Authentication:KeyPersistence:KeysPath` | Absolute path on persistent writable storage for the Data Protection key ring. Keep it outside web-served directories. |
| `Authentication:KeyPersistence:ApplicationName` | Stable application discriminator. Preserve it and the key ring across restarts. |
| `Authentication:Certificates:<purpose>:Path` | Absolute private PFX path, readable only by the backend identity and its operators. |
| `Authentication:Certificates:<purpose>:Password` | PFX password supplied through backend secret configuration. |

The five certificate purposes are `ServerSigning`, `ServerEncryption`, `ClientSigning`, `ClientEncryption`, and `DataProtection`. Supply a distinct, currently valid RSA certificate with its private key and at least 2048 bits for each purpose. Signing/encryption certificates are separate from HTTPS certificates. The Data Protection certificate encrypts the persisted key-ring XML. OpenIddict uses its configured credentials for client state and server tokens; Data Protection protects authorization/refresh-token formats, session payloads, and Identity tickets. These mechanisms must all remain available after a restart.

Environment-variable settings use double underscores, for example `Authentication__Certificates__ServerSigning__Password`, `Authentication__KeyPersistence__KeysPath`, and `Cors__AllowedOrigins__0`. Supply the other four certificate passwords the same way. Files must already be provisioned into private backend storage; the application does not generate production credentials. Configure storage permissions and verify persistence on the actual host before release. Replacing certificates or losing the key ring can invalidate existing tokens, pending callbacks, sessions, or confirmation links; credential rotation and recovery must be planned before release.

Configure these existing settings as well:

- Backend `ConnectionStrings:Identity` for the intended database.
- Backend `Email:ClientBaseUrl`, `Email:SenderAddress`, and `Email:Azure:Endpoint` for the existing production email adapter; its Azure identity/resource setup remains deferred.
- Published frontend `wwwroot/appsettings.json` with `ApiBaseUrl` set to the public API/BFF root HTTPS URL. This is public configuration and must contain no secrets.
- Production host environment, HTTPS termination, and frontend deep-link fallback. The selected Static Web Apps host needs SPA fallback to `index.html` for routes such as `/login`; the Development test server supplies this fallback locally.

Startup rejects missing persistence settings, invalid certificate files/passwords/private keys, duplicate certificate purposes, wildcard/credential-bearing/path/query/fragment origins, and a frontend origin absent from CORS. Error messages identify settings without echoing their values or certificate passwords. HTTP is accepted only for loopback origins in Development. Outside Development, OpenIddict startup also requires the existing Identity/email settings. Development can retain ephemeral credentials when persistence is disabled; those credentials do not promise restart continuity.

The saved public client registration is created on first sign-in. If the configured issuer changes, the next sign-in replaces the old callback URI with the exact configured callback. It does not retain the former URI as another valid destination. Current-session sign-out continues to use the antiforgery-protected BFF endpoint rather than a browser OAuth logout flow.

## Development test setup and reproduction

All integration/browser hosts use Development. Browser tests serve the published client and real API over HTTPS on separate loopback ports, with LocalDB, synthetic accounts, captured email, private temporary test certificates, and a temporary encrypted key ring. Restart coverage preserves the same fixture database and credentials. Fixtures delete only their own databases/key directories.

The browser contexts accept the fixture's self-signed HTTPS certificate. Server-side test HTTP clients accept only the matching fixture certificate for loopback requests. These exceptions exist only in test code; no application certificate-validation bypass was added. The tests demonstrate HTTPS protocol behavior, not public certificate issuance or Azure deployment feasibility.

From the repository root in PowerShell:

```powershell
dotnet build PersonalFinance.slnx
dotnet publish src/PersonalFinance.Client -c Release
pwsh -NoProfile -File tests/PersonalFinance.EndToEndTests/bin/Debug/net10.0/playwright.ps1 install firefox webkit
dotnet test --no-build --no-restore --project tests/PersonalFinance.Application.Tests
dotnet test --no-build --no-restore --project tests/PersonalFinance.Client.Tests
dotnet test --no-build --no-restore --project tests/PersonalFinance.IntegrationTests
pwsh -NoProfile -File tests/Run-BrowserMatrix.ps1 -BravePath 'C:/absolute/path/to/brave.exe'
```

Stop on build/publish failures. Chrome and Edge must be installed. Use an actual Brave binary; the test harness does not substitute Chromium for Brave. `PERSONAL_FINANCE_TEST_BROWSER` selects `chrome` (default), `edge`, `firefox`, `webkit`, or `brave`; `PERSONAL_FINANCE_BRAVE_PATH` supplies Brave's executable path. `PERSONAL_FINANCE_TEST_CLIENT` can override the published client's `wwwroot` path. The matrix script runs browsers serially, restores its environment variables afterward, and writes ignored logs to `TestResults/browser-matrix`. A missing or failed browser causes a nonzero result, never a silent skip.

WebKit is engine coverage, not a claim that Apple Safari was tested. Playwright Firefox uses the patched Firefox build associated with the selected Playwright package, not a separately installed retail Firefox. Browser data, profiles, certificates, and accounts are synthetic; no real messages are sent.

For the four executable browsers on this machine, use the explicitly limited matrix below. This does not include or satisfy Firefox verification:

```powershell
& ./tests/Run-BrowserMatrix.ps1 -Browsers @('chrome', 'edge', 'webkit', 'brave') -BravePath 'C:/absolute/path/to/brave.exe'
```

## Verification record — 4 November 2026

Red/green evidence:

- Six startup checks failed because malformed origins or enabled-but-unconfigured persistence were accepted. They passed after configuration validation was added; a seventh URL-with-query check already failed safely in OpenIddict and remains regression coverage.
- The configured-credential test caught the server still using an ephemeral RSA key instead of its configured certificate. After correcting the wiring it verified four distinct configured OpenIddict certificates and successfully decrypted a protected value in a fresh host. Persisted key XML contained `encryptedSecret` and no plaintext master key.
- The callback-change test found the old URI still registered. It passed after updating the existing registration to the configured exact callback.
- Certificate failure coverage verifies missing files, incorrect passwords, public-only certificates, and duplicate certificate purposes produce safe startup errors.
- Initial HTTPS fixture failures involved Windows TLS private-key handling, self-signed-certificate trust, and Playwright page setup. These were corrected in test fixtures and are not counted as application red/green evidence. An initial NuGet sandbox-access failure is also not red evidence.

Each completed browser run covered six workflows: registration/confirmation/replay; invalid-link resend recovery; narrow-screen/keyboard validation and connection retry; verified PKCE sign-in with server-side refresh and no browser token exposure; sign-out revoking rotated credentials while preserving another browser session; and HTTPS session recovery across a host restart with subsequent refresh/sign-out, antiforgery rejection, and browser CORS rejection from an unapproved origin.

| Check | Observed result |
| --- | --- |
| Application | 30 passed |
| Client components | 30 passed |
| Integration/LocalDB | 64 passed |
| Chrome | 6 passed |
| Edge | 6 passed |
| WebKit 26.0 / build 2215 | 6 passed; actual Safari deferred |
| Brave 1.98.63 | 6 passed using the official standalone archive, checksum verified and executable Authenticode signature valid |
| Firefox 142.0.1 / build 1495 | 6 could not execute: process launch failed before browser interaction |

Firefox's Playwright error was `spawn UNKNOWN`; launching its executable directly reported an incorrect side-by-side configuration. Windows SideBySide events identified the `mozglue` assembly dependency, although `mozglue.dll` was present. The user explicitly chose to retain the current Playwright version and move this verification to another compatible machine. The complete five-browser matrix therefore returns failure on this host; the four completed browser runs are recorded separately, with no Firefox pass or skip claimed.

The solution build succeeded with zero errors and 13 pre-existing warnings in unchanged integration tests (duplicate using and cancellation-token analyzer warnings). The Release client publish succeeded; the optional `wasm-tools` optimization workload remains absent. Changes are confined to Infrastructure, API composition/callback registration, test tooling, and documentation. Application and Domain projects and the database schema are unchanged; no migration was required.

## Deferred release evidence

Before release, provision and test the actual Azure combination: runtime support, SQL connectivity, persistent storage and keys, public HTTPS/callbacks, frontend route fallback, exact credentialed CORS, antiforgery, and browser cookie acceptance. Also execute Firefox on a compatible test machine and Safari on an Apple device, including their default privacy settings.

Separate Azure Static Web Apps and App Service default domains create a cross-site cookie boundary. Local loopback-port tests do not establish its feasibility. Safari/WebKit and Brave restrict third-party cookies; `SameSite=None; Secure` and credentialed CORS do not override browser privacy policy. The selected origins/hosting arrangement remains an unresolved release gate, to be resolved before deployment if the cookie flow fails. No hosting/domain change, Azure resource creation, live email check, or production certification was performed.

References: [OpenIddict credentials](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials), [ASP.NET Core key storage](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0), [Playwright browser coverage](https://playwright.dev/dotnet/docs/browsers), [WebKit tracking prevention](https://webkit.org/tracking-prevention/), and [Brave Shields](https://brave.com/shields/).
