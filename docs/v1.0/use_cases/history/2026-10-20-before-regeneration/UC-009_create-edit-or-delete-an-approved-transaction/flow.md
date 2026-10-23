# UC-009 — Create, edit, or delete an approved transaction — Flow

[Written specification (UC-009)](../use-cases.md#uc-009)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Manual entry defaults to GBP with an optional USD amount for the same transaction; no conversion is implied. Cash is available without a default selection. Paired-amount validation and linked split/IOU effects on edit or deletion remain unspecified. Existing transaction correction is included; splitting is modeled in UC-011.

```mermaid
graph TD
    A["Select current or previous period"] --> B{"Create, edit, or delete?"}
    B -->|Create| C["Enter transaction; GBP default, optional USD"]
    B -->|Edit| D["Open approved transaction and edit details"]
    B -->|Delete| E["Request transaction deletion"]
    C --> F["Choose saved card or Cash; optional comment and tags"]
    D --> F
    F --> G["Submit transaction"]
    G --> H{"Income amount positive, if Income?"}
    H -->|No| I["Require positive Income amount"]
    I --> F
    H -->|Yes or non-Income| J["Save; auto-approve manual creation in selected period"]
    E --> K["Apply deletion"]
    J --> L{"Operation succeeds?"}
    K --> L
    L -->|Yes| DONE["Show approved creation or corrected activity and totals"]
    L -->|No| FAIL["Show failure; change not confirmed"]
    FAIL --> B
    NOTE["Paired currency and linked-record effects undefined"] -.-> D
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
