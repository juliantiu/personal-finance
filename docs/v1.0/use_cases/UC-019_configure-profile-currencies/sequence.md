# UC-019 — Configure profile currencies — Sequence

[Written specification (UC-019)](../use-cases.md#uc-019) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** The main unit governs allocations, goals, corrections, summaries and reports; alternatives are manually entered without FX and excluded from totals (D11). Foreign approval requires main values and complete per-currency splits. Historical main changes, used-currency disabling, and source enablement are proposed A12/A13 policies, not newly confirmed workflows. No initial main default is defined.

```mermaid
sequenceDiagram
    actor User as Profile participant
    participant UI as Conceptual user interface
    participant App as Conceptual application service
    User->>UI: Open profile currency settings during setup or later
    UI->>App: Request available currencies, current settings, and permitted actions
    App-->>UI: Main currency if set, enabled alternatives, available choices, and role
    UI-->>User: Show currency settings
    Note over UI,App: GBP and USD suffice initially, initial main-currency default unspecified
    alt View-only participant
        UI-->>User: Allow review without saving changes
    else Original owner or editor
        User->>UI: Select profile main currency
        opt Enable additional supported currency amounts
            User->>UI: Select optional alternatives, such as USD for GBP or GBP for USD
        end
        Note over UI,App: No alternatives is permitted, historical currency changes require undefined rules
        User->>UI: Save currency configuration
        UI->>App: Save settings subject to writer permission
        alt Save succeeds
            App-->>UI: Saved main currency and enabled alternatives
            UI-->>User: Show saved configuration
            Note over UI,App: Main currency governs allocations, period summaries, and profile reports
            opt Later manual transaction entry through UC-009
                UI-->>User: Request main-currency amount, offer optional enabled alternatives
                User->>UI: Enter main amount and any alternative amounts manually
                UI->>App: Save one transaction with its currency amounts
                App-->>UI: Transaction result under UC-009 rules
                UI-->>User: Show transaction result
                Note over UI,App: No system conversion, alternative amounts excluded from summary and report totals
            end
            opt Later import approval through UC-011
                alt Imported currency differs from profile main currency
                    UI-->>User: Require an amount in the profile main currency for calculations
                    User->>UI: Supply main-currency amount manually
                    Note over User,App: A missing main-currency amount must be supplied before approval completes
                else Imported currency is the profile main currency
                    UI-->>User: Use imported main amount, other enabled currency amounts remain optional
                end
                User->>UI: Submit approval in selected period
                UI->>App: Request approval with main amount and retained imported source details
                App-->>UI: Approval result under UC-011 rules
                UI-->>User: Show approval result
                Note over UI,App: Only main-currency amount contributes to calculations, source details retained
            end
        else Save fails
            App-->>UI: Requested configuration not confirmed as saved
            UI-->>User: Show failure and allow review or retry
        end
    end
```
