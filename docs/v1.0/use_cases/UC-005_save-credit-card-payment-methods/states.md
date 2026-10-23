# UC-005 — Save credit card payment methods — States

[Written specification (UC-005)](../use-cases.md#uc-005) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Cards are profile payment methods and Cash has no default selection. Canonical card-number uniqueness is proposed A3; edit/removal, formatting, and duplicate handling are not confirmed user workflows.

```mermaid
stateDiagram-v2
    [*] --> LoadingMethods
    LoadingMethods --> MethodsReady: Profile methods loaded
    MethodsReady --> CardDraft: Enter card number
    CardDraft --> SavingCard: Submit card
    SavingCard --> CardAvailable: Save succeeds
    SavingCard --> SaveFailed: Save fails
    SaveFailed --> CardDraft: Correct or retry
    CardAvailable --> MethodsReady: Review reusable methods
    note right of CardAvailable
        Reusable across profile periods.
        No default payment selection implied.
    end note
```
