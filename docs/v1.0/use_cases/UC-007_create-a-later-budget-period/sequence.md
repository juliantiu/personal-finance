# UC-007 — Create a later budget period — Sequence

[Written specification (UC-007)](../use-cases.md#uc-007) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Previous periods follow date order (D6). Exclude explicitly deleted parent/child items; active omitted items can be selected again. Savings/debt carry is derived from movements and dated deltas; prior corrections affect later balances. Equal/overlapping-date predecessor and boundary policies remain open.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Create later period with dates
    UI->>App: Request date-ordered previous-period selections and current balance inputs
    Note over UI,App: Use date order, earlier corrections propagate, ambiguous predecessor policy open
    App-->>UI: Available references, copied allocations, derived savings and debt
    UI-->>User: Show draft with zero starting spending
    User->>UI: Keep or adjust selections and allocations
    Note over UI,App: Removed subcategories cannot resume future tracking
    UI->>App: Save independently configured new period
    App->>App: Calculate category sums, preserve earlier periods
    alt Creation succeeds
        App-->>UI: New period saved
        UI-->>User: Show new period ready
    else Save fails
        App-->>UI: Creation not confirmed
        UI-->>User: Show failure
    end
```
