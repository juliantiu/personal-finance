# UC-004 — Maintain categories and subcategories — Flow

[Written specification (UC-004)](../use-cases.md#uc-004) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Ordinary-category and any-subcategory deletion stop new tracking without erasing history (D12). Parent deletion does not overwrite independent child deletion; protected Income/Savings/Debt categories cannot be deleted (D13). Omission is temporary selection absence. Restoration is undefined.

```mermaid
graph TD
    A["Review profile items, comments, and history"] --> B{"Writer permission?"}
    B -->|No| V["Read only"]
    B -->|Yes| C{"Operation?"}
    C -->|Add or edit| D["Edit details or optional comment"]
    C -->|Rename| E["Rename same identity; preserve references and balances"]
    C -->|Explicit deletion| F{"Protected system category?"}
    F -->|Yes| R["Reject category deletion; children may be deleted"]
    F -->|No: ordinary category or any subcategory| G["Stop new tracking; retain item and history"]
    G --> H["Parent deletion blocks children without changing their own deletion status"]
    D --> I["Save item change"]
    E --> I
    H --> I
    I --> J{"Save succeeds?"}
    J -->|No| FAIL["Show failure; change not confirmed"]
    FAIL --> C
    J -->|Yes| DONE["Show result; preserve historical corrections, balances, obligations"]
    R --> C
    NOTE["Period omission does not delete; active items can be selected again"] -.-> A
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
