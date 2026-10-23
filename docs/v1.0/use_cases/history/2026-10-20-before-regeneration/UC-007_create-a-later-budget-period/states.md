# UC-007 — Create a later budget period — States

[Written specification (UC-007)](../use-cases.md#uc-007)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** “Previous period” ordering and propagation of earlier balance corrections are unresolved. Removed subcategories cannot resume future tracking; how they appear in a copied draft is unspecified. Creation copies references/allocations, carries savings/debt, and starts spending at zero without copying transactions.

```mermaid
stateDiagram-v2
    [*] --> PreparingTemplate
    PreparingTemplate --> CopiedDraft: Previous references and allocations available
    CopiedDraft --> CopiedDraft: Adjust dates, selections, or allocations
    CopiedDraft --> SavingPeriod: Submit new period
    SavingPeriod --> PeriodReady: Creation succeeds
    SavingPeriod --> CreationFailed: Creation fails
    CreationFailed --> CopiedDraft: Review and retry
    PeriodReady --> [*]
    note right of CopiedDraft
        Spending starts at zero.
        Savings and debt carry forward.
        Removed items do not resume future tracking.
    end note
    note right of PeriodReady
        Earlier periods remain unchanged by this creation.
    end note
```
