# UC-013 — Link repayments to an IOU — Flow

[Written specification (UC-013)](../use-cases.md#uc-013)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Repayments must belong to the same profile as the IOU and may come from later periods. Attribution, partial allocation, overpayment, unlinking, record-deletion effects, and cross-currency calculation are unspecified. Linking is explicit and is not inferred from tags.

```mermaid
graph TD
    A["Identify approved IOU tagged IOU"] --> B{"Approved repayment available?"}
    B -->|No| C["Record or approve repayment via UC-009 or UC-011"]
    C --> B
    B -->|Yes| D["Select repayment tagged IOU REPAYMENT"]
    D --> E{"Same profile as IOU?"}
    E -->|No| F["Does not meet same-profile requirement"]
    F --> D
    E -->|Yes| G["Link repayment to IOU"]
    G --> H{"Link save succeeds?"}
    H -->|No| FAIL["Show failure; link not confirmed"]
    FAIL --> D
    H -->|Yes| DONE["Reflect linked repayment in unpaid amount"]
    DONE --> I{"More repayments?"}
    I -->|Yes, including later periods| D
    I -->|No| J["Review unpaid amount via UC-016"]
    NOTE["Attribution, overpayment, unlinking, and currency rules undefined"] -.-> G
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
