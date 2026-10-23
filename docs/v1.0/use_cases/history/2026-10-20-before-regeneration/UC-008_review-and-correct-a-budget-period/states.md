# UC-008 — Review and correct a budget period — States

[Written specification (UC-008)](../use-cases.md#uc-008)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Expected-income entry, deselection of items with history, propagation of prior-period savings/debt corrections, and currency basis for totals remain unspecified. Other financial corrections are separate use cases, not new actions added here.

```mermaid
stateDiagram-v2
    [*] --> LoadingPeriod
    LoadingPeriod --> PeriodVisible: Period and summaries loaded
    PeriodVisible --> ReadOnlyReview: Viewer reviews
    PeriodVisible --> SetupDraft: Writer edits selections or allocations
    SetupDraft --> SavingCorrection: Submit correction
    SavingCorrection --> RefreshingSummary: Save succeeds
    RefreshingSummary --> PeriodVisible: Updated category and period totals
    SavingCorrection --> SaveFailed: Save fails
    SaveFailed --> SetupDraft: Review and retry
    PeriodVisible --> LoadingPeriod: Choose another period
    note right of PeriodVisible
        Remaining allowance = allocations minus spending.
        Remaining income = income minus spending.
        Currency basis is unresolved.
    end note
```
