# UC-011 — Approve or split a pending transaction — Sequence

[Written specification (UC-011)](../use-cases.md#uc-011) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** D3/D7 require all split entries to share one period/date/payment and approve together. D10 requires independent complete reconciliation in every supplied currency; D11 requires main currency before foreign approval. All supplied Income amounts are positive. Precision and proposed source-currency enablement A13 remain open; failed approval has no partial financial effects.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open pending source
    UI->>App: Request retained source facts and card match
    App-->>UI: Original details and amount/currency
    User->>UI: Choose shared approval period, date/time, and payment method
    alt Source is main currency
        UI-->>User: Use source as whole main amount
    else Source differs from main currency
        UI-->>User: Require manually supplied whole main-currency value
        User->>UI: Enter main amount before approval
    end
    opt Other enabled-currency valuations supplied
        User->>UI: Enter each whole valuation manually
    end
    User->>UI: Keep one entry or define split entries
    loop Each entry
        User->>UI: Assign subcategory, description/comment, multiple tags, and portions in every supplied currency
    end
    alt User leaves without approval
        UI-->>User: Source remains pending
    else Submit approval
        UI->>App: Approve whole operation with all entries and declared valuations
        App->>App: Validate main/source completeness, Income positivity, same-profile selections
        App->>App: Reconcile source total and every entered currency independently
        alt Validation fails
            App-->>UI: Missing value or inconsistent distribution
            UI-->>User: Correct before approval
        else Whole operation saves
            App->>App: Save Transaction and entries together, mark source reviewed
            App->>App: Update main-currency tracking, balances, and reports
            App-->>UI: All entries approved
            UI-->>User: Show confirmation
        else Save fails
            App-->>UI: Source pending, no partial approved effects
            UI-->>User: Show failure and continue review
        end
    end
    Note over UI,App: Shared date/payment/period, independent entry tags, no FX or proportional conversion
```
