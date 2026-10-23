# UC-010 — Upload a bank statement for review — Sequence

[Written specification (UC-010)](../use-cases.md#uc-010) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Source facts are retained separately from approved entries; pending records contribute nothing. Card matching stays within the profile and never defaults unmatched records to Cash. Formats, external-ID deduplication, unsupported currencies, and partial-import recovery remain open.

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
        UI-->>User: Show failure, partial recovery unspecified
    else Reading succeeds
        loop Successfully imported transaction
            App->>App: Match imported card number to saved profile card
            alt Unique card match
                App->>App: Select matching payment method
            else No unique match
                Note over UI,App: Payment resolution unspecified, no automatic Cash fallback
            end
            App->>App: Place imported record in pending review
        end
        Note over UI,App: Duplicate IDs and missing or other currencies require defined rules
        App-->>UI: Pending records with source details
        UI-->>User: Show pending review list
    end
    Note over UI,App: Approval period is selected in UC-011, pending totals excluded by assumption
```
