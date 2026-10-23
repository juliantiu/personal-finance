# UC-008 — Review and correct a budget period — Sequence

[Written specification (UC-008)](../use-cases.md#uc-008)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Expected-income entry, deselection of items with history, propagation of prior-period savings/debt corrections, and currency basis for totals remain unspecified. Other financial corrections are separate use cases, not new actions added here.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Select current or previous period
    UI->>App: Request setup, activity, and period totals
    App-->>UI: Expected and received income, allocations, spending, remaining amounts
    UI-->>User: Show selected period
    Note over UI,App: Currency basis and expected-income entry unspecified
    opt Authorized writer corrects setup
        User->>UI: Change selections or allocations
        UI->>App: Save period correction
        alt Save succeeds
            App->>App: Refresh category sums and affected summaries
            App-->>UI: Corrected period
            UI-->>User: Show revised totals
        else Save fails
            App-->>UI: Correction not confirmed
            UI-->>User: Show failure
        end
    end
    Note over User,App: Viewers read only; other corrections use UC-009, UC-014, UC-015
    Note over UI,App: Historical correction propagation remains unspecified
```
