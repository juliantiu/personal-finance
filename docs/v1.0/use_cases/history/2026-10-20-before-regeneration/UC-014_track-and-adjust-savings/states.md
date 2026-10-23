# UC-014 — Track and adjust savings — States

[Written specification (UC-014)](../use-cases.md#uc-014)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Savings additions/withdrawals are conceptual recorded activity; whether they are transactions or separate adjustments is unspecified. Reset format, manual reset behavior, negative balances, currency denomination, and propagation of historical corrections are unresolved. A due reset never automatically resets the balance.

```mermaid
stateDiagram-v2
    [*] --> LoadingSavings
    LoadingSavings --> SavingsVisible: Values and activity loaded
    SavingsVisible --> ResetDueVisible: Reset period is due
    ResetDueVisible --> ResetDueVisible: Due indication without automatic reset
    SavingsVisible --> SavingsDraft: Writer edits values or activity
    ResetDueVisible --> SavingsDraft: Writer edits balance, goal, or activity
    SavingsDraft --> SavingSavings: Submit changes
    SavingSavings --> RefreshingSavings: Save succeeds
    SavingSavings --> SaveFailed: Save fails
    SaveFailed --> SavingsDraft: Review and retry
    RefreshingSavings --> SavingsVisible: Updated values loaded
    note right of ResetDueVisible
        No automatic balance reset.
        Manual reset behavior is unspecified.
    end note
    note right of SavingsDraft
        Prior withdrawals and balances remain editable.
        Later-period propagation is unspecified.
    end note
```
