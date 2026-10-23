# UC-016 — Review savings, debt, and unpaid IOUs — States

[Written specification (UC-016)](../use-cases.md#uc-016) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Main-currency reports combine stable item identity, approved signed movements, dated metric deltas, and valid repayment links. Explicit deletion or selection gaps do not hide balances/outstanding obligations. IOU credit A6 and time boundaries A10 remain qualified; report failure never implies complete results.

```mermaid
stateDiagram-v2
    [*] --> LoadingReports
    LoadingReports --> ReportsVisible: Records and calculations available
    LoadingReports --> EmptyReport: No relevant records
    LoadingReports --> ReportFailed: Load or calculation fails
    ReportFailed --> LoadingReports: Retry
    ReportsVisible --> LoadingReports: Reopen or refresh report
    EmptyReport --> LoadingReports: Reopen or refresh report
    note right of ReportsVisible
        Combine records by the same item across renames.
        Main currency only; deleted and omitted obligations stay visible.
        Source records are unchanged.
    end note
```
