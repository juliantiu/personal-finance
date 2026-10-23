# UC-015 — Track and adjust debt — States

[Written specification (UC-015)](../use-cases.md#uc-015) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Positive Debt payments increase paid/decrease remaining; negative payments reverse both (D6). Plans do not move debt. Opening/manual edits create dated metric deltas T minus B, with paid/remaining corrected independently (D1). Prior changes propagate. Correction deletion, negative balances, precision and boundaries remain open.

```mermaid
stateDiagram-v2
    [*] --> LoadingDebt
    LoadingDebt --> DebtVisible: Paid and remaining derived
    DebtVisible --> MetricTargetDraft: Writer selects metric, effective time, and target
    MetricTargetDraft --> DeltaPrepared: Calculate target minus selected metric
    DeltaPrepared --> SavingCorrection: Submit one or two independent deltas
    SavingCorrection --> RecalculatingDebt: Save succeeds
    SavingCorrection --> SaveFailed: Save fails
    SaveFailed --> DebtVisible: Review and retry
    RecalculatingDebt --> DebtVisible: Subsequent values updated
    DebtVisible --> RecalculatingDebt: Earlier movement or correction changes
    note right of MetricTargetDraft
        Remaining correction does not alter paid.
        Separate metric changes create separate deltas.
    end note
    note right of DebtVisible
        Allocations do not move debt.
        Negative approved payments reverse positive payment effects.
    end note
```
