# UC-009 — Create, edit, or delete an approved transaction — Sequence

[Written specification (UC-009)](../use-cases.md#uc-009) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Main-currency manual creation is approved immediately; alternatives are manually entered and excluded from totals. Income supplied values must be positive. Shared split facts and each retained currency total must stay consistent through edits/deletions; dependent links must be resolved. Schema A8 import-source retention is proposed, not an established requeue workflow.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Select period and transaction operation
    alt Create manually
        UI-->>User: Entry in main currency with optional enabled alternatives
        User->>UI: Enter amount, subcategory, payment, optional comment and tags
        UI->>App: Save manual transaction in selected period
        Note over UI,App: Cash is not default, optional alternatives may be blank, no conversion
    else Edit approved transaction
        UI->>App: Request approved transaction
        App-->>UI: Existing details
        User->>UI: Edit transaction
        UI->>App: Save consistent group and entry edits
        App->>App: Preserve shared facts and reconcile each supplied currency
    else Delete approved transaction
        User->>UI: Delete transaction
        UI->>App: Delete approved activity with dependent links resolved
        Note over UI,App: Rebalance remaining split portions or delete whole operation
    end
    alt Any supplied Income valuation is not positive
        App-->>UI: Positive Income amount required
        UI-->>User: Show correction requirement
    else Operation succeeds
        App->>App: Auto-approve manual creation or apply edit or deletion
        App->>App: Recalculate financial effects and subsequent dated balances
        App-->>UI: Successful result
        UI-->>User: Show updated activity
    else Operation fails
        App-->>UI: Change not confirmed
        UI-->>User: Show failure
    end
    Note over UI,App: No conversion, precision/sign rules open, source retention after deletion is proposed A8
```
