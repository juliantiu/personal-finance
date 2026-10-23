# UC-016 — Review savings, debt, and unpaid IOUs — Flow

[Written specification (UC-016)](../use-cases.md#uc-016)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** This report is read-only and combines profile history by item identity, including renames. Inclusion of removed items in current balance lists, empty-total presentation, and currency basis are unresolved. IOU unpaid amounts depend on saved repayment links and eventual attribution rules.

```mermaid
graph TD
    A["Open profile savings, debt, and IOU reports"] --> B["Load profile records across periods"]
    B --> C{"Loading and calculation succeed?"}
    C -->|No| FAIL["Show failure; results not represented as complete"]
    FAIL --> A
    C -->|Yes| D{"Relevant records exist?"}
    D -->|No| E["Indicate no relevant records"]
    D -->|Yes| F["Combine same items across periods and renames"]
    F --> G["Show savings amounts, goals, and total"]
    G --> H["Show debt paid, remaining, and remaining total"]
    H --> I["Show IOUs with unpaid amounts after linked repayments"]
    I --> DONE["Review financial position without changing records"]
    E --> DONE
    NOTE["Currency basis and removed-item inclusion undefined"] -.-> F
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
