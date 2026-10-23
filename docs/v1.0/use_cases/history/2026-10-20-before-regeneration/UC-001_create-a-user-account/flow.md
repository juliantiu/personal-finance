# UC-001 — Create a user account — Flow

[Written specification (UC-001)](../use-cases.md#uc-001)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Account fields, credential validation, verification, sign-in, and recovery are not defined. The form and creation request below are conceptual. Failure feedback follows the approved shared failure-handling assumption.

```mermaid
graph TD
    A["Start account creation"] --> B["Provide required account information"]
    B --> C["Submit creation request"]
    C --> D{"Creation succeeds?"}
    D -->|Yes| DONE["Account available for profile access"]
    D -->|No| FAIL["Show failure; creation not confirmed"]
    FAIL --> B
    NOTE["Account requirements and duplicate rules undefined"] -.-> B
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
