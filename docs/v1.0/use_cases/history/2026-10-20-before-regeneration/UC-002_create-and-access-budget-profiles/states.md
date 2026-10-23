# UC-002 — Create and access budget profiles — States

[Written specification (UC-002)](../use-cases.md#uc-002)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Profile setup, profile selection, and creation boundaries are conceptual. Creation must include the automatic categories, but recovery from partial creation is unspecified. No deletion or ownership-transfer interaction is added.

```mermaid
stateDiagram-v2
    [*] --> LoadingProfiles
    LoadingProfiles --> NoProfiles: No accessible profiles
    LoadingProfiles --> ProfileList: Profiles available
    NoProfiles --> CreatingProfile: Create first profile
    ProfileList --> CreatingProfile: Create another profile
    CreatingProfile --> OpeningProfile: Profile and default categories created
    CreatingProfile --> CreationFailed: Creation fails
    CreationFailed --> CreatingProfile: Retry creation
    ProfileList --> OpeningProfile: Select profile
    OpeningProfile --> ProfileOpen: Access permitted
    OpeningProfile --> AccessDenied: Access removed
    AccessDenied --> LoadingProfiles: Return to profiles
    ProfileOpen --> LoadingProfiles: Switch profile
    note right of CreatingProfile
        Recovery from partial creation is unspecified.
    end note
```
