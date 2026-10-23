# UC-014 — Track and adjust savings — Sequence

[Written specification (UC-014)](../use-cases.md#uc-014) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Reset periods are informational notes, with no due calculation or automatic reset (D2). Positive/negative Savings entries add/withdraw funds. Opening amounts and manual targets create dated deltas T minus B; resetting creates minus B. Earlier corrections change later values even after reset (D1). Correction deletion, negative-balance policy, and date boundaries remain open.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open Savings history and settings
    UI->>App: Request approved movements, deltas, goal, and reset note
    App-->>UI: Derived main balance and settings
    UI-->>User: Show balance, additions, withdrawals, and informational note
    opt Authorized writer changes savings
        alt Edit goal or reset note
            User->>UI: Enter settings
            UI->>App: Save settings without scheduling a reset
        else Opening amount, manual edit, or manual reset
            User->>UI: Choose effective time and target T, or zero for reset
            UI->>App: Calculate balance B at that effective time
            App-->>UI: Complete main-currency balance B
            UI-->>User: Show correction delta T minus B
            User->>UI: Save dated correction with optional explanation
            UI->>App: Record signed correction, not fixed checkpoint
        end
        alt Save succeeds
            App->>App: Derive subsequent balances from movements and deltas
            App-->>UI: Updated savings position
            UI-->>User: Show saved result
        else Save fails
            App-->>UI: Change not confirmed
            UI-->>User: Show failure
        end
    end
    Note over User,App: Savings movements use UC-009/011, positive adds and negative withdraws
    opt Earlier movement or correction changes
        App->>App: Recalculate later balances, including after manual resets
        App-->>UI: Updated derived balance when reviewed
    end
    Note over UI,App: Omission or explicit deletion does not erase balance history, correction deletion undecided
```
