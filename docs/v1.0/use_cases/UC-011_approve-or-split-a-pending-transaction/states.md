# UC-011 — Approve or split a pending transaction — States

[Written specification (UC-011)](../use-cases.md#uc-011) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** D3/D7 require all split entries to share one period/date/payment and approve together. D10 requires independent complete reconciliation in every supplied currency; D11 requires main currency before foreign approval. All supplied Income amounts are positive. Precision and proposed source-currency enablement A13 remain open; failed approval has no partial financial effects.

```mermaid
stateDiagram-v2
    [*] --> PendingReview
    PendingReview --> ApprovalDraft: Choose shared period, date, payment
    ApprovalDraft --> AwaitingMain: Foreign import lacks main amount
    AwaitingMain --> ApprovalDraft: User supplies main amount
    ApprovalDraft --> SplitDraft: Define linked entries and distributions
    ApprovalDraft --> Validating: Submit single entry
    SplitDraft --> Validating: Submit all linked entries
    Validating --> InputCorrection: Missing value, unreconciled currency, or invalid Income
    InputCorrection --> ApprovalDraft: Revise details or portions
    Validating --> ApprovingTogether: All independent currency checks pass
    ApprovingTogether --> Approved: Entire operation succeeds
    ApprovingTogether --> ApprovalFailed: Save fails without partial effects
    ApprovalFailed --> PendingReview: Continue review
    ApprovalDraft --> PendingReview: Leave without approval
    SplitDraft --> PendingReview: Leave without approval
    Approved --> [*]
    note right of SplitDraft
        One date, payment method, and period.
        Every supplied currency reconciles independently.
        Comments and tags belong to individual entries.
    end note
```
