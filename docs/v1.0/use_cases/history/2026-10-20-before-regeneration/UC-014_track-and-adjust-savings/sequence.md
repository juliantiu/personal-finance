# UC-014 — Track and adjust savings — Sequence

[Written specification (UC-014)](../use-cases.md#uc-014)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Savings additions/withdrawals are conceptual recorded activity; whether they are transactions or separate adjustments is unspecified. Reset format, manual reset behavior, negative balances, currency denomination, and propagation of historical corrections are unresolved. A due reset never automatically resets the balance.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open Savings subcategory and period
    UI->>App: Request balance, goal, reset period, additions, withdrawals
    App-->>UI: Savings values and due indication
    UI-->>User: Show savings and reset due if applicable
    Note over UI,App: Reset due never automatically resets balance
    opt Authorized writer changes savings
        User->>UI: Edit values, additions, or withdrawals, including prior period
        UI->>App: Save savings correction
        alt Save succeeds
            App->>App: Reflect saved values in savings tracking and reports
            App-->>UI: Updated savings
            UI-->>User: Show saved result
        else Save fails
            App-->>UI: Change not confirmed
            UI-->>User: Show failure
        end
    end
    Note over UI,App: Transaction versus adjustment model and propagation unspecified
    opt Later period created through UC-007
        App->>App: Carry savings balance into new period
    end
```
