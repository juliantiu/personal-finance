# UC-018 — Report spending by date and category — States

[Written specification (UC-018)](../use-cases.md#uc-018)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Spending classification/netting, currency basis, historical filter presentation, date boundaries, and time zone are unresolved. All-date reporting and selected-date reporting use transaction dates across periods and preserve item identity across renames. The report does not change source records.

```mermaid
stateDiagram-v2
    [*] --> FilterSelection
    FilterSelection --> InvalidRange: Selected date range not accepted
    InvalidRange --> FilterSelection: Correct dates
    FilterSelection --> LoadingSpending: Submit all dates or accepted range and item filters
    LoadingSpending --> SpendingVisible: Matching totals calculated
    LoadingSpending --> EmptySpending: No matching activity
    LoadingSpending --> ReportFailed: Loading or calculation fails
    ReportFailed --> FilterSelection: Review and retry
    SpendingVisible --> FilterSelection: Change or clear filters
    EmptySpending --> FilterSelection: Change or clear filters
    note right of LoadingSpending
        Use transaction dates and stable item identity.
        Currency, netting, and historical filter rules unspecified.
    end note
```
