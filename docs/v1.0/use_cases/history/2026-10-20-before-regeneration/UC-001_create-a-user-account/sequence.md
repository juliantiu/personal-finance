# UC-001 — Create a user account — Sequence

[Written specification (UC-001)](../use-cases.md#uc-001)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Account fields, credential validation, verification, sign-in, and recovery are not defined. The form and creation request below are conceptual. Failure feedback follows the approved shared failure-handling assumption.

```mermaid
sequenceDiagram
    actor User as Prospective user
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Start account creation
    UI-->>User: Request account information
    Note over UI,App: Fields and validation rules are unspecified
    User->>UI: Supply information and submit
    UI->>App: Request account creation
    alt Creation succeeds
        App-->>UI: Account created
        UI-->>User: Confirm account available
    else Creation fails
        App-->>UI: Creation failure
        UI-->>User: Show failure without confirming creation
    end
```
