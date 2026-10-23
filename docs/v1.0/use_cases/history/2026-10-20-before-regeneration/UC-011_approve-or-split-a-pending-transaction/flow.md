# UC-011 — Approve or split a pending transaction — Flow

[Written specification (UC-011)](../use-cases.md#uc-011)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** A GBP transaction offers optional USD; a USD transaction offers optional GBP. The paired field may be blank. Split total preservation is an approved specification assumption, not a confirmed business rule; dual-currency distribution, split atomicity, and other currencies remain unspecified. Approval period differs from transaction date.

```mermaid
graph TD
    A["Open pending transaction and imported details"] --> B["Select approval period and subcategory"]
    B --> C{"Imported currency?"}
    C -->|USD| D["Offer optional GBP amount"]
    C -->|GBP| E["Offer optional USD amount"]
    C -->|Missing or other| U["Currency handling unspecified"]
    D --> F["Confirm card or Cash; optional paired amount, comments, tags"]
    E --> F
    F --> G{"Split transaction?"}
    G -->|Yes| H["Create linked entries with amounts, subcategories, comments, tags"]
    G -->|No| I["Submit single-entry approval"]
    H --> J["Submit linked-entry approval"]
    I --> K{"Income entries positive?"}
    J --> K
    K -->|No| R["Require positive Income amounts"]
    R --> F
    K -->|Yes or no Income entries| L["Save approved entries in selected period"]
    L --> M{"Approval succeeds?"}
    M -->|Yes| DONE["Resolve pending status and reflect approved activity"]
    M -->|No| FAIL["Show failure; review remains unresolved"]
    FAIL --> F
    A -->|Leave without approval| P["Keep pending"]
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class U assumption;
```
