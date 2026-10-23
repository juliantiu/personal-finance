# UC-007 — Create a later budget period — Flow

[Written specification (UC-007)](../use-cases.md#uc-007)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** “Previous period” ordering and propagation of earlier balance corrections are unresolved. Removed subcategories cannot resume future tracking; how they appear in a copied draft is unspecified. Creation copies references/allocations, carries savings/debt, and starts spending at zero without copying transactions.

```mermaid
graph TD
    A["Start later period; enter dates"] --> B["Use previous period as starting point"]
    B --> C["Copy item references and allocation amounts"]
    C --> D["Start spending at zero; carry savings and debt totals"]
    D --> E["Adjust new selections and allocations"]
    E --> F["Recalculate category sums"]
    F --> G["Save new period"]
    G --> H{"Creation succeeds?"}
    H -->|Yes| DONE["New period ready; earlier periods unchanged"]
    H -->|No| FAIL["Show issue; creation not confirmed"]
    FAIL --> E
    NOTE["Predecessor ordering and correction propagation undefined"] -.-> B
    N2["Removed subcategories must not resume tracking"] -.-> E
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
    class N2 assumption;
```
