# UC-013 — Link repayments to an IOU — Flow

[Written specification (UC-013)](../use-cases.md#uc-013) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** One repayment entry has one IOU target (D4), while an IOU may have many repayments from later periods. Both use their required main amounts; source currencies do not affect the unit. A6 full absolute credit, direction, partial attribution and overpayment remain proposed/open; tagging alone does not link.

```mermaid
graph TD
    A["Identify approved IOU tagged IOU"] --> B{"Approved repayment available?"}
    B -->|No| C["Record or approve repayment via UC-009 or UC-011"]
    C --> B
    B -->|Yes| D["Select repayment tagged IOU REPAYMENT"]
    D --> E{"Same profile and repayment has no other IOU target?"}
    E -->|No| F["Reject different profile or conflicting repayment target"]
    F --> D
    E -->|Yes| G["Link repayment to IOU"]
    G --> H{"Link save succeeds?"}
    H -->|No| FAIL["Show failure; link not confirmed"]
    FAIL --> D
    H -->|Yes| DONE["Reflect linked repayment in unpaid amount"]
    DONE --> I{"More repayments?"}
    I -->|Yes, including later periods| D
    I -->|No| J["Review unpaid amount via UC-016"]
    NOTE["Main currency is settled; A6 attribution/overpayment and unlinking policy open"] -.-> G
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
