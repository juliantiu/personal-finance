# UC-006 — Create the first budget period — Sequence

[Written specification (UC-006)](../use-cases.md#uc-006)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Dates, item selections, and allocation entry are conceptual controls. Date validation, empty selections, overlapping periods, and allocation limits are not defined. Category allocations must sum the selected subcategory allocations.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Start first budget period
    UI->>App: Request profile categories and subcategories
    App-->>UI: Available profile items
    User->>UI: Set dates, select items, and allocate subcategories
    UI->>App: Request period creation with item references
    Note over UI,App: Calculation location is illustrative; validation rules unspecified
    App->>App: Calculate category sums from selected subcategories
    alt Creation succeeds
        App-->>UI: Saved period with dates and allocations
        UI-->>User: Show period ready for activity
    else Save or input issue
        App-->>UI: Creation not confirmed
        UI-->>User: Show issue for correction
    end
```
