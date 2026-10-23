# UC-010 — Upload a bank statement for review — Sequence

[Written specification (UC-010)](../use-cases.md#uc-010)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Statement formats, duplicate resolution, partial-import recovery, and missing/other currencies are unspecified. The unmatched-card branch describes an unresolved payment field, not a Cash fallback. Pending records are assumed not to affect totals until approval, as stated in the written specification.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Select profile and upload statement
    UI->>App: Import statement for pending review
    App->>App: Read IDs, descriptions, date/time, amounts, currencies, payment data
    alt Upload or reading fails
        App-->>UI: Import issue
        UI-->>User: Show failure; partial recovery unspecified
    else Reading succeeds
        loop Successfully imported transaction
            App->>App: Match imported card number to saved profile card
            alt Unique card match
                App->>App: Select matching payment method
            else No unique match
                Note over UI,App: Payment resolution unspecified; no automatic Cash fallback
            end
            App->>App: Place imported record in pending review
        end
        Note over UI,App: Duplicate IDs and missing or other currencies require defined rules
        App-->>UI: Pending records with source details
        UI-->>User: Show pending review list
    end
    Note over UI,App: Approval period is selected in UC-011; pending totals excluded by assumption
```
