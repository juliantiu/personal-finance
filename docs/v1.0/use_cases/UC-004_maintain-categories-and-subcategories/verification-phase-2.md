# UC-004 Phase 2 — Verification

Verified 10 November 2026 after the user's approval of the Phase 2 implementation plan and the category metadata rules. `docs/v1.0/` was the highest numeric version directory. This evidence covers ordinary category creation and category name/comment editing only.

## Agreed rules

- Names are required and nonblank after trimming, with a maximum of 100 characters after trimming. Duplicate display names are allowed; records are identified by stable IDs.
- Comments are optional plain text, up to 2,000 characters. Empty and whitespace-only comments are stored as absent. Interior line breaks are retained and displayed.
- Category edits use a SQL Server row version scoped to the category record. A stale version returns a conflict, and the client retains the user's draft until the latest category version is explicitly loaded.

## Delivered behavior

- Owners and current editors can create ordinary categories and edit metadata on ordinary and system categories. Viewers can read names and comments but cannot write.
- System category kind and profile association remain fixed during edits. Existing category IDs and system defaults remain intact. Multiple categories with the same display name are supported.
- Category reads return stable IDs, comments, and opaque version tokens. Profile overview projections now include category IDs, and the client keys rows by identity.
- Mutations use the current BFF account, antiforgery validation, profile-scoped server authorization, and no-store responses. A target from another profile is unavailable to the write. Invalid input leaves stored values untouched.
- The management page validates and submits category forms, refreshes after successful writes, preserves drafts after conflicts, distinguishes viewer access, and requires a category review before retrying a create with an uncertain outcome.
- The migration allows `ordinary`, adds nullable comments, and adds row-version concurrency. It retains the existing filtered uniqueness for the three system kinds.

No subcategory behavior, tracking-stop/deletion behavior, SavingsPlan records, or financial history was added.

## Red/green evidence

Before production mutation behavior was added, the new API integration tests failed at the intended missing behavior: category creation returned **405 Method Not Allowed** instead of **201 Created**, and the category update route returned **404 Not Found** instead of validation feedback. After implementation, the focused mutation tests passed for ordinary creation, system-category rename/comment, owner/editor success, viewer denial, antiforgery rejection, forged kind/profile fields, cross-profile substitution, input boundaries, and stale-edit preservation.

The new migration-upgrade test started from the previous profile-creation migration, then verified that an existing account, session, profile, and all three system category IDs/names/kinds survived. The new comments were null and SQL Server generated eight-byte row versions for the retained categories.

## Final checks

| Check | Result |
| --- | --- |
| `dotnet build PersonalFinance.slnx --no-restore --verbosity minimal` | Passed; 0 warnings and 0 errors on the final build |
| `dotnet test tests/PersonalFinance.Domain.Tests --no-build --no-restore --verbosity minimal` | 23 passed |
| `dotnet test tests/PersonalFinance.Application.Tests --no-build --no-restore --verbosity minimal` | 49 passed |
| `dotnet test tests/PersonalFinance.Client.Tests --no-build --no-restore --verbosity minimal` | 78 passed |
| `dotnet test tests/PersonalFinance.IntegrationTests --no-build --no-restore --verbosity minimal` | 103 passed, including isolated LocalDB migration and API tests |
| `dotnet publish src/PersonalFinance.Client -c Release --no-restore --verbosity minimal` | Passed; publish emitted the existing optional `wasm-tools` optimization notice |
| `dotnet test tests/PersonalFinance.EndToEndTests --no-build --no-restore --filter FullyQualifiedName~ProfileCategoriesBrowserTests --verbosity minimal` | 1 focused Chrome workflow passed: sign-in return path, create, duplicate name, rename, reload, viewer read, and revoked access |
| `git diff --check` | Passed |

The complete EndToEndTests suite was also run. Four browser cases failed during sign-in navigation when run as a suite: `AccountBrowserTests.Cross_site_client_can_sign_in_again_after_sign_out`, `CreateProfileBrowserTests.Signed_in_account_can_create_complete_profiles_with_explicit_currency_settings`, `ProfileListBrowserTests.Protected_list_supports_real_sign_in_current_grants_and_recoverable_load_failure`, and the category browser test. The category workflow passed when rerun alone against the final published client; the other three authentication workflows were not changed by Phase 2 and remain failures in this full-suite run. No other browser engines or deployed origins were tested.

The build/test commands requiring LocalDB or browser execution ran outside the sandbox. Test databases were isolated synthetic databases; no development database was migrated.

## Remaining gates

Phases 3–7 remain unapproved and incomplete. Subcategory concurrency and the SavingsPlan creation boundary still require agreement. Parent-retirement/child-write concurrency, editing retained items, and repeated deletion rules remain unresolved for the later lifecycle phases. Financial-history retention must be verified only when its dependent use cases exist.
