# UC-005 — Save credit card payment methods — Sequence

[Written specification (UC-005)](../use-cases.md#uc-005) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Cards are profile payment methods and Cash has no default selection. Canonical card-number uniqueness is proposed A3; edit/removal, formatting, and duplicate handling are not confirmed user workflows.

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
