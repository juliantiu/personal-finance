# UC-006 — Create the first budget period — Sequence

[Written specification (UC-006)](../use-cases.md#uc-006) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Allocations use the profile main currency; Income allocation is expected Income (D5). Deleted items and children of deleted categories cannot be newly selected. Omission is not deletion. Date validation, nonnegative allocation policy A11, and empty-selection rules remain qualified.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Start first budget period
    UI->>App: Request profile categories and subcategories
    App-->>UI: Available profile items
    User->>UI: Set dates and available selections, allocate in main currency
    UI->>App: Request period creation with item references
    Note over UI,App: Calculation location is illustrative, validation rules unspecified
    App->>App: Calculate category sums, Income allocation is expected income
    alt Creation succeeds
        App-->>UI: Saved period with dates and allocations
        UI-->>User: Show period ready for activity
    else Save or input issue
        App-->>UI: Creation not confirmed
        UI-->>User: Show issue for correction
    end
```
