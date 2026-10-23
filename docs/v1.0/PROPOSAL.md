# Budget and Transaction Tracker

## Overview

A budgeting app where users manage multiple budget profiles. Each profile defines categories and subcategories and contains budget periods that reference selected items, with their own dates, allocations, and financial activity. Profiles can be shared as editable or view-only.

## Goals

- Track planned and actual income, spending, savings, debt, and IOUs.
- Keep previous budget periods available for review and correction.
- Support manual transaction entry and bank statement upload with transaction review.

## Users and Usage

Each user account can access multiple profiles. The original owner alone can share a profile or change access. An editor can change the whole profile, including all periods, categories, and subcategories; view-only users can view it.

Each account records an email address, first name, and last name separately from its stable identity. Required fields, email uniqueness and verification, and sign-in rules remain to be decided.

## Core Features

### Profiles and Budget Periods

- Each profile has a main currency for all allocations, period summaries, and profile report calculations. Settings allow additional currencies, with GBP (£) and USD ($) sufficient initially.
- Users create and manage categories and subcategories within a budget profile. Savings, Debt, and Income categories are created automatically when the profile is created and cannot be deleted. Their subcategories can be deleted from future tracking. Allocation amounts are set separately for each budget period.
- Each category and subcategory has an optional comment field that users can view and edit.
- Each profile can save credit cards as payment methods, identified by their card numbers, for use across its budget periods.
- From a profile, users create a budget period with start and end dates. For the first period, they select profile categories and subcategories to reference and allocate amounts to the selected subcategories. Each category's allocation is the sum of its selected subcategory allocations.
- Later periods follow date order and reuse the previous period's category and subcategory references and copy its allocations, with spending reset to zero. Users can adjust these selections and allocations without changing previous periods.
- Previous periods remain available to view and edit.
- Renaming a category or subcategory preserves its identity across periods, including allocations, transactions, history, and savings or debt balances. Explicitly deleting an ordinary category or any subcategory stops future tracking while retaining the item and its history. Deleted items are excluded from new selections and future period copies; existing periods remain available for review and correction.
- Deleting a category makes all its subcategories unavailable for new tracking without changing whether each child was separately deleted. If restoration is supported later, independently deleted children must remain unavailable. Restoration itself is not yet a defined feature.
- Leaving an item out of one or more budget periods does not delete it. It may be selected again in a future period. Removing a selection from one period does not change other existing periods.
- Deletion or gaps in period selection do not erase balances or outstanding obligations, or hide them from relevant profile reports.

### Income, Savings, and Debt

- Income subcategories track expected and received income; the period allocation is the expected income amount.
- Each period shows total income received, total allocated, and total spending. It also shows allocated allowance remaining (allocations minus spending) and income remaining after spending (income minus spending).
- Savings subcategories track current balances, goals, reset periods, and money added or withdrawn per period. Balances carry forward; balances and withdrawals remain editable. The reset period is an informational note, such as “every 12 months”; it does not calculate a due date or trigger a reset. Users manually set the balance to zero when needed.
- Debt subcategories track amounts paid and remaining, carrying totals forward between periods. Calculated amounts remain editable. Allocations are plans and do not change debt balances. Positive approved payments increase paid and decrease remaining; negative payments reverse those effects.
- Savings and debt balances combine approved transactions with dated corrections. Positive savings transactions add funds and negative ones withdraw funds. Opening balances and manual balance edits are recorded as the difference from the calculated amount, not a fixed replacement checkpoint. Earlier corrections therefore change subsequent balances, including balances after a manual reset. Debt paid and debt remaining may be corrected independently. Whether corrections can be deleted remains undecided.

### Transactions and IOUs

- Users can create, edit, and delete transactions. Manually created transactions are approved automatically in the selected period. During review, imported transactions are assigned to the period selected for approval.
- Transactions use the profile's main currency, with optional amounts in other enabled currencies: GBP for a USD profile, or vice versa. Users enter alternative amounts manually; the system performs no conversion. These amounts describe the same transaction and are excluded from summary and report totals.
- When approving an import in another currency, the user supplies an amount in the profile's main currency for calculations.
- Users can add optional comments during manual entry or approval, saved with the transaction.
- Each transaction's payment method selects from the profile's saved cards or Cash. Cash is always available but is not the default.
- Bank statement transactions (including their ID, description, date and time, amount, currency, and payment method) enter a pending review list. Their payment method is automatically selected by matching the imported credit card number to a saved card in the profile.
- Users can approve a pending transaction or split a transaction into linked approved entries assigned to different subcategories, each with independently managed tags. For example, split a restaurant bill between restaurant and "for others" subcategories, tagging only the latter entry as IOU. Each entry can have multiple tags. All split entries share one date/time, payment method, and budget period and are approved together. Each supplied currency is distributed across the entries and must reconcile independently to its total.
- Transactions can have positive or negative amounts, except Income transactions, which must be positive.
- Provide default IOU and IOU REPAYMENT tags, along with custom tags users can create, edit, and delete. Link multiple repayments to one IOU, including repayments in later periods within the same profile. Each repayment entry links to only one IOU.

### Profile Reports

- Show each Savings subcategory's current amount and goal, total savings, each Debt subcategory's remaining and paid amounts, total remaining debt, and IOU transactions with unpaid amounts.
- Show total income earned between selected start and end dates, using transaction dates.
- Show spending across all transaction dates or a selected date range, with totals filterable by category or subcategory.
- Combine records for the same category or subcategory across periods, including records from before it was renamed.

## Key Workflow

1. Set up a profile with a main currency, optional additional currencies, categories, subcategories, and saved cards. Share it with other users as needed.
2. Create a budget period and set its selections and allocations, using the previous period as a starting point when available.
3. Enter or import transactions, review and approve entries, then review period totals and profile reports.

## Scope

All core features above belong in the first version. Live bank connections are deferred; when added, they will use the same saved-card matching as statement uploads.

## Suggested Build Order

1. Create accounts and profiles with currency settings, sharing, categories, subcategories, and saved payment methods. Add budget periods with their own selections and allocations.
2. Add transaction entry, statement review, period summaries, and income, savings, debt, and IOU tracking.
3. Add profile reports with date, category, and subcategory filters.

## Definition of Done

The first version is complete when users can follow the workflow from profile setup through transaction review and reporting, with all core features available. Sharing permissions work as described, earlier periods remain accessible for correction, and summaries and reports reflect the recorded activity and allocations.
