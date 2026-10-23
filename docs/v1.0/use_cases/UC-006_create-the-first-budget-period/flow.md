# UC-006 — Create the first budget period — Flow

[Written specification (UC-006)](../use-cases.md#uc-006) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Allocations use the profile main currency; Income allocation is expected Income (D5). Deleted items and children of deleted categories cannot be newly selected. Omission is not deletion. Date validation, nonnegative allocation policy A11, and empty-selection rules remain qualified.

```mermaid
graph TD
    A["Create first period in profile"] --> B["Enter start and end dates"]
    B --> C["Select available profile categories and subcategories"]
    C --> D["Set main-currency allocations; Income allocation is expected income"]
    D --> E["Show category sums"]
    E --> F{"Adjust selections or allocations?"}
    F -->|Yes| C
    F -->|No| G["Save period"]
    G --> H{"Creation succeeds?"}
    H -->|Yes| DONE["Period available for activity and review"]
    H -->|No| FAIL["Show save or input issue"]
    FAIL --> B
    NOTE["Deleted items unavailable; omission permits later reselection; validation policies open"] -.-> G
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
