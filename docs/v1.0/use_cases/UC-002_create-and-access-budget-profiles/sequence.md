# UC-002 — Create and access budget profiles — Sequence

[Written specification (UC-002)](../use-cases.md#uc-002) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Profile setup includes its main currency, optional additional currencies, automatic protected categories, Cash and default tags. Partial-creation recovery and profile lifecycle beyond creation/access are unspecified.

```mermaid
sequenceDiagram
    actor User as Account holder
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open profiles
    UI->>App: Request accessible profiles
    App-->>UI: Profile list or no profiles
    UI-->>User: Show available choices
    alt Create profile
        User->>UI: Create profile
        UI->>App: Request profile creation
        alt Creation succeeds
            App->>App: Set original owner and currencies, create protected categories, Cash and default tags
            App-->>UI: Created profile
        else Creation fails
            App-->>UI: Failure, partial recovery unspecified
            UI-->>User: Show creation failure
        end
    else Open existing profile
        User->>UI: Select profile
        UI->>App: Request profile with current access
        alt Access permitted
            App-->>UI: Profile and assigned role
        else Access removed
            App-->>UI: Access denied
        end
    end
    UI-->>User: Show result
    opt Switch profiles
        User->>UI: Return to accessible profiles
    end
```
