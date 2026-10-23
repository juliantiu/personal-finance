# UC-019 — Configure profile currencies — Flow

[Written specification (UC-019)](../use-cases.md#uc-019) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** The main unit governs allocations, goals, corrections, summaries and reports; alternatives are manually entered without FX and excluded from totals (D11). Foreign approval requires main values and complete per-currency splits. Historical main changes, used-currency disabling, and source enablement are proposed A12/A13 policies, not newly confirmed workflows. No initial main default is defined.

```mermaid
graph TD
    A["Open currency settings during profile setup or later"] --> B["Load available currencies and existing settings"]
    B --> C{"Writer permission?"}
    C -->|No: view-only participant| V["Review settings without saving changes"]
    C -->|Yes: original owner or editor| D["Select profile main currency; initially GBP or USD"]
    D --> E["Choose optional additional enabled currencies, or none"]
    E --> F["Save currency configuration"]
    F --> G{"Save succeeds?"}
    G -->|No| FAIL["Show failure; requested change not confirmed"]
    FAIL --> D
    G -->|Yes| DONE["Review saved main currency and enabled alternatives"]
    DONE -.-> H["Allocations, period summaries, and profile reports use main currency"]
    DONE -.-> I["UC-009: enter main amount; optional alternatives entered manually"]
    DONE -.-> J["UC-011: foreign import requires user-supplied main-currency amount"]
    NOTE["Historical main-currency changes and used-currency disabling remain undefined"] -.-> D
    N2["No system conversion; alternative amounts excluded from summary and report totals"] -.-> I
    N2 -.-> J
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
    class N2 assumption;
```
