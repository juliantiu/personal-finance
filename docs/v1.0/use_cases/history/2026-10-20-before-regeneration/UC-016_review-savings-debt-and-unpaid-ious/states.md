# UC-016 — Review savings, debt, and unpaid IOUs — States

[Written specification (UC-016)](../use-cases.md#uc-016)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** This report is read-only and combines profile history by item identity, including renames. Inclusion of removed items in current balance lists, empty-total presentation, and currency basis are unresolved. IOU unpaid amounts depend on saved repayment links and eventual attribution rules.

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
        Currency basis and removed-item inclusion unspecified.
        Source records are unchanged.
    end note
```
