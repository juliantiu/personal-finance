# UC-007 — Create a later budget period — Sequence

[Written specification (UC-007)](../use-cases.md#uc-007)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** “Previous period” ordering and propagation of earlier balance corrections are unresolved. Removed subcategories cannot resume future tracking; how they appear in a copied draft is unspecified. Creation copies references/allocations, carries savings/debt, and starts spending at zero without copying transactions.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Create later period with dates
    UI->>App: Request previous-period starting values
    Note over UI,App: Predecessor ordering and correction propagation unspecified
    App-->>UI: References, allocations, carried savings and debt
    UI-->>User: Show draft with zero starting spending
    User->>UI: Keep or adjust selections and allocations
    Note over UI,App: Removed subcategories cannot resume future tracking
    UI->>App: Save independently configured new period
    App->>App: Calculate category sums; preserve earlier periods
    alt Creation succeeds
        App-->>UI: New period saved
        UI-->>User: Show new period ready
    else Save fails
        App-->>UI: Creation not confirmed
        UI-->>User: Show failure
    end
```
