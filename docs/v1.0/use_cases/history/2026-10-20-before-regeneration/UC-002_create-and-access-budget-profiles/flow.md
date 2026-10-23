# UC-002 — Create and access budget profiles — Flow

[Written specification (UC-002)](../use-cases.md#uc-002)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Profile setup, profile selection, and creation boundaries are conceptual. Creation must include the automatic categories, but recovery from partial creation is unspecified. No deletion or ownership-transfer interaction is added.

```mermaid
graph TD
    A["Open accessible profiles"] --> B{"Any accessible profiles?"}
    B -->|No| C["Choose first profile creation"]
    B -->|Yes| D{"Create or open?"}
    D -->|Create| C
    C --> E["Create profile owned by current account"]
    E --> F{"Creation succeeds?"}
    F -->|Yes| G["Create Savings, Debt, and Income categories"]
    F -->|No| FAIL["Show creation failure"]
    G --> H["Open profile"]
    D -->|Open existing| H
    H --> I{"Access still permitted?"}
    I -->|Yes| DONE["Show profile with assigned role"]
    I -->|No| J["Show access denied"]
    DONE -->|Switch profile| A
    J --> A
    FAIL --> C
    NOTE["Partial-creation recovery undefined"] -.-> E
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
