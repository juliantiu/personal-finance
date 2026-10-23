# UC-009 — Create, edit, or delete an approved transaction — States

[Written specification (UC-009)](../use-cases.md#uc-009)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Manual entry defaults to GBP with an optional USD amount for the same transaction; no conversion is implied. Cash is available without a default selection. Paired-amount validation and linked split/IOU effects on edit or deletion remain unspecified. Existing transaction correction is included; splitting is modeled in UC-011.

```mermaid
stateDiagram-v2
    [*] --> PeriodSelected
    PeriodSelected --> ManualDraft: Create with GBP default
    PeriodSelected --> ApprovedVisible: Open approved transaction
    ManualDraft --> SavingTransaction: Submit with optional USD, comment, tags
    ApprovedVisible --> EditDraft: Edit details
    EditDraft --> SavingTransaction: Submit edits
    ApprovedVisible --> DeletingTransaction: Delete
    SavingTransaction --> InputCorrection: Income amount not positive
    InputCorrection --> SavingTransaction: Correct and resubmit
    SavingTransaction --> ApprovedSaved: Save succeeds
    SavingTransaction --> OperationFailed: Save fails
    DeletingTransaction --> Deleted: Delete succeeds
    DeletingTransaction --> OperationFailed: Delete fails
    OperationFailed --> PeriodSelected: Review and retry
    ApprovedSaved --> ApprovedVisible: Review saved transaction
    Deleted --> PeriodSelected: Review updated activity
    note right of ManualDraft
        Optional USD is another value for the same transaction.
        Cash has no default selection.
    end note
```
