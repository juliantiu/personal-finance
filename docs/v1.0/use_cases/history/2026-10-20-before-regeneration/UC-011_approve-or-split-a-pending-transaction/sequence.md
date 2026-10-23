# UC-011 — Approve or split a pending transaction — Sequence

[Written specification (UC-011)](../use-cases.md#uc-011)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** A GBP transaction offers optional USD; a USD transaction offers optional GBP. The paired field may be blank. Split total preservation is an approved specification assumption, not a confirmed business rule; dual-currency distribution, split atomicity, and other currencies remain unspecified. Approval period differs from transaction date.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open pending transaction
    UI->>App: Request imported details and payment match
    App-->>UI: Pending record with amount and currency
    User->>UI: Select approval period and subcategory
    alt USD source
        UI-->>User: Offer optional GBP amount
    else GBP source
        UI-->>User: Offer optional USD amount
    else Missing or other currency
        Note over User,App: Approval behavior unspecified for this currency
    end
    User->>UI: Confirm payment; optionally add paired amount, comment, tags
    opt Split transaction
        User->>UI: Define linked entries with amounts and different subcategories
        loop Split entry
            User->>UI: Optionally assign comment and multiple tags
        end
        Note over UI,App: Split total preservation assumed; dual-currency distribution unspecified
    end
    alt User leaves without approval
        UI-->>User: Transaction remains pending
    else User submits approval for a defined currency path
        UI->>App: Approve single or linked entries in selected period
        alt Income entry not positive
            App-->>UI: Positive Income amount required
            UI-->>User: Show correction requirement
        else Approval succeeds
            App->>App: Save approved entries, links, currency values, comments, tags
            App->>App: Resolve pending status and reflect financial activity
            App-->>UI: Approved result
            UI-->>User: Show approval confirmation
        else Approval fails
            App-->>UI: Approval not confirmed
            UI-->>User: Show failure; continue review
        end
    end
    Note over UI,App: No automatic conversion assumed; split atomicity unspecified
```
