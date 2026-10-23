# UC-012 — Manage transaction tags — States

[Written specification (UC-012)](../use-cases.md#uc-012) · [Source proposal](../../PROPOSAL.md) · [Supporting schema](../../migrations/schema-design.md)

Approved generation for **v1.0**, 20 October 2026. UI/service boundaries are conceptual; no HTTP or implementation contract is implied.

**Scope and open policies:** Multiple tags belong to individual entries. Custom tag management is separate from entry/review. Profile scope, used-tag cleanup and default-role protection are proposed A5; active IOU link consistency must be resolved, and no new default-tag deletion workflow is inferred.

```mermaid
stateDiagram-v2
    [*] --> LoadingTags
    LoadingTags --> TagList: Default and custom tags available
    TagList --> CustomTagDraft: Create or edit custom tag
    TagList --> DeletingCustomTag: Delete custom tag
    CustomTagDraft --> SavingCustomTag: Submit details
    SavingCustomTag --> TagsUpdated: Save succeeds
    DeletingCustomTag --> TagsUpdated: Delete succeeds under eventual rules
    SavingCustomTag --> OperationFailed: Save fails
    DeletingCustomTag --> OperationFailed: Delete fails
    OperationFailed --> TagList: Review and retry
    TagsUpdated --> TagList: Review available tags
    note right of DeletingCustomTag
        Effects on existing tagged transactions unspecified.
    end note
    note right of TagList
        Default-tag mutability unspecified.
        Applying tags occurs in UC-009 or UC-011.
    end note
```
