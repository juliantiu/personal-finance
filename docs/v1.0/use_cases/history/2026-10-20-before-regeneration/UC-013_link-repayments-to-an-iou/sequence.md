# UC-013 — Link repayments to an IOU — Sequence

[Written specification (UC-013)](../use-cases.md#uc-013)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Repayments must belong to the same profile as the IOU and may come from later periods. Attribution, partial allocation, overpayment, unlinking, record-deletion effects, and cross-currency calculation are unspecified. Linking is explicit and is not inferred from tags.

```mermaid
sequenceDiagram
    actor User as Authorized writer
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Identify IOU and approved repayment
    opt Repayment not yet approved
        Note over User,App: Complete UC-009 or UC-011 before linking
    end
    loop One or more repayments, including later periods
        User->>UI: Select repayment and request IOU link
        UI->>App: Link approved repayment to IOU
        alt Different profiles
            App-->>UI: Same-profile requirement not met
            UI-->>User: Show mismatch
        else Same profile and save succeeds
            App->>App: Save link and reflect unpaid amount under eventual rules
            App-->>UI: Link saved and remaining amount
            UI-->>User: Show link result
        else Link save fails
            App-->>UI: Link not confirmed
            UI-->>User: Show failure
        end
    end
    Note over UI,App: Attribution, overpayment, unlinking, and currency rules unspecified
    User->>UI: Review unpaid IOUs via UC-016
    UI->>App: Request unpaid IOU report
    App-->>UI: Report based on saved links
    UI-->>User: Show unpaid amounts
```
