# UC-009 — Create, edit, or delete an approved transaction — States

[Written specification (UC-009)](../use-cases.md#uc-009) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Main-currency manual creation is approved immediately; alternatives are manually entered and excluded from totals. Income supplied values must be positive. Shared split facts and each retained currency total must stay consistent through edits/deletions; dependent links must be resolved. Schema A8 import-source retention is proposed, not an established requeue workflow.

```mermaid
stateDiagram-v2
    [*] --> PeriodSelected
    PeriodSelected --> ManualDraft: Create with profile main currency
    PeriodSelected --> ApprovedVisible: Open approved transaction
    ManualDraft --> SavingTransaction: Submit with optional alternatives, comment, tags
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
        Alternatives describe the same event and are excluded from totals.
        Cash has no default selection.
    end note
```
