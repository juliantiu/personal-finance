# UC-007 — Create a later budget period — Flow

[Written specification (UC-007)](../use-cases.md#uc-007) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Previous periods follow date order (D6). Exclude explicitly deleted parent/child items; active omitted items can be selected again. Savings/debt carry is derived from movements and dated deltas; prior corrections affect later balances. Equal/overlapping-date predecessor and boundary policies remain open.

```mermaid
graph TD
    A["Start later period; enter dates"] --> B["Find previous period by date order"]
    B --> C["Copy available references and allocation amounts"]
    C --> D["Start spending at zero; derive carry from movements and dated deltas"]
    D --> E["Adjust new selections and allocations"]
    E --> F["Recalculate category sums"]
    F --> G["Save new period"]
    G --> H{"Creation succeeds?"}
    H -->|Yes| DONE["New period ready; earlier periods unchanged"]
    H -->|No| FAIL["Show issue; creation not confirmed"]
    FAIL --> E
    NOTE["Equal-date or overlapping predecessor choice and boundaries remain open"] -.-> B
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
