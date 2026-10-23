# UC-018 — Report spending by date and category — Sequence

[Written specification (UC-018)](../use-cases.md#uc-018)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Spending classification/netting, currency basis, historical filter presentation, date boundaries, and time zone are unresolved. All-date reporting and selected-date reporting use transaction dates across periods and preserve item identity across renames. The report does not change source records.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open spending report
    User->>UI: Choose all dates or range; optionally choose category or subcategory
    UI->>App: Request spending with date and item filters
    alt Invalid selected date range
        App-->>UI: Date issue under eventual rules
        UI-->>User: Request corrected dates
    else All dates or accepted range
        App->>App: Select spending by transaction dates across periods
        App->>App: Combine same-item history before and after renames
        Note over UI,App: Currency, negative-value netting, classification, and removed-item filters unresolved
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
