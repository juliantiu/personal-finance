# UC-004 — Maintain categories and subcategories — States

[Written specification (UC-004)](../use-cases.md#uc-004)

Conceptual diagram for approved proposal version **v1.0**. Screen labels and UI/service boundaries are illustrative; no endpoint or implementation contract is implied.

**Scope and open rules:** Renaming preserves item identity and all financial references. Removal here applies to subcategories only. Category deletion, automatic-category restrictions, restoration, and removed-item use in existing periods remain unspecified.

```mermaid
stateDiagram-v2
    [*] --> LoadingItems
    LoadingItems --> ViewingItems: Items and comments loaded
    ViewingItems --> ReadOnly: View-only role
    ViewingItems --> ItemDraft: Writer adds, edits, or renames
    ViewingItems --> RemovalRequested: Writer removes subcategory
    ItemDraft --> SavingItem: Submit change
    RemovalRequested --> SavingItem: Submit removal
    SavingItem --> ActiveItem: Add, edit, or rename succeeds
    SavingItem --> HistoricalItem: Subcategory removal succeeds
    SavingItem --> SaveFailed: Save fails
    SaveFailed --> ViewingItems: Review and retry
    ActiveItem --> ViewingItems: Refresh items
    HistoricalItem --> ViewingItems: Review retained history
    note right of ActiveItem
        Rename retains identity and period references.
    end note
    note right of HistoricalItem
        History remains; future tracking stops.
    end note
```
