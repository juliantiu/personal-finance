# UC-012 — Manage transaction tags — States

[Written specification (UC-012)](../use-cases.md#uc-012)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Tag management is separate from transaction entry/review. Only custom-tag creation, editing, and deletion are defined. Default-tag mutability, duplicate names, cross-profile scope, and deletion effects on used tags remain unspecified. Tags alone do not create repayment links.

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
