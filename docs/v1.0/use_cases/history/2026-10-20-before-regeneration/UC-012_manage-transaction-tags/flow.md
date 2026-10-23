# UC-012 — Manage transaction tags — Flow

[Written specification (UC-012)](../use-cases.md#uc-012)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Tag management is separate from transaction entry/review. Only custom-tag creation, editing, and deletion are defined. Default-tag mutability, duplicate names, cross-profile scope, and deletion effects on used tags remain unspecified. Tags alone do not create repayment links.

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
