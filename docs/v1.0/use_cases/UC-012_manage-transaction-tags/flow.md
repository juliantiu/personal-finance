# UC-012 — Manage transaction tags — Flow

[Written specification (UC-012)](../use-cases.md#uc-012) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Multiple tags belong to individual entries. Custom tag management is separate from entry/review. Profile scope, used-tag cleanup and default-role protection are proposed A5; active IOU link consistency must be resolved, and no new default-tag deletion workflow is inferred.

```mermaid
graph TD
    A["Open separate tag management"] --> B["Show IOU, IOU REPAYMENT, and custom tags"]
    B --> C{"Custom-tag operation?"}
    C -->|Create or edit| D["Enter custom tag details"]
    C -->|Delete| E["Request custom tag deletion"]
    D --> F["Apply custom tag change"]
    E --> F
    F --> G{"Operation succeeds?"}
    G -->|Yes| DONE["Show updated custom tags"]
    G -->|No| FAIL["Show issue; change not confirmed"]
    FAIL --> C
    DONE --> H["Apply multiple tags in UC-009 or UC-011"]
    NOTE["Default mutability and used-tag deletion effects undefined"] -.-> C
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
