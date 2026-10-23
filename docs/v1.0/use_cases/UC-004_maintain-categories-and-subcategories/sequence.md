# UC-004 — Maintain categories and subcategories — Sequence

[Written specification (UC-004)](../use-cases.md#uc-004) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Ordinary-category and any-subcategory deletion stop new tracking without erasing history (D12). Parent deletion does not overwrite independent child deletion; protected Income/Savings/Debt categories cannot be deleted (D13). Omission is temporary selection absence. Restoration is undefined.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Review items, comments, and history
    UI->>App: Request profile items and permitted actions
    App-->>UI: Current and retained items
    UI-->>User: Show items
    opt Authorized writer changes items
        User->>UI: Add, edit, rename, or explicitly delete
        UI->>App: Save requested item operation
        alt Delete protected Income, Savings, or Debt category
            App-->>UI: Reject deletion
            UI-->>User: Show protected category result
        else Permitted operation succeeds
            alt Rename
                App->>App: Update same item, preserve all references and balances
            else Explicit deletion
                App->>App: Stop new tracking and copying, retain history
                App->>App: Parent blocks children without changing child-specific deletion
            else Add or edit
                App->>App: Save details and optional comment
            end
            App-->>UI: Saved result with historical access preserved
            UI-->>User: Show result and retained obligations
        else Save fails
            App-->>UI: Change not confirmed
            UI-->>User: Show failure
        end
    end
    Note over User,App: Period omission is not deletion, restoration remains undefined
```
