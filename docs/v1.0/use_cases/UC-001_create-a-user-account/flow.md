# UC-001 — Create a user account — Flow

[Written specification](../use-cases.md#uc-001) · [Approved implementation plan](implementation-plan.md)

Updated 29 October 2026. Names belong to a later account profile page and OpenIddict sign-in is a separate slice. Development uses smtp4dev; production uses Azure Communication Services Email.

```mermaid
graph TD
    A["Open registration"] --> B["Enter email, password, and password confirmation"]
    B --> C{"Inputs valid?"}
    C -->|No| ERR["Show validation errors"]
    ERR --> B
    C -->|Yes| D["Create unverified account with stable ID and hashed password"]
    D --> E["Deliver confirmation link through the environment's email provider"]
    E -.-> DEV["Development: smtp4dev"]
    E -.-> PROD["Production: Azure Communication Services Email"]
    E --> F["Open confirmation link"]
    F --> G{"Link valid and within 24 hours?"}
    G -->|Yes| H["Verify email and show result"]
    G -->|No| I["Show invalid or expired confirmation result"]
    D -.-> J["Generic acknowledgement; retain unverified account after email failure; offer resend"]
    E -.-> J
    H --> K["Registration and confirmation complete"]
    NOTE["Resend included; valid-link replay succeeds without signing in"] -.-> H
```
