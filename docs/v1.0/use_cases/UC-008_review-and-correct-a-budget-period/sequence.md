# UC-008 — Review and correct a budget period — Sequence

[Written specification (UC-008)](../use-cases.md#uc-008) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Expected Income equals allocation, and main-currency amounts alone feed totals. Period omission is not deletion; saved period configuration is independent. Referenced selections must not be orphaned. Spending/allocation inclusion A9 and date policy A10 remain proposed; financial corrections propagate to later balances.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Select current or previous period
    UI->>App: Request setup, activity, and period totals
    App-->>UI: Expected and received income, allocations, spending, remaining amounts
    UI-->>User: Show selected period
    Note over UI,App: Income allocation is expected income, main only, alternatives excluded
    opt Authorized writer corrects setup
        User->>UI: Change selections or allocations
        UI->>App: Save period correction without orphaning referenced selections
        alt Save succeeds
            App->>App: Refresh category sums and affected summaries
            App-->>UI: Corrected period
            UI-->>User: Show revised totals
        else Save fails
            App-->>UI: Correction not confirmed
            UI-->>User: Show failure
        end
    end
    Note over User,App: Viewers read only, other corrections use UC-009, UC-014, UC-015
    Note over UI,App: Historical corrections propagate to later balances, including after a reset
```
