# UC-011 — Approve or split a pending transaction — Flow

[Written specification (UC-011)](../use-cases.md#uc-011) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** D3/D7 require all split entries to share one period/date/payment and approve together. D10 requires independent complete reconciliation in every supplied currency; D11 requires main currency before foreign approval. All supplied Income amounts are positive. Precision and proposed source-currency enablement A13 remain open; failed approval has no partial financial effects.

```mermaid
graph TD
    A["Review pending source facts and amount/currency"] --> B["Choose one period, date/time, and payment method"]
    B --> C{"Source equals profile main currency?"}
    C -->|Yes| D["Use source main amount"]
    C -->|No| E["User supplies required whole main-currency amount"]
    D --> F["Optionally enter other enabled-currency totals manually"]
    E --> F
    F --> G["Choose single entry or linked split entries"]
    G --> H["Assign subcategories, independent comments/tags; distribute every supplied currency"]
    H --> I{"Complete values, reconciled currency totals, positive Income values?"}
    I -->|No| R["Correct distributions or required values; no conversion"]
    R --> H
    I -->|Yes| J["Approve whole operation together"]
    J --> K{"Approval succeeds?"}
    K -->|Yes| DONE["Source reviewed; main amounts affect financial tracking"]
    K -->|No| FAIL["Keep pending; no partial approved effects"]
    FAIL --> B
    A -->|Leave without approval| P["Keep source pending"]
    NOTE["Unknown currency, precision, and source enablement A13 remain open"] -.-> C
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
