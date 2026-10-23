# UC-002 — Create and access budget profiles — Flow

[Written specification (UC-002)](../use-cases.md#uc-002) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Profile setup includes its main currency, optional additional currencies, automatic protected categories, Cash and default tags. Partial-creation recovery and profile lifecycle beyond creation/access are unspecified.

```mermaid
graph TD
    A["Open accessible profiles"] --> B{"Any accessible profiles?"}
    B -->|No| C["Choose first profile creation"]
    B -->|Yes| D{"Create or open?"}
    D -->|Create| C
    C --> E["Configure currencies via UC-019; create profile with original owner"]
    E --> F{"Creation succeeds?"}
    F -->|Yes| G["Create protected system categories, Cash, IOU and IOU REPAYMENT"]
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
