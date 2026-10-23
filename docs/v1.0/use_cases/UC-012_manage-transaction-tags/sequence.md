# UC-012 — Manage transaction tags — Sequence

[Written specification (UC-012)](../use-cases.md#uc-012) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Multiple tags belong to individual entries. Custom tag management is separate from entry/review. Profile scope, used-tag cleanup and default-role protection are proposed A5; active IOU link consistency must be resolved, and no new default-tag deletion workflow is inferred.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open tag management
    UI->>App: Request default and custom tags
    App-->>UI: IOU, IOU REPAYMENT, and custom tags
    UI-->>User: Show tags
    alt Create or edit custom tag
        User->>UI: Enter custom tag details
        UI->>App: Save custom tag
    else Delete custom tag
        User->>UI: Request custom tag deletion
        UI->>App: Delete custom tag
        Note over UI,App: Effects on existing uses are unspecified
    end
    alt Operation succeeds
        App-->>UI: Updated available tags
        UI-->>User: Show updated tag list
    else Operation fails
        App-->>UI: Change not confirmed
        UI-->>User: Show issue
    end
    Note over User,App: Default mutability and duplicate rules unspecified
    Note over User,UI: Multiple tags applied in UC-009 or UC-011, links use UC-013
```
