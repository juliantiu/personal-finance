# UC-010 — Upload a bank statement for review — Flow

[Written specification (UC-010)](../use-cases.md#uc-010) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Source facts are retained separately from approved entries; pending records contribute nothing. Card matching stays within the profile and never defaults unmatched records to Cash. Formats, external-ID deduplication, unsupported currencies, and partial-import recovery remain open.

```mermaid
graph TD
    A["Select profile and upload statement"] --> B["Read transaction IDs, descriptions, dates, amounts, currencies"]
    B --> C{"Upload and reading succeed?"}
    C -->|No| FAIL["Show issue; partial recovery undefined"]
    FAIL --> A
    C -->|Yes| D["Match imported card number to saved profile cards"]
    D --> E{"Unique matching card?"}
    E -->|Yes| F["Automatically select matching payment method"]
    E -->|No| G["Payment match unresolved; no automatic Cash fallback"]
    F --> H["Show successfully imported records pending review"]
    G --> H
    H --> DONE["Open review list; approval period chosen in UC-011"]
    NOTE["Duplicate IDs and missing or unsupported currencies need defined rules"] -.-> B
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
