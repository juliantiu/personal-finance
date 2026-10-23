# UC-001 — Create a user account — Sequence

[Written specification](../use-cases.md#uc-001) · [Approved implementation plan](implementation-plan.md)

Updated 23 October 2026. Identity and email implementations remain behind Application ports; HTTP and Identity types stay outside inner layers.

```mermaid
sequenceDiagram
    actor User as Prospective user
    participant UI as Blazor registration and confirmation UI
    participant API as Account controller
    participant App as Application commands and validation
    participant Identity as Infrastructure Identity adapter
    participant Email as Development email sink
    User->>UI: Enter email, password, and confirmation
    UI->>API: Submit registration
    API->>App: Dispatch registration command
    App->>App: Validate registration inputs
    alt Inputs invalid
        App-->>API: Validation errors without account creation
        API-->>UI: Safe validation response
    else Inputs valid
        App->>Identity: Create account and confirmation token
        Identity-->>App: Stable unverified account and protected token
        App->>Email: Deliver confirmation link
        Note over App,Email: Duplicates and known delivery failures use the same generic acknowledgement
        App-->>API: Registration result
        API-->>UI: Show registration result
    end
    User->>UI: Open confirmation link
    UI->>API: Submit account confirmation
    API->>App: Dispatch confirmation command
    App->>Identity: Validate token and confirm intended account
    alt Valid link within 24 hours
        Identity-->>App: Email confirmed
        App-->>API: Confirmation result
        API-->>UI: Show confirmation without sign-in
    else Invalid, tampered, or expired link
        Identity-->>App: Confirmation rejected
        App-->>API: Safe failure result
        API-->>UI: Show failure without verification
    end
    Note over UI,Identity: Resend supported; replay validates the token before harmless success
```
