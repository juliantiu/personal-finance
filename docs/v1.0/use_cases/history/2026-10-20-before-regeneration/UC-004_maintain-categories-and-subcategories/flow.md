# UC-004 — Maintain categories and subcategories — Flow

[Written specification (UC-004)](../use-cases.md#uc-004)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Renaming preserves item identity and all financial references. Removal here applies to subcategories only. Category deletion, automatic-category restrictions, restoration, and removed-item use in existing periods remain unspecified.

```mermaid
graph TD
    A["Review categories, subcategories, comments"] --> B{"Writer permission?"}
    B -->|No| C["View items and history only"]
    B -->|Yes| D{"Requested change?"}
    D -->|Add or edit| E["Enter item details and optional comment"]
    D -->|Rename| F["Change name of the same profile item"]
    D -->|Remove subcategory| G["Stop future tracking; retain history"]
    E --> H["Save profile item change"]
    F --> H
    G --> H
    H --> I{"Save succeeds?"}
    I -->|No| FAIL["Show failure; change not confirmed"]
    FAIL --> D
    I -->|Yes| DONE["Show saved item or preserved historical item"]
    DONE --> A
    NOTE["Keep references, allocations, transactions, and balances on rename"] -.-> F
    N2["Category deletion and removal effects in existing periods undefined"] -.-> G
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
    class N2 assumption;
```
