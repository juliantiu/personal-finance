# UC-002 — Create and access budget profiles — Sequence

[Written specification (UC-002)](../use-cases.md#uc-002)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Profile setup, profile selection, and creation boundaries are conceptual. Creation must include the automatic categories, but recovery from partial creation is unspecified. No deletion or ownership-transfer interaction is added.

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
            App->>App: Set original owner and create Savings, Debt, Income
            App-->>UI: Created profile
        else Creation fails
            App-->>UI: Failure; partial recovery unspecified
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
