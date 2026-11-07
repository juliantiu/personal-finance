# UC-002 — Phase 5 verification

Date: **7 November 2026**. Scope: [approved implementation plan, Phase 5](implementation-plan.md). Phase 5 is complete; Phase 6 has not started. `docs/v1.0/` remains the latest and only version directory.

## Delivered recovery behavior

The create form now distinguishes a rejected antiforgery proof from ordinary validation feedback. It tells the user that creation was rejected and that submitting again will fetch a fresh security proof. It preserves the entered values and never retries automatically.

After an uncertain creation result, the form still requires a successful list refresh before enabling an explicit retry. Refreshed profile entries now link directly to their profile detail route, so a profile that was saved despite a lost response can be opened without another creation POST. An expired session during profile detail loading returns to the existing login flow; the page does not display protected profile content after the `401` response. Existing generic unavailable handling continues to cover a profile whose access was removed.

## Red/green evidence

- **New recovery cases:** Before the UI changes, the focused profile UI run had **2 failed, 16 passed**. The failures showed that antiforgery rejection did not explain the next action and that a profile found after refresh had no direct open link.
- **Focused profile UI tests:** After the changes, **18 passed**, including the antiforgery recovery message, direct open link after uncertain creation, and expired-session redirect with no profile details retained.
- **Client regression:** The complete Client test suite passed: **57 passed, 0 failed, 0 skipped**. This includes existing controlled network/server failures, uncertain POST outcomes, input preservation, explicit refresh-before-retry, blocked duplicate submission, refresh failure, missing session, and route/access behavior.
- **Profile creation integration:** **15 passed, 0 failed, 0 skipped**, including missing/invalid antiforgery proof returning `400` without persisting a profile, atomic rollback, and session rejection.
- The focused integration build reported existing warnings in unrelated account test files (`CS0105` duplicate using and `xUnit1051` cancellation-token analyzer warnings).

## Repeatable commands

Run from the repository root with the installed .NET 10 SDK. This environment's test runner required execution outside the sandbox; the client test project restore was needed to obtain the `Mono.browser-wasm` runtime pack.

```powershell
dotnet restore tests/PersonalFinance.Client.Tests --verbosity minimal
dotnet test --no-restore --project tests/PersonalFinance.Client.Tests -- --filter-class '*CreateProfilePageTests' --filter-class '*BudgetProfileDetailsPageTests'
dotnet test --no-restore --project tests/PersonalFinance.Client.Tests
dotnet test --no-restore --project tests/PersonalFinance.IntegrationTests -- --filter-class '*CreateProfileTests'
git diff --check
```

## Checkpoint

Cross-flow recovery leaves a clear next action for rejected antiforgery, lost creation responses, expired sessions, dependency failures, and removed access. Phase 5 ends here. Continue to Phase 6 only when the user indicates readiness.
