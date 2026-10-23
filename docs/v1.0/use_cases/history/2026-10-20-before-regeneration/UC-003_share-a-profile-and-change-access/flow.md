# UC-003 — Share a profile and change access — Flow

[Written specification (UC-003)](../use-cases.md#uc-003)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Only the original owner can manage sharing. Recipient identification and invitation/acceptance mechanisms are unspecified. Saving role changes and removing access are shown without an assumed delivery channel.

```mermaid
graph TD
    A["Open sharing controls"] --> B{"Original owner?"}
    B -->|No| C["Deny sharing and access changes"]
    B -->|Yes| D["Identify recipient or existing participant"]
    D --> E{"Recipient identified?"}
    E -->|No| F["Access not granted; resolve recipient"]
    F --> D
    E -->|Yes| G["Choose editor, view-only, or remove access"]
    G --> H["Save access change"]
    H --> I{"Save succeeds?"}
    I -->|No| FAIL["Show failure; change not confirmed"]
    FAIL --> G
    I -->|Yes| DONE["Access reflects owner's change"]
    DONE --> J["Recipient uses permitted profile actions"]
    NOTE["Invitation and acceptance mechanism undefined"] -.-> D
    classDef default fill:#eff6ff,stroke:#31567a,color:#152536;
    classDef outcome fill:#e8f5e9,stroke:#2e7d32,color:#173c1b;
    classDef failure fill:#fef2f2,stroke:#b91c1c,color:#7f1d1d;
    classDef assumption fill:#fff7ed,stroke:#b45309,color:#7c2d12,stroke-dasharray:4 3;
    class DONE outcome;
    class FAIL failure;
    class NOTE assumption;
```
