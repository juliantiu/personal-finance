# UC-005 — Save credit card payment methods — Sequence

[Written specification (UC-005)](../use-cases.md#uc-005)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Saved cards are identified by their numbers and belong to the profile. Card-number validation, duplicate handling, and card editing/removal are unspecified. Cash is always a transaction choice and is not selected by default; saving a card does not choose a default.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open profile payment methods
    UI->>App: Request saved profile cards
    App-->>UI: Saved cards
    UI-->>User: Show methods
    User->>UI: Enter identifying card number and save
    UI->>App: Save credit card for profile
    alt Save succeeds
        App-->>UI: Reusable saved card
        UI-->>User: Confirm card available across periods
    else Save fails
        App-->>UI: Save issue
        UI-->>User: Show issue without confirming save
    end
    Note over UI,App: Duplicate and lifecycle rules unspecified
    Note over User,UI: Cash is always available for transactions, without default selection
```
