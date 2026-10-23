# UC-006 — Create the first budget period — Flow

[Written specification (UC-006)](../use-cases.md#uc-006)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Dates, item selections, and allocation entry are conceptual controls. Date validation, empty selections, overlapping periods, and allocation limits are not defined. Category allocations must sum the selected subcategory allocations.

```mermaid
graph TD
    A["Create first period in profile"] --> B["Enter start and end dates"]
    B --> C["Select profile categories and subcategories"]
    C --> D["Set each selected subcategory allocation"]
    D --> E["Show category sums"]
    E --> F{"Adjust selections or allocations?"}
    F -->|Yes| C
    F -->|No| G["Save period"]
    G --> H{"Creation succeeds?"}
    H -->|Yes| DONE["Period available for activity and review"]
    H -->|No| FAIL["Show save or input issue"]
    FAIL --> B
    NOTE["Exact date, selection, and allocation validation undefined"] -.-> G
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
