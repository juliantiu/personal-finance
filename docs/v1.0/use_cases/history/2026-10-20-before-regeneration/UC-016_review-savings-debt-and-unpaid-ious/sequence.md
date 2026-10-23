# UC-016 — Review savings, debt, and unpaid IOUs — Sequence

[Written specification (UC-016)](../use-cases.md#uc-016)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** This report is read-only and combines profile history by item identity, including renames. Inclusion of removed items in current balance lists, empty-total presentation, and currency basis are unresolved. IOU unpaid amounts depend on saved repayment links and eventual attribution rules.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open savings, debt, and IOU reports
    UI->>App: Request profile financial position across periods
    App->>App: Combine same-item history across renames
    App->>App: Assemble savings goals and totals, debt values, unpaid linked IOUs
    Note over UI,App: Currency basis, removed-item inclusion, and IOU attribution unresolved
    alt Report succeeds with records
        App-->>UI: Savings, debt, and unpaid IOU results
        UI-->>User: Show financial position
    else No relevant records
        App-->>UI: No relevant records
        UI-->>User: Show absence of activity
    else Loading or calculation fails
        App-->>UI: Report failure
        UI-->>User: Show issue without presenting incomplete results as complete
    end
    Note over User,App: Report does not change source records
```
