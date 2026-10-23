# UC-017 — Report income for a date range — Flow

[Written specification (UC-017)](../use-cases.md#uc-017)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Date validation, boundary inclusivity, time zone, report currency, and missing paired amounts are unresolved. The report uses transaction dates across periods, not the dates of approval periods. Two currency values for one transaction do not represent two income events.

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
    NOTE["Date boundaries, time zone, and currency basis undefined"] -.-> E
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
