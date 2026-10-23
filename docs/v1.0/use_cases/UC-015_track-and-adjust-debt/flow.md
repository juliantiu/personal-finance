# UC-015 — Track and adjust debt — Flow

[Written specification (UC-015)](../use-cases.md#uc-015) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Positive Debt payments increase paid/decrease remaining; negative payments reverse both (D6). Plans do not move debt. Opening/manual edits create dated metric deltas T minus B, with paid/remaining corrected independently (D1). Prior changes propagate. Correction deletion, negative balances, precision and boundaries remain open.

```mermaid
graph TD
    A["Review paid and remaining debt"] --> B{"Writer correction?"}
    B -->|No or viewer| DONE["Review derived debt position"]
    B -->|Yes| C{"Payment or metric target?"}
    C -->|Approved payment via UC-009/011| D["Positive: paid up, remaining down; negative reverses both"]
    C -->|Opening or manual metric edit| E["Choose paid or remaining, effective time, and target T"]
    E --> F["Calculate selected metric B; correction is T minus B"]
    F --> G["Use separate deltas when changing both metrics"]
    D --> H["Save financial change"]
    G --> H
    H --> I{"Save succeeds?"}
    I -->|Yes| J["Recompute subsequent paid and remaining values"]
    I -->|No| FAIL["Show failure; change not confirmed"]
    FAIL --> C
    J --> DONE
    NOTE["Allocations are plans and never change debt balances"] -.-> A
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
