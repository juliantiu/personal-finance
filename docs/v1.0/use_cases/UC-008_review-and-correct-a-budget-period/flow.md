# UC-008 — Review and correct a budget period — Flow

[Written specification (UC-008)](../use-cases.md#uc-008) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Expected Income equals allocation, and main-currency amounts alone feed totals. Period omission is not deletion; saved period configuration is independent. Referenced selections must not be orphaned. Spending/allocation inclusion A9 and date policy A10 remain proposed; financial corrections propagate to later balances.

```mermaid
graph TD
    A["Choose current or previous period"] --> B["Load dates, items, income, and activity"]
    B --> C["Show allocations, income, spending, remaining amounts"]
    C --> D{"Correct period setup?"}
    D -->|No| DONE["Review selected period"]
    D -->|Yes| E{"Writer permission?"}
    E -->|No| F["View only; changes unavailable"]
    F --> DONE
    E -->|Yes| G["Adjust selected period only; omission is not deletion"]
    G --> H["Save correction and refresh category sums"]
    H --> I{"Save succeeds?"}
    I -->|Yes| C
    I -->|No| FAIL["Show failure; correction not confirmed"]
    FAIL --> G
    DONE --> J["Optional financial correction via UC-009, UC-014, UC-015"]
    NOTE["Expected Income equals allocation; main currency only; policy A9 remains proposed"] -.-> C
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
