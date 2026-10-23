# UC-014 — Track and adjust savings — Flow

[Written specification (UC-014)](../use-cases.md#uc-014)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Savings additions/withdrawals are conceptual recorded activity; whether they are transactions or separate adjustments is unspecified. Reset format, manual reset behavior, negative balances, currency denomination, and propagation of historical corrections are unresolved. A due reset never automatically resets the balance.

```mermaid
graph TD
    A["Open Savings subcategory and selected period"] --> B["Show balance, goal, reset period, additions, withdrawals"]
    B --> C{"Reset period due?"}
    C -->|Yes| D["Show due indication; do not reset balance"]
    C -->|No| E{"Edit values or activity?"}
    D --> E
    E -->|No or viewer| DONE["Review savings values"]
    E -->|Authorized writer| F["Edit goal, reset period, balance, additions, or withdrawals"]
    F --> G["Save changes and reflect savings tracking"]
    G --> H{"Save succeeds?"}
    H -->|Yes| B
    H -->|No| FAIL["Show failure; change not confirmed"]
    FAIL --> F
    DONE --> I["Carry balance when UC-007 creates next period"]
    NOTE["Manual reset and earlier-correction propagation undefined"] -.-> F
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
