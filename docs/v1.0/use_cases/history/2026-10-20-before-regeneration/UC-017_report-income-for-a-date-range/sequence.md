# UC-017 — Report income for a date range — Sequence

[Written specification (UC-017)](../use-cases.md#uc-017)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Date validation, boundary inclusivity, time zone, report currency, and missing paired amounts are unresolved. The report uses transaction dates across periods, not the dates of approval periods. Two currency values for one transaction do not represent two income events.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open income report and select dates
    UI->>App: Request income for transaction-date range
    alt Invalid range under eventual validation rules
        App-->>UI: Date issue
        UI-->>User: Request corrected dates
    else Accepted range
        App->>App: Select income by transaction dates across periods
        App->>App: Combine same-item history across renames
        Note over UI,App: Currency basis, paired values, date boundaries, and time zone unresolved
        alt Results available
            App-->>UI: Total income for selected dates
            UI-->>User: Show income total
        else No matching income
            App-->>UI: No matching activity
            UI-->>User: Show empty result
        else Load or calculation fails
            App-->>UI: Report failure
            UI-->>User: Show failure without a complete result
        end
    end
    opt User changes dates
        User->>UI: Select a different range
        UI->>App: Recompute report for new dates
        App-->>UI: Updated result or issue
        UI-->>User: Show new result
    end
    Note over User,App: No source records changed
```
