# UC-009 — Create, edit, or delete an approved transaction — Flow

[Written specification (UC-009)](../use-cases.md#uc-009) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Main-currency manual creation is approved immediately; alternatives are manually entered and excluded from totals. Income supplied values must be positive. Shared split facts and each retained currency total must stay consistent through edits/deletions; dependent links must be resolved. Schema A8 import-source retention is proposed, not an established requeue workflow.

```mermaid
graph TD
    A["Select current or previous period"] --> B{"Create, edit, or delete?"}
    B -->|Create| C["Enter main-currency transaction; optional enabled alternatives"]
    B -->|Edit| D["Open approved transaction and edit details"]
    B -->|Delete| E["Request transaction deletion"]
    C --> F["Choose saved card or Cash; optional comment and tags"]
    D --> F
    F --> G["Submit transaction"]
    G --> H{"All supplied Income values positive, if Income?"}
    H -->|No| I["Require positive Income amount"]
    I --> F
    H -->|Yes or non-Income| J["Save; auto-approve manual creation in selected period"]
    E --> K["Apply deletion"]
    J --> L{"Operation succeeds?"}
    K --> L
    L -->|Yes| DONE["Show approved creation or corrected activity and totals"]
    L -->|No| FAIL["Show failure; change not confirmed"]
    FAIL --> B
    NOTE["Reconcile split edits; resolve dependent links; reviewed-source policy A8 proposed"] -.-> D
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
