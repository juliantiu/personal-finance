# UC-016 — Review savings, debt, and unpaid IOUs — Flow

[Written specification (UC-016)](../use-cases.md#uc-016) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Main-currency reports combine stable item identity, approved signed movements, dated metric deltas, and valid repayment links. Explicit deletion or selection gaps do not hide balances/outstanding obligations. IOU credit A6 and time boundaries A10 remain qualified; report failure never implies complete results.

```mermaid
graph TD
    A["Open profile savings, debt, and IOU reports"] --> B["Load movement/correction history across periods, including deleted or omitted items"]
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
    NOTE["Main only; retain deleted obligations; IOU credit A6 and boundaries A10 proposed"] -.-> F
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
