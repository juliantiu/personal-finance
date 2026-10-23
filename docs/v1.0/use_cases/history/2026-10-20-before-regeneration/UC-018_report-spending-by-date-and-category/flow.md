# UC-018 — Report spending by date and category — Flow

[Written specification (UC-018)](../use-cases.md#uc-018)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Spending classification/netting, currency basis, historical filter presentation, date boundaries, and time zone are unresolved. All-date reporting and selected-date reporting use transaction dates across periods and preserve item identity across renames. The report does not change source records.

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
    NOTE["Currency, netting, and removed-item filter rules undefined"] -.-> H
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
