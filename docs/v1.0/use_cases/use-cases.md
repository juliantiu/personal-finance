# Budget and Transaction Tracker — Written Use Cases

Source: [Project proposal](../PROPOSAL.md). Proposal version: **v1.0**.

Version selection: `docs/v1.0/` is the only version folder available; Git is unavailable for this project directory. Input and outputs use that existing version.

Status: **Draft for approval.** Per-use-case diagrams will be generated after explicit approval of this set.

## Feature coverage

| Proposal feature or section | Use case IDs | Coverage / status |
| --- | --- | --- |
| Users and Usage; build order: user accounts | UC-001 | Account creation is in scope; account fields and authentication behavior are unspecified. |
| Overview; multiple profiles per account; profile creation | UC-002 | Create and access profiles; automatically create Savings, Debt, and Income categories. |
| Users and Usage; editable and view-only sharing; owner-only access control | UC-003 | Owner manages access; editors can change the whole profile; viewers can view it. |
| Profiles and Budget Periods: category/subcategory setup and comments | UC-004 | Maintain profile items and their optional comments. |
| Profiles and Budget Periods: rename identity and remove subcategories | UC-004 | Preserve references, financial records, and balances; removal stops future tracking. |
| Profiles and Budget Periods: saved credit cards; cash payment option | UC-005, UC-009, UC-010, UC-011 | Cards belong to the profile and are reusable across periods; Cash is available without being the default. |
| Profiles and Budget Periods: first period, dates, selections, allocations | UC-006 | Select profile items and allocate each selected subcategory; category allocations are sums. |
| Profiles and Budget Periods: later periods | UC-007 | Copy previous selections and allocations, start spending at zero, and allow independent adjustments. |
| Profiles and Budget Periods: previous periods remain editable | UC-008, UC-009, UC-014, UC-015 | Review and correct prior periods; effects on later carried balances need clarification. |
| Income, Savings, and Debt: expected and received income | UC-008, UC-009, UC-011 | Review expected/received income by subcategory; received income comes from approved Income transactions. Expected-income entry is unresolved. |
| Income, Savings, and Debt: period totals and remaining amounts | UC-008 | Received income, allocations, spending, allocations minus spending, and income minus spending. |
| Income, Savings, and Debt: savings balances, goals, resets, additions, withdrawals | UC-014, UC-007, UC-016 | Carry balances forward, show reset due, allow editing, and never reset automatically. |
| Income, Savings, and Debt: debt paid/remaining amounts | UC-015, UC-007, UC-016 | Carry totals forward and keep calculated amounts editable. |
| Transactions and IOUs: manual create/edit/delete and automatic approval | UC-009 | Selected-period assignment; optional comments; positive/negative amounts except positive-only Income. |
| Transactions and IOUs: statement upload and pending records | UC-010 | Preserve imported ID, description, date/time, amount, and payment method; match saved card number. |
| Transactions and IOUs: approve/split pending transactions | UC-011 | Assign to the selected approval period; linked entries can use different subcategories, comments, and multiple tags. |
| Transactions and IOUs: default and custom tags | UC-012, UC-009, UC-011 | Default IOU and IOU REPAYMENT tags; separately manage custom tags and apply multiple tags. |
| Transactions and IOUs: multiple repayments, including later periods | UC-013, UC-016 | Link repayments to one IOU within the same profile and report unpaid amounts. |
| Profile Reports: savings, debt, and unpaid IOUs | UC-016 | Current subcategory amounts/goals, savings total, debt paid/remaining amounts, debt total, unpaid IOUs. |
| Profile Reports: income between selected dates | UC-017 | Use transaction dates across periods. |
| Profile Reports: spending over all dates or selected dates; category filters | UC-018 | Filter totals by category/subcategory and aggregate across periods. |
| Profile Reports: combine items before/after rename | UC-016, UC-017, UC-018 | Aggregate by the same profile item, preserving history across names. |
| Key Workflow; Suggested Build Order; Definition of Done | UC-001–UC-018 | Setup, budgets, financial activity, review, and reporting; ordering and completion criteria are supporting constraints. |
| Scope: live bank connections and future saved-card matching | — | Deferred. No live-bank-connection use case is included in v1.0. |

## Shared rules and unresolved details

- **Roles:** the original owner alone can share a profile or change access. An editor may change all profile content, including previous periods. A view-only participant can read it. Every write flow below is subject to these permissions.
- **Terminology:** “authorized writer” means the original owner or an editor. “participant” means an owner, editor, or view-only user with access. Interaction labels describe goals; they do not define screens or technical contracts.
- **Financial consistency:** successful changes must be reflected in affected summaries and reports. The proposal does not define recalculation timing or how corrections to earlier savings/debt records propagate to later periods.
- **Failure handling assumption:** a failed save or upload should be reported and should not be represented as a successful change. Validation and retry details remain unspecified.
- **Account/profile lifecycle:** account fields, authentication, account recovery, profile naming, profile deletion, and ownership transfer are unspecified. Only account creation and profile creation/access are modeled.
- **Item lifecycle:** treatment of removed subcategories in an already-created period, restoration, category removal, and special handling of automatic Savings/Debt/Income categories are unspecified. Removal must preserve history and stop future tracking.
- **Period rules:** date validation, overlapping periods, empty selections, allocation amount limits, and the meaning of “previous period” when periods are entered out of order are unspecified.
- **Income expectations:** expected income is required, but the proposal does not say whether it is entered separately or derived from Income allocations. UC-008 exposes this as an unresolved operation rather than selecting a rule.
- **Payments/imports:** supported statement formats, duplicate imported IDs, unmatched/ambiguous card numbers, required payment selection, and editing/removing saved cards are unspecified. No default Cash selection or automatic creation of a missing card is assumed.
- **Transaction rules:** required fields beyond those named in the proposal, zero amounts outside Income, manual splitting, deleting pending imports, and whether approved imports can be returned to pending are unspecified. Splitting is modeled during imported-transaction review.
- **Split assumption to confirm:** linked split entries should preserve the original transaction total. The proposal describes splitting but does not explicitly state total validation, rounding, or retained source metadata.
- **Tag/IOU rules:** default-tag editability, the effect of deleting a tag already used, repayment amount attribution, overpayment, unlinking, and IOU direction/sign conventions are unspecified. Tagging and linking are separate actions; applying a tag alone is not assumed to create a repayment link.
- **Savings/debt rules:** goal/reset-period formats, manual reset behavior, starting-balance entry, whether additions/payments are transactions or separate adjustments, negative-balance rules, and precedence of manual overrides are unspecified.
- **Reports:** date-boundary inclusivity, time zone, currencies, negative-transaction treatment, and spending classification are unspecified. The written flows assume pending imports do not affect financial totals until approval, consistent with the proposal's review workflow; confirm this interpretation.

## UC-001 — Create a user account

**ID:** UC-001  
**Name:** Create a user account  
**Actors:** Prospective user; application  
**Preconditions:** The user needs an account to access budget profiles. Account creation is included in the proposed build order.

**Main Flow**
1. The user starts account creation.
2. The application requests the account information required by the eventual account design.
3. The user supplies the information and submits it.
4. The application creates an account that can be used to access profiles.

**Alternative Flows**
- **2–3a:** Account requirements and invalid/duplicate-account rules are unresolved; do not assume particular credentials or verification steps.
- **3–4a:** Creation fails; the application reports the failure without representing an account as created.

**Postconditions:** On success, a user account exists. On failure, account creation is not confirmed. Sign-in and recovery workflows are outside this specification until requirements are defined.

## UC-002 — Create and access budget profiles

**ID:** UC-002  
**Name:** Create and access budget profiles  
**Actors:** Account holder; application  
**Preconditions:** The user has a user account.

**Main Flow**
1. The user accesses the budget profiles available to their account.
2. The user chooses to create a new profile.
3. The application creates the profile with the user as its original owner.
4. The application automatically creates Savings, Debt, and Income categories in that profile.
5. The user opens the profile to begin setup.
6. The user can return to their accessible profiles and open another profile.

**Alternative Flows**
- **1a:** No profiles are available; the user can create their first profile.
- **2a:** The user opens an existing owned/shared profile; the application permits actions according to their role.
- **3–4a:** Creation fails; the application reports the failure. Partial-creation recovery is unspecified.
- **6a:** Access to a profile has been removed; the user cannot continue accessing it through their account.

**Postconditions:** A newly created profile has its original owner and automatic categories, or an existing accessible profile is opened. Profiles remain independent and one account can access multiple profiles.

## UC-003 — Share a profile and change access

**ID:** UC-003  
**Name:** Share a profile and change access  
**Actors:** Original owner; recipient user; application  
**Preconditions:** The profile exists. The acting user is its original owner. The recipient-identification and invitation mechanism are unspecified.

**Main Flow**
1. The original owner opens the profile's sharing controls.
2. The owner identifies another user to receive access.
3. The owner selects editor or view-only access.
4. The application saves that user's access to the profile.
5. The recipient accesses the profile with the assigned permissions.
6. The owner can return to change or remove that user's access.

**Alternative Flows**
- **1a:** An editor or viewer tries to share the profile or change access; the application denies the action.
- **2a:** The recipient cannot be identified; access is not granted. Invitation and acceptance behavior remains unresolved.
- **3–5a:** An editor can modify all profile content but cannot manage sharing; a viewer can view but cannot modify content.
- **4 or 6a:** Saving access fails; the requested change is not confirmed.

**Postconditions:** Access reflects the owner's successful changes. Only the original owner retains authority to manage sharing.

## UC-004 — Maintain categories and subcategories

**ID:** UC-004  
**Name:** Maintain categories and subcategories  
**Actors:** Authorized writer; participant viewing comments/history; application  
**Preconditions:** The user has access to a profile; changes require writer permission.

**Main Flow**
1. The user reviews the profile's categories, subcategories, and optional comments.
2. An authorized writer adds or edits a category/subcategory and its optional comment.
3. The application saves the item at profile level for use by budget periods.
4. To rename an item, the writer changes its name.
5. The application updates the same item; period references, allocations, transactions, history, and savings/debt balances remain associated with it.
6. To stop tracking a subcategory, the writer removes it.
7. The application preserves that subcategory's history and stops its future tracking.

**Alternative Flows**
- **1a:** A viewer reads items/comments but cannot save changes.
- **2–3 or 4–5a:** A save fails; the requested change is not confirmed.
- **6–7a:** The subcategory has historical activity; that activity remains available. Treatment in existing periods and restoration are unresolved.
- **2 or 6a:** The user wants to delete a category or change special-category behavior; those rules are unresolved and are not inferred from subcategory removal.

**Postconditions:** Profile items/comments reflect successful edits. Renames preserve identity and history; removed subcategories retain history and cease future tracking. Allocation amounts remain period-specific.

## UC-005 — Save credit card payment methods

**ID:** UC-005  
**Name:** Save credit card payment methods  
**Actors:** Authorized writer; application  
**Preconditions:** The profile exists and the user has writer permission.

**Main Flow**
1. The user opens the profile's saved payment methods.
2. The user supplies a credit card number to identify a card.
3. The application saves the card in the profile.
4. The saved card becomes available for transaction selection across that profile's periods and for imported-card-number matching.

**Alternative Flows**
- **2–3a:** The card cannot be saved; the application reports the issue. Number-validation and duplicate-card rules are unspecified.
- **4a:** The user uses Cash for a transaction; Cash is always available and is not selected by default.
- **1a:** Editing/removing a saved card is requested; the proposal does not define those operations or their effect on history.

**Postconditions:** A successfully saved card is reusable within the profile. Saving it does not select a default transaction payment method.

## UC-006 — Create the first budget period

**ID:** UC-006  
**Name:** Create the first budget period  
**Actors:** Authorized writer; application  
**Preconditions:** A profile exists with categories/subcategories available to reference and no previous period to copy.

**Main Flow**
1. The user starts creating a budget period in the profile.
2. The user enters its start and end dates.
3. The user selects the profile categories and subcategories to reference.
4. The user sets an allocation for each selected subcategory.
5. The application calculates each category's allocation as the sum of its selected subcategory allocations.
6. The user saves the period.
7. The application makes the period available for financial activity and review.

**Alternative Flows**
- **3–4a:** The user changes selections or allocations before saving; category totals reflect the revised selections.
- **2–6a:** Dates, selections, or allocations are unacceptable under rules still to be defined; creation cannot be confirmed until corrected.
- **6a:** Saving fails; creation is not confirmed.

**Postconditions:** A period exists with its own dates, item references, and allocations. Saving the period does not redefine the profile items.

## UC-007 — Create a later budget period

**ID:** UC-007  
**Name:** Create a later budget period  
**Actors:** Authorized writer; application  
**Preconditions:** The profile has a previous budget period to use as a starting point.

**Main Flow**
1. The user starts creating another budget period and supplies its start/end dates.
2. The application starts with references to the profile categories/subcategories used in the previous period and copies their allocations.
3. The application starts the new period's spending at zero and carries savings balances and debt totals forward.
4. The user adjusts the new period's selections and subcategory allocations as needed.
5. The application recalculates category allocations from the selected subcategories.
6. The user saves the new period.
7. Previous periods remain available with their existing selections, allocations, and activity.

**Alternative Flows**
- **2a:** A previously selected subcategory has been removed; it must not resume future tracking. The precise presentation of its preserved history is unresolved.
- **2–3a:** The predecessor is ambiguous or an earlier balance was corrected; period-ordering and propagation rules require clarification.
- **4a:** The user retains all copied selections/allocations and proceeds to save.
- **6a:** Saving fails; creation of the new period is not confirmed.

**Postconditions:** The new period has independently adjustable selections and allocations, zero starting spending, and carried savings/debt values. Creating it does not change earlier periods.

## UC-008 — Review and correct a budget period

**ID:** UC-008  
**Name:** Review and correct a budget period  
**Actors:** Participant; authorized writer for corrections; application  
**Preconditions:** The user can access the profile and at least one budget period.

**Main Flow**
1. The user opens a current or previous budget period.
2. The application shows its dates, selected items, allocations, and recorded financial activity, including expected/received income by Income subcategory.
3. The application shows total income received, total allocated, total spending, allocated allowance remaining (allocations minus spending), and income remaining after spending (income minus spending).
4. An authorized writer changes the period's selections or allocations when a correction is needed.
5. The application saves the correction and updates category allocation totals and affected period summaries.
6. The user reviews the resulting period; transaction, savings, and debt corrections follow UC-009, UC-014, and UC-015.

**Alternative Flows**
- **4a:** A viewer reviews the period without modifying it.
- **2 or 4a:** The user needs to enter/change expected income; how this relates to allocations is unresolved. The final interaction must follow the clarified rule.
- **4–5a:** A selected item has financial history or has been removed; rules for deselection and removed-item use in existing periods are unresolved, and history must be preserved.
- **5a:** Saving fails; the requested correction is not confirmed.
- **6a:** Editing a prior period affects carried savings/debt; propagation to later periods is unresolved.

**Postconditions:** The participant can review the selected period. Successful corrections are saved and reflected in the affected summaries; previous periods remain accessible.

## UC-009 — Create, edit, or delete an approved transaction

**ID:** UC-009  
**Name:** Create, edit, or delete an approved transaction  
**Actors:** Authorized writer; application  
**Preconditions:** The profile and target period exist; the user has writer permission and a relevant subcategory is available.

**Main Flow**
1. The user selects a budget period, including a previous period when correcting history.
2. The user starts manual transaction entry and records its details, amount, and subcategory.
3. The user selects a saved profile card or Cash as the payment method; Cash is available without being preselected by default.
4. The user optionally enters a comment and applies one or more tags.
5. The user saves the transaction.
6. The application enforces a positive amount for Income transactions and automatically approves a valid manually created transaction in the selected period.
7. The application saves the transaction/comment/tags and reflects approved activity in relevant tracking, summaries, and reports.
8. To correct an approved transaction, the user opens it, edits its details or deletes it, and the application updates the affected records.

**Alternative Flows**
- **2 or 8a:** A non-Income transaction has a positive or negative amount; both signs are supported. Zero handling is unspecified.
- **6 or 8a:** An Income amount is not positive; the application does not accept that transaction/change until corrected.
- **3a:** No saved card is suitable; the user can save a card through UC-005 or deliberately select Cash when appropriate. Whether a payment method is mandatory is unresolved.
- **5 or 8a:** Saving/deletion fails; the requested change is not confirmed.
- **8b:** The transaction is part of a split or linked to an IOU; effects on linked entries/repayments require clarified consistency rules.

**Postconditions:** A successfully created manual transaction is approved in the selected period. Successful edits/deletions are reflected in affected totals; comments are retained with transactions that still exist.

## UC-010 — Upload a bank statement for review

**ID:** UC-010  
**Name:** Upload a bank statement for review  
**Actors:** Authorized writer; application  
**Preconditions:** The user has writer permission in the target profile and a bank statement to upload.

**Main Flow**
1. The user selects the target profile and uploads a statement.
2. The application reads the statement transactions, including their ID, description, date/time, amount, and payment method information.
3. The application matches each imported credit card number to a saved card in that profile and automatically selects the matching payment method.
4. The application places the imported transactions in a pending review list.
5. The user opens the list to begin review through UC-011.

**Alternative Flows**
- **1–2a:** Upload or statement reading fails; the application reports the issue. Supported formats and partial-import recovery are unresolved.
- **3a:** No unique saved-card match is available; resolution is required before a payment method can be confirmed. Fallback behavior is unspecified; Cash is not assumed automatically.
- **2–4a:** An imported transaction ID appears to be duplicated; duplicate detection and resolution are unresolved.

**Postconditions:** Successfully imported records are pending review and retain their source details. Approval-period assignment occurs during UC-011. Under the stated assumption, pending records do not yet contribute to financial totals.

## UC-011 — Approve or split a pending transaction

**ID:** UC-011  
**Name:** Approve or split a pending transaction  
**Actors:** Authorized writer; application  
**Preconditions:** The profile contains a pending imported transaction and a budget period that can receive the approval.

**Main Flow**
1. The user opens a pending transaction and reviews its imported details and matched payment method.
2. The user selects the period for approval and assigns a subcategory.
3. The user confirms the payment method and optionally adds a transaction comment and multiple tags.
4. The user approves the transaction.
5. The application validates the entry, including positive-only Income amounts, and saves it as approved in the selected period.
6. The application resolves its pending review status and updates relevant financial tracking, summaries, and reports.

**Alternative Flows**
- **2–4a (split):** The user divides the transaction into linked entries, assigns each entry an amount and subcategory, and may assign different subcategories. For example, restaurant spending and a portion paid for others can be separate entries, with IOU applied to the latter. The user may add comments and multiple tags to each entry, then approve them in the selected period. The application preserves their link and updates totals. Preservation of the original total is an explicit assumption awaiting confirmation.
- **1–3a:** Card matching was unresolved; the user resolves the payment method. Available choices include saved cards and Cash; the exact correction interaction is unspecified.
- **5a:** An Income entry is zero or negative; approval requires a positive amount.
- **4–6a:** Approval fails; completion is not confirmed and the user can continue review. Atomic handling of split entries is unspecified.
- **1–4a:** The user leaves without approval; the transaction remains pending. Rejection/deletion of imports is unspecified.

**Postconditions:** The transaction or its linked split entries are approved in the chosen period and retain saved comments/tags. The source is no longer an unresolved pending item on successful completion. Transaction dates remain distinct from approval-period assignment.

## UC-012 — Manage transaction tags

**ID:** UC-012  
**Name:** Manage transaction tags  
**Actors:** Authorized writer; application  
**Preconditions:** The user has writer permission in a profile. Transactions can use multiple tags.

**Main Flow**
1. The user opens tag management separately from transaction entry/review.
2. The application provides the default IOU and IOU REPAYMENT tags and existing custom tags.
3. The user creates a custom tag or edits an existing custom tag.
4. The application saves the tag for use on transactions.
5. The user may delete a custom tag.
6. The user applies available tags to an entry during UC-009 or UC-011; an entry can have more than one tag.

**Alternative Flows**
- **3–4a:** A tag cannot be saved; the application reports the issue. Naming/duplicate rules and cross-profile tag scope are unspecified.
- **5a:** The tag is already used; the effect on existing transactions is unresolved.
- **2–5a:** The user wants to edit/delete a default tag; default-tag mutability is unspecified.
- **4 or 5a:** Saving/deletion fails; the requested change is not confirmed.

**Postconditions:** Successful custom-tag changes are saved. Default IOU/IOU REPAYMENT tags are available, and successful transaction tagging preserves multiple tag associations. No repayment link is inferred from tagging alone.

## UC-013 — Link repayments to an IOU

**ID:** UC-013  
**Name:** Link repayments to an IOU  
**Actors:** Authorized writer; application  
**Preconditions:** An approved IOU transaction and repayment transaction(s) exist within the same profile; the user has writer permission.

**Main Flow**
1. The user identifies an IOU transaction tagged IOU.
2. The user identifies a repayment transaction using IOU REPAYMENT.
3. The user links the repayment to that IOU.
4. The application saves the link and reflects the repayment in the IOU's unpaid amount.
5. The user repeats the process for additional repayments, including transactions in later periods of the same profile.
6. The user reviews the remaining unpaid amount through UC-016.

**Alternative Flows**
- **2a:** A repayment has not yet been recorded/approved; the user first completes UC-009 or UC-011.
- **3a:** The selected repayment belongs to another profile; it does not meet the same-profile requirement.
- **3–4a:** The proposed link would overpay the IOU or needs a partial attribution; the handling rule is unresolved.
- **4a:** Saving the link fails; the repayment is not confirmed as linked.
- **5a:** A linked record later changes or is deleted; unlinking and recalculation rules are unresolved.

**Postconditions:** Successfully linked repayments remain associated with one IOU across periods within the profile, and its unpaid amount reflects those links according to the eventual attribution rules.

## UC-014 — Track and adjust savings

**ID:** UC-014  
**Name:** Track and adjust savings  
**Actors:** Participant; authorized writer for changes; application  
**Preconditions:** A Savings subcategory is available in the profile and the user can access its relevant period/history.

**Main Flow**
1. The user opens a Savings subcategory and reviews its current balance, goal, reset period, and period additions/withdrawals.
2. An authorized writer sets or edits its goal, reset period, and editable balance information.
3. The writer records or adjusts money added or withdrawn for the selected period, including correcting withdrawals.
4. The application saves successful changes and reflects them in savings tracking and relevant summaries/reports.
5. When a reset period is due, the application shows that it is due and leaves the balance unchanged by any automatic reset.
6. The balance is carried forward when the next period is created through UC-007.

**Alternative Flows**
- **2–3a:** A viewer reviews the values without editing them.
- **2–4a:** A balance/withdrawal needs correction in a previous period; it remains editable. Propagation to later balances requires clarification.
- **3a:** A withdrawal or adjustment violates a rule yet to be defined; negative-balance and adjustment/transaction rules are unresolved.
- **4a:** Saving fails; the requested change is not confirmed.
- **5a:** The user wants to perform a manual reset; the proposal specifies a due indication but does not define a reset operation.

**Postconditions:** Successfully edited savings values and period activity are retained and reportable. Balances carry forward; a due reset never causes an automatic reset.

## UC-015 — Track and adjust debt

**ID:** UC-015  
**Name:** Track and adjust debt  
**Actors:** Participant; authorized writer for changes; application  
**Preconditions:** A Debt subcategory is available in the profile and the user can access its relevant period/history.

**Main Flow**
1. The user opens a Debt subcategory and reviews amounts paid and remaining.
2. An authorized writer records or adjusts debt payment activity for the selected period.
3. The application updates paid and remaining amounts from the recorded activity.
4. The writer may edit the calculated amounts when a correction is needed.
5. The application saves successful changes and reflects them in debt tracking and relevant summaries/reports.
6. Debt totals carry forward when the next period is created through UC-007.

**Alternative Flows**
- **2 or 4a:** A viewer reviews the debt without changing it.
- **2–5a:** The user corrects a previous period; it remains editable. Effects on later carried totals and override precedence require clarification.
- **2–4a:** Initial debt entry, overpayment, negative remaining debt, and whether payments are separate adjustments or transactions are unresolved.
- **5a:** Saving fails; the requested change is not confirmed.

**Postconditions:** Debt paid/remaining values reflect successful recorded activity and permitted manual corrections. Those totals remain available for reporting and carry forward between periods.

## UC-016 — Review savings, debt, and unpaid IOUs

**ID:** UC-016  
**Name:** Review savings, debt, and unpaid IOUs  
**Actors:** Participant; application  
**Preconditions:** The user can access a profile. Relevant records may span several budget periods.

**Main Flow**
1. The user opens the profile's savings/debt/IOU reports.
2. The application shows each Savings subcategory's current amount and goal and total savings.
3. The application shows each Debt subcategory's remaining and paid amounts and total remaining debt.
4. The application shows IOU transactions with unpaid amounts after linked repayments, including repayments from later periods.
5. The application combines records belonging to the same profile category/subcategory across periods and renames, preserving historical records.
6. The user reviews the profile's financial position.

**Alternative Flows**
- **2–4a:** No relevant records exist; the report indicates the absence of records. Empty-total presentation is unspecified.
- **2–5a:** An item was renamed/removed; history remains associated with the same item. Which removed items are included in current balance lists is unresolved.
- **2–5b:** Loading/calculation fails; the application reports failure and does not represent incomplete results as complete.

**Postconditions:** The user can review the profile's savings, debt, and unpaid IOUs without changing source records.

## UC-017 — Report income for a date range

**ID:** UC-017  
**Name:** Report income for a date range  
**Actors:** Participant; application  
**Preconditions:** The user can access the profile.

**Main Flow**
1. The user opens the profile's income report.
2. The user selects start and end dates.
3. The application selects recorded income using transaction dates within the chosen range, across budget periods.
4. The application combines any records for the same category/subcategory across renames and periods.
5. The application shows total income earned for the selected dates.

**Alternative Flows**
- **2a:** The date range is invalid; the user must correct it. Date-boundary and time-zone rules are unresolved.
- **3a:** No matching income exists; the report indicates no matching activity.
- **3–5a:** Loading/calculation fails; the application reports failure without representing incomplete results as complete.
- **2–5a:** The user changes the selected dates; the report recomputes using the new transaction-date range.

**Postconditions:** The report shows recorded income for the chosen transaction-date range, independent of approval-period dates. Source transactions remain unchanged.

## UC-018 — Report spending by date and category

**ID:** UC-018  
**Name:** Report spending by date and category  
**Actors:** Participant; application  
**Preconditions:** The user can access the profile.

**Main Flow**
1. The user opens the profile's spending report.
2. The user chooses all transaction dates or a selected start/end date range.
3. The user optionally filters totals by category or subcategory.
4. The application selects matching spending using transaction dates across budget periods.
5. The application combines records for the same profile item, including records from before it was renamed.
6. The application shows spending totals for the chosen dates and filters.
7. The user changes or clears filters to review another view of spending.

**Alternative Flows**
- **2a:** The date range is invalid; the user corrects it. Date boundaries and time zones are unresolved.
- **3–6a:** A selected item has been renamed or removed; its preserved historical records remain associated with the same item. Historical filter presentation is unspecified.
- **4a:** No activity matches; the report indicates no matching activity.
- **4–6a:** Loading/calculation fails; the application reports failure without representing incomplete results as complete.
- **4–6b:** Positive/negative transactions, IOU repayments, or savings/debt activity need classification as spending; the proposal leaves their precise netting/classification rules unresolved.

**Postconditions:** The user can review spending totals for all dates or a chosen transaction-date range and category/subcategory filter, with history combined across periods and renames. Source records remain unchanged.

## Approval and next step

Approve or revise UC-001–UC-018 and the stated assumptions before diagram generation. Approval authorizes three conceptual Mermaid documents (`flow.md`, `states.md`, and `sequence.md`) for every approved use case under this version's `use_cases/` directory. Unresolved rules will remain visibly qualified unless clarified; diagram generation will not invent screens, endpoints, or implementation contracts.
