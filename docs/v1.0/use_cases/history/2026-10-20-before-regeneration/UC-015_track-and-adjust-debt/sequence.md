# UC-015 — Track and adjust debt — Sequence

[Written specification (UC-015)](../use-cases.md#uc-015)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Debt activity, paid/remaining calculations, and manual overrides are conceptual operations. Starting debt entry, overpayment, currency denomination, override precedence, and propagation of earlier corrections are unspecified. Calculated amounts remain editable.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open Debt subcategory and period
    UI->>App: Request paid and remaining amounts
    App-->>UI: Debt values
    UI-->>User: Show debt position
    opt Authorized writer changes debt
        User->>UI: Record or adjust payment activity
        UI-->>User: Show updated paid and remaining amounts
        Note over UI,App: Calculation location is illustrative
        opt Manually correct calculated amounts
            User->>UI: Edit paid or remaining amount
        end
        UI->>App: Save debt activity and permitted corrections
        alt Save succeeds
            App-->>UI: Updated debt tracking and report values
            UI-->>User: Show saved result
        else Save fails
            App-->>UI: Change not confirmed
            UI-->>User: Show failure
        end
    end
    Note over UI,App: Initial debt, overpayment, overrides, and propagation unspecified
    opt Later period created through UC-007
        App->>App: Carry debt totals into new period
    end
```
