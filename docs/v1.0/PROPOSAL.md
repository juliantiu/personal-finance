# Budget and Transaction Tracker

## Overview

A budgeting app where users manage multiple budget profiles. Each profile defines categories and subcategories and contains budget periods that reference selected items, with their own dates, allocations, and financial activity. Profiles can be shared as editable or view-only.

## Goals

- Track planned and actual income, spending, savings, debt, and IOUs.
- Keep previous budget periods available for review and correction.
- Support manual transaction entry and bank statement upload with transaction review.

## Users and Usage

Each user account can access multiple profiles. The original owner alone can share a profile or change access. An editor can change the whole profile, including all periods, categories, and subcategories; view-only users can view it.

## Core Features

### Profiles and Budget Periods

- Users create and manage categories and subcategories within a budget profile. Savings, Debt, and Income categories are created automatically when the profile is created. Allocation amounts are set separately for each budget period.
- Each category and subcategory has an optional comment field that users can view and edit.
- Each profile can save credit cards as payment methods, identified by their card numbers, for use across its budget periods.
- Each profile has a control to create a budget period with a start and end date. For the first period, users select which profile categories and subcategories to reference and set an allocation for each selected subcategory. A category's allocation is the sum of its selected subcategory allocations.
- Later periods start with references to the same profile categories and subcategories used in the previous period, and copies of its allocation amounts. Spending starts at zero. Users can adjust the selections and allocations for the new period without changing previous periods.
- Previous periods remain available to view and edit.
- Renaming a category or subcategory updates the name of the same profile item. All budget periods continue to reference it, preserving allocations, transactions, history, and any savings or debt balances. Removing a subcategory preserves its history and stops future tracking.

### Income, Savings, and Debt

- Income subcategories track expected and received income.
- Each period shows total income received, total allocated, and total spending. It also shows allocated allowance remaining (allocations minus spending) and income remaining after spending (income minus spending).
- Savings subcategories track current balances, goals, reset periods, and money added or withdrawn in each period. Carry balances forward between periods. Show when a reset period is due, but do not reset automatically. Balances and withdrawals remain editable.
- Debt subcategories track amounts paid and remaining. Carry totals forward between periods; calculated amounts remain editable.

### Transactions and IOUs

- Users can create, edit, and delete transactions. Manually created transactions are approved automatically in the selected period. During review, imported transactions are assigned to the period selected for approval.
- Users can write optional comments when manually entering or approving a transaction. Comments are saved with the transaction.
- Each transaction has a payment method field that selects from the profile's saved credit cards. Cash is always an available option but is not selected by default.
- Bank statement transactions (including their ID, description, date and time, amount, and payment method) enter a pending review list. Their payment method is automatically selected by matching the imported credit card number to a saved card in the profile.
- Users can approve a pending transaction or split a transaction into linked approved entries assigned to different subcategories. Each entry can have multiple tags, managed separately. For example, part of a restaurant bill can go to a restaurant subcategory and the portion paid for others can go to another subcategory and be tagged IOU.
- Transactions can have positive or negative amounts, except Income transactions, which must be positive.
- Provide default IOU and IOU REPAYMENT tags, along with custom tags users can create, edit, and delete. Link multiple repayments to one IOU, including repayments in later periods within the same profile.

### Profile Reports

- Show each Savings subcategory's current amount and goal, total savings, each Debt subcategory's remaining and paid amounts, total remaining debt, and IOU transactions with unpaid amounts.
- Show total income earned between selected start and end dates, using transaction dates.
- Show spending across all transaction dates or a selected date range, with totals filterable by category or subcategory.
- Combine records for the same category or subcategory across periods, including records from before it was renamed.

## Key Workflow

1. Set up a profile with categories, subcategories, and saved cards. Share it with other users as needed.
2. Create a budget period and set its selections and allocations, using the previous period as a starting point when available.
3. Enter or import transactions, review and approve entries, then review period totals and profile reports.

## Scope

All core features above belong in the first version. Live bank connections are deferred; when added, they will use the same saved-card matching as statement uploads.

## Suggested Build Order

1. Create accounts, profiles, sharing, profile categories and subcategories, and saved payment methods. Add budget periods that select from those categories and subcategories and set their own allocations.
2. Add transaction entry, statement review, period summaries, and income, savings, debt, and IOU tracking.
3. Add profile reports with date, category, and subcategory filters.

## Definition of Done

The first version is complete when users can follow the workflow from profile setup through transaction review and reporting, with all core features available. Sharing permissions work as described, earlier periods remain accessible for correction, and summaries and reports reflect the recorded activity and allocations.
