# UC-014 — Track and adjust savings — States

[Written specification (UC-014)](../use-cases.md#uc-014) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Reset periods are informational notes, with no due calculation or automatic reset (D2). Positive/negative Savings entries add/withdraw funds. Opening amounts and manual targets create dated deltas T minus B; resetting creates minus B. Earlier corrections change later values even after reset (D1). Correction deletion, negative-balance policy, and date boundaries remain open.

```mermaid
stateDiagram-v2
    [*] --> LoadingSavings
    LoadingSavings --> SavingsVisible: Movement and correction history loaded
    SavingsVisible --> SettingsDraft: Writer edits goal or reset note
    SavingsVisible --> TargetDraft: Writer sets opening, balance edit, or manual zero target
    TargetDraft --> DeltaPrepared: Calculate target minus dated balance
    SettingsDraft --> Saving: Submit settings
    DeltaPrepared --> Saving: Submit dated delta
    Saving --> Recalculating: Save succeeds
    Saving --> SaveFailed: Save fails
    SaveFailed --> SavingsVisible: Review and retry
    Recalculating --> SavingsVisible: Later derived balances updated
    SavingsVisible --> Recalculating: Earlier approved movement or correction changes
    note right of DeltaPrepared
        Reset to zero is minus B, not a replacement checkpoint.
        Later backdated edits can change balances after reset.
    end note
    note right of SettingsDraft
        Reset note is informational; no due date or automatic reset.
    end note
```
