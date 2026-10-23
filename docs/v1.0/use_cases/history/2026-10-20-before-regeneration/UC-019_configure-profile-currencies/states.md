# UC-019 — Configure profile currencies — States

[Written specification (UC-019)](../use-cases.md#uc-019) · [Currency clarifications](../use-cases.md#current-currency-rules-and-precedence)

Conceptual application interaction states for approved proposal version **v1.0**, rather than implemented persistence states or screen contracts.

**Scope and open rules:** a main-only configuration is valid because additional currencies are optional. Saving requires writer permission. The initial main-currency default, changes to the main currency after recorded activity, disabling used currencies, and further currency-catalog management are unspecified. Their handling is not modeled as an implemented state or validation rule. Failed-save feedback follows the approved shared failure-handling assumption.

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
