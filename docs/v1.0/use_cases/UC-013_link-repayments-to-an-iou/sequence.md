# UC-013 — Link repayments to an IOU — Sequence

[Written specification (UC-013)](../use-cases.md#uc-013) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** One repayment entry has one IOU target (D4), while an IOU may have many repayments from later periods. Both use their required main amounts; source currencies do not affect the unit. A6 full absolute credit, direction, partial attribution and overpayment remain proposed/open; tagging alone does not link.

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
        alt Different profiles or repayment already targets another IOU
            App-->>UI: Same-profile or single-target requirement not met
            UI-->>User: Show mismatch
        else Same profile, unused repayment target, and save succeeds
            App->>App: Save link and reflect unpaid amount under eventual rules
            App-->>UI: Link saved and remaining amount
            UI-->>User: Show link result
        else Link save fails
            App-->>UI: Link not confirmed
            UI-->>User: Show failure
        end
    end
    Note over UI,App: Main amounts only, A6 attribution and overpayment remain proposed
    User->>UI: Review unpaid IOUs via UC-016
    UI->>App: Request unpaid IOU report
    App-->>UI: Report based on saved links
    UI-->>User: Show unpaid amounts
```
