# UC-013 — Link repayments to an IOU — States

[Written specification (UC-013)](../use-cases.md#uc-013)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Repayments must belong to the same profile as the IOU and may come from later periods. Attribution, partial allocation, overpayment, unlinking, record-deletion effects, and cross-currency calculation are unspecified. Linking is explicit and is not inferred from tags.

```mermaid
stateDiagram-v2
    [*] --> IOUSelected
    IOUSelected --> AwaitingRepayment: No approved repayment exists
    AwaitingRepayment --> IOUSelected: Repayment recorded or approved elsewhere
    IOUSelected --> RepaymentSelected: Select approved repayment
    RepaymentSelected --> ProfileMismatch: Different profile
    ProfileMismatch --> IOUSelected: Choose another repayment
    RepaymentSelected --> SavingLink: Same-profile link submitted
    SavingLink --> LinkSaved: Save succeeds
    SavingLink --> LinkFailed: Save fails
    LinkFailed --> IOUSelected: Review and retry
    LinkSaved --> IOUSelected: Link another repayment
    LinkSaved --> UnpaidAmountVisible: Review report
    note right of LinkSaved
        Amount attribution and cross-currency rules unresolved.
    end note
```
