# UC-017 — Report income for a date range — Flow

[Written specification (UC-017)](../use-cases.md#uc-017) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Reports use main amounts and transaction dates across approval periods. Stable item identity combines rename/deletion history; pending imports and alternatives contribute nothing. Boundary inclusivity and time zone remain proposed/open A10.

```mermaid
graph TD
    A["Open income report"] --> B["Select start and end dates"]
    B --> C{"Date range accepted?"}
    C -->|No| D["Explain date issue"]
    D --> B
    C -->|Yes| E["Load income by transaction dates across periods"]
    E --> F{"Loading and calculation succeed?"}
    F -->|No| FAIL["Show report failure"]
    FAIL --> B
    F -->|Yes| G{"Matching income exists?"}
    G -->|No| H["Indicate no matching activity"]
    G -->|Yes| I["Combine same-item history across renames"]
    I --> DONE["Show total income for selected dates"]
    H --> J{"Change dates?"}
    DONE --> J
    J -->|Yes| B
    J -->|No| K["Finish review"]
    NOTE["Main currency settled; date boundary and time-zone policy A10 open"] -.-> E
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
