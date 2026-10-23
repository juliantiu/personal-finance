# UC-004 — Maintain categories and subcategories — States

[Written specification (UC-004)](../use-cases.md#uc-004) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Ordinary-category and any-subcategory deletion stop new tracking without erasing history (D12). Parent deletion does not overwrite independent child deletion; protected Income/Savings/Debt categories cannot be deleted (D13). Omission is temporary selection absence. Restoration is undefined.

```mermaid
stateDiagram-v2
    [*] --> LoadingItems
    LoadingItems --> ItemsVisible: Items and history loaded
    ItemsVisible --> ReadOnly: Viewer
    ItemsVisible --> ItemDraft: Writer adds, edits, or renames
    ItemsVisible --> DeletionRejected: Delete protected system category
    DeletionRejected --> ItemsVisible: Review permitted actions
    ItemsVisible --> RetirementDraft: Delete ordinary category or any subcategory
    ItemDraft --> Saving: Submit edit
    RetirementDraft --> Saving: Submit explicit deletion
    Saving --> ActiveSaved: Edit succeeds
    Saving --> HistoricalRetained: Explicit deletion succeeds
    Saving --> SaveFailed: Save fails
    SaveFailed --> ItemsVisible: Review and retry
    ActiveSaved --> ItemsVisible: Review same item identity
    HistoricalRetained --> ItemsVisible: Review retained history
    note right of HistoricalRetained
        New tracking stops; balances and obligations remain reportable.
        Parent deletion preserves child-specific deletion facts.
        Restoration is not defined.
    end note
```
