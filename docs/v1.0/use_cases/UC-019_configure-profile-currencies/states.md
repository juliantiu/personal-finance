# UC-019 — Configure profile currencies — States

[Written specification (UC-019)](../use-cases.md#uc-019) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** The main unit governs allocations, goals, corrections, summaries and reports; alternatives are manually entered without FX and excluded from totals (D11). Foreign approval requires main values and complete per-currency splits. Historical main changes, used-currency disabling, and source enablement are proposed A12/A13 policies, not newly confirmed workflows. No initial main default is defined.

```mermaid
stateDiagram-v2
    [*] --> LoadingSettings
    LoadingSettings --> SettingsVisible: Available currencies and settings loaded
    SettingsVisible --> ReadOnlyReview: View-only participant
    SettingsVisible --> CurrencyDraft: Owner or editor selects configuration
    CurrencyDraft --> CurrencyDraft: Select main currency and optional alternatives or none
    CurrencyDraft --> SavingSettings: Submit currency settings
    SavingSettings --> SettingsSaved: Save succeeds
    SavingSettings --> SaveFailed: Save fails
    SaveFailed --> CurrencyDraft: Review and retry
    SettingsSaved --> SettingsVisible: Review saved configuration
    ReadOnlyReview --> [*]: Finish viewing
    note right of CurrencyDraft
        GBP and USD suffice initially; no default is specified.
        Historical main-currency changes and used-currency
        disabling require rules that remain undefined.
    end note
    note right of SettingsSaved
        Main currency governs allocations, summaries, and reports.
        Alternative amounts are manually entered and excluded from totals.
        Foreign-import approval requires a main-currency amount.
        The system performs no currency conversion.
    end note
```
