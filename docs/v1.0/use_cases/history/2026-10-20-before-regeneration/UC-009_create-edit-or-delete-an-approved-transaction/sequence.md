# UC-009 — Create, edit, or delete an approved transaction — Sequence

[Written specification (UC-009)](../use-cases.md#uc-009)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Manual entry defaults to GBP with an optional USD amount for the same transaction; no conversion is implied. Cash is available without a default selection. Paired-amount validation and linked split/IOU effects on edit or deletion remain unspecified. Existing transaction correction is included; splitting is modeled in UC-011.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Select period and transaction operation
    alt Create manually
        UI-->>User: Entry with GBP default and optional USD amount
        User->>UI: Enter amount, subcategory, payment, optional comment and tags
        UI->>App: Save manual transaction in selected period
        Note over UI,App: Cash is available but not default; optional USD may be blank
    else Edit approved transaction
        UI->>App: Request approved transaction
        App-->>UI: Existing details
        User->>UI: Edit transaction
        UI->>App: Save edits
    else Delete approved transaction
        User->>UI: Delete transaction
        UI->>App: Delete approved transaction
    end
    alt Save with invalid Income amount
        App-->>UI: Positive Income amount required
        UI-->>User: Show correction requirement
    else Operation succeeds
        App->>App: Auto-approve manual creation or apply edit or deletion
        App->>App: Reflect affected activity in summaries and reports
        App-->>UI: Successful result
        UI-->>User: Show updated activity
    else Operation fails
        App-->>UI: Change not confirmed
        UI-->>User: Show failure
    end
    Note over UI,App: Conversion, paired-amount validation, and linked-record effects unspecified
```
