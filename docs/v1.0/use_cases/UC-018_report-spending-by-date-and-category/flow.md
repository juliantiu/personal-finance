# UC-018 — Report spending by date and category — Flow

[Written specification (UC-018)](../use-cases.md#uc-018) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Main-currency reports use transaction dates and stable item identity across periods, renames and deletion. Alternative valuations/pending sources contribute nothing. Spending classification/netting A9, boundary policy A10, and historical filter presentation remain proposed/open.

```mermaid
graph TD
    A["Open spending report"] --> B{"All dates or selected range?"}
    B -->|All transaction dates| C["Use all dates"]
    B -->|Selected range| D["Enter start and end dates"]
    D --> E{"Date range accepted?"}
    E -->|No| F["Explain date issue"]
    F --> D
    E -->|Yes| G["Optionally filter by category or subcategory"]
    C --> G
    G --> H["Load matching spending by transaction date"]
    H --> I{"Loading and calculation succeed?"}
    I -->|No| FAIL["Show report failure"]
    FAIL --> G
    I -->|Yes| J{"Matching activity exists?"}
    J -->|No| K["Indicate no matching activity"]
    J -->|Yes| L["Combine same items across periods and renames"]
    L --> DONE["Show filtered spending totals"]
    K --> M["Change or clear date and item filters"]
    DONE --> M
    M --> B
    NOTE["Main currency settled; spending policy A9 and date policy A10 proposed"] -.-> H
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
