# UC-004 — Maintain categories and subcategories — Sequence

[Written specification (UC-004)](../use-cases.md#uc-004)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Renaming preserves item identity and all financial references. Removal here applies to subcategories only. Category deletion, automatic-category restrictions, restoration, and removed-item use in existing periods remain unspecified.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Review items and comments
    UI->>App: Request profile items and history
    App-->>UI: Items with permitted actions
    UI-->>User: Show categories and subcategories
    opt Authorized writer changes items
        User->>UI: Add, edit, rename, or remove subcategory
        UI->>App: Save profile-level change
        alt Change succeeds
            alt Rename
                App->>App: Update same item; retain all references and balances
            else Remove subcategory
                App->>App: Preserve history; stop future tracking
            else Add or edit
                App->>App: Save item and optional comment
            end
            App-->>UI: Saved result
            UI-->>User: Show result and preserved history
        else Save fails
            App-->>UI: Change not confirmed
            UI-->>User: Show failure
        end
    end
    Note over User,App: Viewers cannot save; category deletion rules unspecified
```
