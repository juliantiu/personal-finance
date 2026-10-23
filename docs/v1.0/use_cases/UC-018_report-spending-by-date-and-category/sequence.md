# UC-018 — Report spending by date and category — Sequence

[Written specification (UC-018)](../use-cases.md#uc-018) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Main-currency reports use transaction dates and stable item identity across periods, renames and deletion. Alternative valuations/pending sources contribute nothing. Spending classification/netting A9, boundary policy A10, and historical filter presentation remain proposed/open.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open spending report
    User->>UI: Choose all dates or range, optionally choose category or subcategory
    UI->>App: Request spending with date and item filters
    alt Invalid selected date range
        App-->>UI: Date issue under eventual rules
        UI-->>User: Request corrected dates
    else All dates or accepted range
        App->>App: Select spending by transaction dates across periods
        App->>App: Combine same-item history before and after renames
        Note over UI,App: Main only, spending policy A9, dates A10, and historical filter presentation qualified
        alt Matching results
            App-->>UI: Filtered spending totals
            UI-->>User: Show totals
        else No matching activity
            App-->>UI: No matching activity
            UI-->>User: Show empty result
        else Loading or calculation fails
            App-->>UI: Report failure
            UI-->>User: Show failure without a complete result
        end
    end
    opt Change or clear filters
        User->>UI: Change date or item selections
        UI->>App: Recompute using revised filters
        App-->>UI: Updated result or issue
        UI-->>User: Show revised report
    end
    Note over User,App: No source records changed
```
