# UC-003 — Share a profile and change access — States

[Written specification (UC-003)](../use-cases.md#uc-003)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Only the original owner can manage sharing. Recipient identification and invitation/acceptance mechanisms are unspecified. Saving role changes and removing access are shown without an assumed delivery channel.

```mermaid
stateDiagram-v2
    [*] --> CheckingOwner
    CheckingOwner --> AccessDenied: Acting user is not original owner
    CheckingOwner --> SharingReady: Original owner
    SharingReady --> RecipientUnresolved: Recipient not identified
    RecipientUnresolved --> SharingReady: Recipient identified
    SharingReady --> AccessDraft: Select or change role or remove access
    AccessDraft --> SavingAccess: Submit change
    SavingAccess --> AccessUpdated: Save succeeds
    SavingAccess --> SaveFailed: Save fails
    SaveFailed --> AccessDraft: Correct or retry
    AccessUpdated --> SharingReady: Manage another participant
    AccessDenied --> [*]
    note right of AccessUpdated
        Editors modify content; viewers read.
        Only the original owner manages sharing.
    end note
```
