# UC-005 — Save credit card payment methods — Sequence

[Written specification](../use-cases.md#uc-005) · [Proposal](../../PROPOSAL.md) · [Implementation phases](implementation-plan.md)

Reconciled on 12 November 2026. This sequence describes the Phase 2 save slice; the confirmed reveal/edit/retire/restore workflows are delivered in later phases.

```mermaid
sequenceDiagram
    actor User as Owner or editor
    participant UI as Payment methods page
    participant API as BFF-backed API
    participant App as Save card use case
    participant Protection as Card protection port
    participant Store as Transactional SQL adapter
    User->>UI: Enter full number and save
    UI->>API: Fetch antiforgery proof
    UI->>API: Submit number in protected POST body
    API->>App: Resolve eligible account and invoke save
    App->>Store: Read current profile role
    Store-->>App: Current role or safe denial
    Note over App: Authorize before card-specific validation
    App->>App: Normalize; validate 12-19 ASCII digits and Luhn
    App->>Protection: Protect for profile and method ID
    Protection-->>App: Ciphertext, fingerprint, key ID and last four
    App->>Store: Save protected metadata atomically
    Note over Store: Serialize writes; recheck access and key compatibility; SQL uniqueness
    alt Accepted
        Store-->>App: Stable ID and masked metadata
        App-->>API: Safe saved result
        API-->>UI: Masked created response; no-store
        UI-->>User: Clear draft and show masked method
    else Invalid, duplicate, forbidden or failed
        API-->>UI: Safe field or availability feedback
        UI-->>User: No confirmed save
    else Response lost
        UI-->>User: Uncertain outcome; draft stays only in memory
        User->>UI: Explicit refresh
        UI->>API: Read authoritative masked methods
        API-->>UI: Current masked methods
        Note over User,UI: Retry requires a separate explicit user action
    end
    Note over User,Store: Viewers read masked methods only. Cash is explicit and never the default.
```

**Nickname amendment (12 November 2026):** Payment methods have an optional nickname, trimmed before the 100-character limit; blank values become null and duplicates are allowed. Add-card entry accepts a nickname, and masked lists/profile details show it beside the masked number, falling back to the method label when absent. Existing and newly created Cash methods default to "Cash"; existing cards retain a null nickname. Nicknames do not affect card identity, duplicate detection, protection or matching. Renaming existing methods, including Cash, waits for the planned editing phase.
