# UC-014 — Track and adjust savings — Flow

[Written specification (UC-014)](../use-cases.md#uc-014) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Reset periods are informational notes, with no due calculation or automatic reset (D2). Positive/negative Savings entries add/withdraw funds. Opening amounts and manual targets create dated deltas T minus B; resetting creates minus B. Earlier corrections change later values even after reset (D1). Correction deletion, negative-balance policy, and date boundaries remain open.

```mermaid
graph TD
    A["Review balance, goal, reset note, and movements"] --> B{"Writer change?"}
    B -->|No or viewer| DONE["Review derived savings"]
    B -->|Yes| C{"Change type?"}
    C -->|Goal or note| D["Edit settings; reset note has no scheduling meaning"]
    C -->|Transaction movement| E["UC-009/011: positive adds, negative withdraws"]
    C -->|Opening or balance target| F["Choose effective date/time and desired target T"]
    C -->|Manual reset| G["Choose effective date/time and target zero"]
    F --> H["Calculate B from approved movements and earlier deltas"]
    G --> H
    H --> I["Record correction T minus B; reset is minus B"]
    D --> J["Save change"]
    E --> J
    I --> J
    J --> K{"Save succeeds?"}
    K -->|No| FAIL["Show failure; change not confirmed"]
    FAIL --> C
    K -->|Yes| L["Recompute later balances, including after resets"]
    L --> DONE
    NOTE["No fixed checkpoint; deletion/gaps preserve balance history"] -.-> L
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
