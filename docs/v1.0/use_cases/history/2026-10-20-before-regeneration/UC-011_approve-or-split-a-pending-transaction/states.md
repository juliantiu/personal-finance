# UC-011 — Approve or split a pending transaction — States

[Written specification (UC-011)](../use-cases.md#uc-011)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** A GBP transaction offers optional USD; a USD transaction offers optional GBP. The paired field may be blank. Split total preservation is an approved specification assumption, not a confirmed business rule; dual-currency distribution, split atomicity, and other currencies remain unspecified. Approval period differs from transaction date.

```mermaid
stateDiagram-v2
    [*] --> PendingReview
    PendingReview --> ApprovalDraft: Select period and subcategory
    ApprovalDraft --> ApprovalDraft: Set optional other-currency amount, payment, comment, tags
    ApprovalDraft --> SplitDraft: Split into linked entries
    SplitDraft --> Approving: Submit split approval
    ApprovalDraft --> Approving: Submit single approval
    Approving --> InputCorrection: Income amount not positive
    InputCorrection --> ApprovalDraft: Revise entry details
    Approving --> Approved: Approval succeeds
    Approving --> ApprovalFailed: Approval fails
    ApprovalFailed --> PendingReview: Continue review
    ApprovalDraft --> PendingReview: Leave without approval
    SplitDraft --> PendingReview: Leave without approval
    Approved --> [*]
    note right of ApprovalDraft
        GBP offers optional USD; USD offers optional GBP.
        Other-currency behavior remains unspecified.
    end note
    note right of SplitDraft
        Total preservation is an assumption.
        Currency distribution and atomicity are unresolved.
    end note
```
