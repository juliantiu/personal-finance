# UC-005 — Save credit card payment methods — Flow

[Written specification](../use-cases.md#uc-005) · [Proposal](../../PROPOSAL.md) · [Implementation phases](implementation-plan.md)

Reconciled on 12 November 2026 with the confirmed UC-005 decisions. Phase 2 delivers encrypted saving and masked review. Reveal, editing and retirement/restoration remain later phases; actual transaction/import integration requires the dependent use cases.

```mermaid
flowchart TD
    A[Open profile payment methods] --> ACCESS{Current profile access?}
    ACCESS -->|None| UNAVAILABLE[Non-disclosing unavailable result]
    ACCESS -->|Participant| LIST[Review masked cards and explicit Cash]
    LIST --> WRITE{Owner or editor?}
    WRITE -->|No| READ[Masked review only]
    WRITE -->|Yes| INPUT[Enter full card number]
    INPUT --> VALID{Remove spaces and hyphens; 12-19 ASCII digits and Luhn?}
    VALID -->|No| ERROR[Safe field feedback; nothing saved]
    ERROR --> INPUT
    VALID -->|Yes| SAVE[Recheck access; encrypt and commit metadata atomically]
    SAVE --> RESULT{Outcome?}
    RESULT -->|Accepted| MASK[Clear draft; show masked saved method]
    RESULT -->|Active duplicate or known failure| ERROR
    RESULT -->|Response lost| UNCERTAIN[Keep draft in memory; outcome uncertain]
    UNCERTAIN --> REFRESH[Explicitly refresh authoritative methods]
    REFRESH --> RETRY[User decides whether to retry]
    RETRY --> INPUT
    MASK --> LIST
    LIFECYCLE[Later phases: explicit reveal; stable-ID correction; retirement; re-save retired number to restore original ID] -.-> LIST
    CASH[Cash is unchanged and never selected by default] -.-> LIST
```

Canonical identity is unique per profile, including retained cards. Leading zeros remain text. Different full numbers sharing the last four digits are distinct; another profile may save the same number. No card-brand whitelist or live verification is implied. Future stale edits/removals require explicit refresh without automatic resubmission.

**Nickname amendment (12 November 2026):** Payment methods have an optional nickname, trimmed before the 100-character limit; blank values become null and duplicates are allowed. Add-card entry accepts a nickname, and masked lists/profile details show it beside the masked number, falling back to the method label when absent. Existing and newly created Cash methods default to "Cash"; existing cards retain a null nickname. Nicknames do not affect card identity, duplicate detection, protection or matching. Renaming existing methods, including Cash, waits for the planned editing phase.
