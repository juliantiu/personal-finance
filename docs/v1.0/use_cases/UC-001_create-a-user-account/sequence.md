# UC-001 — Create a user account — Sequence

[Written specification (UC-001)](../use-cases.md#uc-001) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Account identity is independent of email, first name and last name (D14). Requiredness, uniqueness, verification and authentication remain undecided; no credential workflow is invented.

```mermaid
sequenceDiagram
    actor User as Prospective user
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Start account creation
    UI-->>User: Request email, first name, and last name
    Note over UI,App: Fields are defined, requiredness and email uniqueness remain open
    User->>UI: Supply information and submit
    UI->>App: Request account creation
    alt Creation succeeds
        App-->>UI: Stable account identity created, supplied personal facts retained
        UI-->>User: Confirm account available
    else Creation fails
        App-->>UI: Creation failure
        UI-->>User: Show failure without confirming creation
    end
```
