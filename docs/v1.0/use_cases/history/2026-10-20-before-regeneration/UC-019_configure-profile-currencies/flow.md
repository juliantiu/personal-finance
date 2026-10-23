# UC-019 — Configure profile currencies — Flow

[Written specification (UC-019)](../use-cases.md#uc-019) · [Currency clarifications](../use-cases.md#current-currency-rules-and-precedence)

Conceptual diagram for approved proposal version **v1.0**. Settings and screen labels are illustrative; no implementation or HTTP contract is implied.

**Scope and open rules:** the owner and editors may save profile currency settings; viewers may read them. GBP and USD suffice initially, with no specified initial main-currency default. Changing the main currency after financial activity exists, disabling an already-used currency, and managing further supported currencies require rules that the proposal does not define. The diagram does not invent an allow/deny decision for those unresolved operations. Dashed arrows show how other use cases consume saved settings, rather than additional steps needed to complete configuration.

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
    class NOTE,N2 assumption;
```
