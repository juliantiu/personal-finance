# UC-017 — Report income for a date range — Sequence

[Written specification (UC-017)](../use-cases.md#uc-017) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Reports use main amounts and transaction dates across approval periods. Stable item identity combines rename/deletion history; pending imports and alternatives contribute nothing. Boundary inclusivity and time zone remain proposed/open A10.

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
        Note over UI,App: Main only, alternatives excluded, date boundary and time-zone policy A10 open
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
