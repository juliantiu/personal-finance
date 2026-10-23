# UC-015 — Track and adjust debt — Sequence

[Written specification (UC-015)](../use-cases.md#uc-015) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Positive Debt payments increase paid/decrease remaining; negative payments reverse both (D6). Plans do not move debt. Opening/manual edits create dated metric deltas T minus B, with paid/remaining corrected independently (D1). Prior changes propagate. Correction deletion, negative balances, precision and boundaries remain open.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Review Debt position and history
    UI->>App: Request main-currency movements and metric deltas
    App-->>UI: Derived paid and remaining
    UI-->>User: Show debt position
    opt Authorized writer corrects a metric
        User->>UI: Select paid or remaining, effective time, and target T
        UI->>App: Request selected metric B at that time
        App-->>UI: Calculated metric B
        UI-->>User: Show correction T minus B
        User->>UI: Save dated correction
        UI->>App: Record delta for selected metric
        alt Save succeeds
            App->>App: Recompute subsequent metrics independently
            App-->>UI: Updated debt values
            UI-->>User: Show result
        else Save fails
            App-->>UI: Change not confirmed
            UI-->>User: Show failure
        end
    end
    Note over User,App: Change both metrics using two corrections, not a coupled override
    Note over UI,App: Positive payments increase paid and decrease remaining, negative reverses both
    Note over UI,App: Allocations are plans, earlier changes propagate, correction deletion undecided
```
