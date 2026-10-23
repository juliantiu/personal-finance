# UC-003 — Share a profile and change access — Sequence

[Written specification (UC-003)](../use-cases.md#uc-003)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Only the original owner can manage sharing. Recipient identification and invitation/acceptance mechanisms are unspecified. Saving role changes and removing access are shown without an assumed delivery channel.

```mermaid
sequenceDiagram
    actor Owner as Acting user
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    actor Recipient as Recipient user
    Owner->>UI: Open sharing controls
    UI->>App: Check original-owner authority
    alt Not original owner
        App-->>UI: Deny sharing changes
        UI-->>Owner: Show access denied
    else Original owner
        App-->>UI: Sharing information
        Owner->>UI: Identify recipient and assign, change, or remove role
        Note over UI,App: Identification and invitation mechanisms unspecified
        UI->>App: Save owner-authorized access change
        alt Recipient resolved and save succeeds
            App-->>UI: Access updated
            UI-->>Owner: Confirm change
            opt Recipient later opens profile
                Recipient->>App: Request profile access
                App-->>Recipient: Enforce editor, viewer, or removed access
            end
        else Recipient unresolved or save fails
            App-->>UI: Access change not confirmed
            UI-->>Owner: Show issue
        end
    end
```
