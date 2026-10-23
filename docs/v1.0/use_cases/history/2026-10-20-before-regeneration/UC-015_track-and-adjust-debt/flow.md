# UC-015 — Track and adjust debt — Flow

[Written specification (UC-015)](../use-cases.md#uc-015)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Debt activity, paid/remaining calculations, and manual overrides are conceptual operations. Starting debt entry, overpayment, currency denomination, override precedence, and propagation of earlier corrections are unspecified. Calculated amounts remain editable.

```mermaid
graph TD
    A["Open Debt subcategory and period"] --> B["Show amounts paid and remaining"]
    B --> C{"Make a correction?"}
    C -->|No or viewer| DONE["Review debt values"]
    C -->|Authorized writer| D["Record or adjust payment activity"]
    D --> E["Update paid and remaining amounts"]
    E --> F{"Edit calculated amounts?"}
    F -->|Yes| G["Enter manual correction"]
    F -->|No| H["Save debt changes"]
    G --> H
    H --> I{"Save succeeds?"}
    I -->|Yes| B
    I -->|No| FAIL["Show failure; change not confirmed"]
    FAIL --> D
    DONE --> J["Carry totals when UC-007 creates next period"]
    NOTE["Override precedence, overpayment, and historical propagation undefined"] -.-> E
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
