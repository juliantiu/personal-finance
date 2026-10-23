# UC-016 — Review savings, debt, and unpaid IOUs — Sequence

[Written specification (UC-016)](../use-cases.md#uc-016) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Main-currency reports combine stable item identity, approved signed movements, dated metric deltas, and valid repayment links. Explicit deletion or selection gaps do not hide balances/outstanding obligations. IOU credit A6 and time boundaries A10 remain qualified; report failure never implies complete results.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open savings, debt, and IOU reports
    UI->>App: Request profile financial position across periods
    App->>App: Combine same-item history across renames
    App->>App: Derive movement-plus-delta balances, independent debt metrics, and linked unpaid IOUs
    Note over UI,App: Main only, retired obligations retained, A6 IOU credit and A10 boundaries proposed
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
