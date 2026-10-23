# UC-001 — Create a user account — Flow

[Written specification (UC-001)](../use-cases.md#uc-001) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Account identity is independent of email, first name and last name (D14). Requiredness, uniqueness, verification and authentication remain undecided; no credential workflow is invented.

```mermaid
graph TD
    A["Start account creation"] --> B["Provide email, first name, and last name"]
    B --> C["Submit creation request"]
    C --> D{"Creation succeeds?"}
    D -->|Yes| DONE["Account available for profile access"]
    D -->|No| FAIL["Show failure; creation not confirmed"]
    FAIL --> B
    NOTE["Requiredness, email uniqueness, verification, and sign-in undefined"] -.-> B
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
