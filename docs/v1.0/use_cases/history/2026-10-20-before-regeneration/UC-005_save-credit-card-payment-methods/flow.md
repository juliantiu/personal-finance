# UC-005 — Save credit card payment methods — Flow

[Written specification (UC-005)](../use-cases.md#uc-005)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Saved cards are identified by their numbers and belong to the profile. Card-number validation, duplicate handling, and card editing/removal are unspecified. Cash is always a transaction choice and is not selected by default; saving a card does not choose a default.

```mermaid
graph TD
    A["Open profile payment methods"] --> B["Enter credit card number"]
    B --> C["Save card in profile"]
    C --> D{"Save succeeds?"}
    D -->|No| FAIL["Show issue; card not confirmed as saved"]
    FAIL --> B
    D -->|Yes| DONE["Card available across profile periods"]
    DONE --> E["Use for transaction selection or import matching"]
    NOTE["Duplicate rules and card editing or removal undefined"] -.-> C
    N2["Cash remains available without default selection"] -.-> E
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
    class N2 assumption;
```
