# UC-008 — Review and correct a budget period — Flow

[Written specification (UC-008)](../use-cases.md#uc-008)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Expected-income entry, deselection of items with history, propagation of prior-period savings/debt corrections, and currency basis for totals remain unspecified. Other financial corrections are separate use cases, not new actions added here.

```mermaid
graph TD
    A["Choose current or previous period"] --> B["Load dates, items, income, and activity"]
    B --> C["Show allocations, income, spending, remaining amounts"]
    C --> D{"Correct period setup?"}
    D -->|No| DONE["Review selected period"]
    D -->|Yes| E{"Writer permission?"}
    E -->|No| F["View only; changes unavailable"]
    F --> DONE
    E -->|Yes| G["Adjust selections or allocations"]
    G --> H["Save correction and refresh category sums"]
    H --> I{"Save succeeds?"}
    I -->|Yes| C
    I -->|No| FAIL["Show failure; correction not confirmed"]
    FAIL --> G
    DONE --> J["Optional financial correction via UC-009, UC-014, UC-015"]
    NOTE["Expected-income entry, history effects, and currency basis undefined"] -.-> C
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
