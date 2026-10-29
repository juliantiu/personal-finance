# UC-001 — Create a user account — States

[Written specification](../use-cases.md#uc-001) · [Approved implementation plan](implementation-plan.md)

Updated 23 October 2026. These are conceptual workflow states; they do not prescribe database columns.

```mermaid
stateDiagram-v2
    [*] --> RegistrationEntry
    RegistrationEntry --> Validating: Submit email and passwords
    Validating --> RegistrationEntry: Invalid inputs
    Validating --> Creating: Inputs valid
    Creating --> Unverified: Account persisted
    Creating --> CreationFailed: Creation fails
    CreationFailed --> RegistrationEntry: Correct or retry
    Unverified --> AwaitingConfirmation: Email accepted by the environment provider
    Unverified --> DeliveryFailed: Email delivery fails
    AwaitingConfirmation --> Verified: Valid link within 24 hours
    AwaitingConfirmation --> ConfirmationRejected: Invalid or expired link
    Verified --> [*]
    note right of DeliveryFailed
        Retain unverified account and offer resend.
    end note
    note right of Verified
        No authenticated session is created.
        Resend is included; still-valid replay succeeds.
    end note
```
