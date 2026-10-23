# UC-005 — Save credit card payment methods — Flow

[Written specification (UC-005)](../use-cases.md#uc-005) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Cards are profile payment methods and Cash has no default selection. Canonical card-number uniqueness is proposed A3; edit/removal, formatting, and duplicate handling are not confirmed user workflows.

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
