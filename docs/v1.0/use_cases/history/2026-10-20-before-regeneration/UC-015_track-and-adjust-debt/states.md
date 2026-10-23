# UC-015 — Track and adjust debt — States

[Written specification (UC-015)](../use-cases.md#uc-015)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Debt activity, paid/remaining calculations, and manual overrides are conceptual operations. Starting debt entry, overpayment, currency denomination, override precedence, and propagation of earlier corrections are unspecified. Calculated amounts remain editable.

```mermaid
stateDiagram-v2
    [*] --> LoadingDebt
    LoadingDebt --> DebtVisible: Paid and remaining values loaded
    DebtVisible --> DebtDraft: Writer adjusts payment activity
    DebtDraft --> CalculatedDraft: Paid and remaining values updated
    CalculatedDraft --> OverrideDraft: Writer edits calculated amounts
    CalculatedDraft --> SavingDebt: Submit values
    OverrideDraft --> SavingDebt: Submit corrected amounts
    SavingDebt --> RefreshingDebt: Save succeeds
    SavingDebt --> SaveFailed: Save fails
    SaveFailed --> DebtDraft: Review and retry
    RefreshingDebt --> DebtVisible: Updated values loaded
    note right of OverrideDraft
        Override precedence and later recalculation unspecified.
    end note
    note right of DebtVisible
        Previous periods remain editable.
        Carry-forward correction propagation unspecified.
    end note
```
