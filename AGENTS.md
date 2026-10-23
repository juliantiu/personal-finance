# Project Agent Instructions

These instructions apply throughout this repository.

## Clarify and agree before implementation

- Do not implement application code, tests, migrations, scaffolding, dependencies, or refactors unless the user explicitly asks for implementation. Requests to discuss, review, plan, or clarify are not implementation authorization.
- Before implementing, read the relevant project documents and existing code, discuss the proposed approach with the user, and establish a shared understanding of the use case, scope, expected behavior, acceptance criteria, affected layers, and testing approach.
- Ask the user whenever anything is unclear, missing, contradictory, or open to multiple interpretations. Do not silently choose requirements, business rules, architecture, technologies, defaults, or scope on the user's behalf.
- Keep assumptions to a minimum. Present unresolved choices and their implications as questions; do not treat a proposed assumption as an agreed decision.
- Once you believe mutual understanding has been reached and outstanding questions have been resolved, prepare and show the user a concrete implementation plan. Include the agreed use-case scope, intended behavior and acceptance criteria, changes across the necessary layers, implementation order, and red/green testing approach.
- After presenting the implementation plan, explicitly ask the user whether it is okay to implement that plan. Wait for the user's approval before making implementation changes; agreement during clarification or an earlier request to implement does not replace approval of the presented plan. If the user requests revisions, update the plan and ask for approval of the revised plan.
- Wait for answers to unresolved questions and approval of the implementation plan before making implementation changes. Read-only investigation and discussion may continue while waiting.
- Existing proposals, specifications, use cases, suggested build orders, and prior discussion provide context; their existence alone does not authorize implementation.
- Once the user has authorized the agreed implementation, proceed within that scope without repeatedly requesting the same approval. If new ambiguity or a required scope change arises, pause the affected work and clarify before proceeding.
- An explicit request to edit documentation, including this file, authorizes that documentation change only; it does not authorize application implementation.

## Develop vertically by use case

- Select one user-authorized use case at a time, or a smaller coherent slice of it agreed with the user.
- Implement that use case across every layer it needs: domain, application, infrastructure, and presentation or delivery interfaces. Add only the components needed for that use case.
- Do not implement the entire domain layer first, then the entire application layer, then infrastructure, or otherwise build horizontally across unrelated use cases.
- Keep any foundational setup to the minimum needed for the approved use case. Discuss prerequisites that would expand the agreed scope before implementing them.
- Complete and verify the use case and its corresponding tests before moving to another use case. Completion does not authorize starting the next one.
- Avoid speculative abstractions, unused components, unrelated cleanup, and functionality intended only for future use cases.

## Domain-driven design and Clean/Onion architecture

- Model business concepts and behavior using the project's agreed domain language. Clarify unclear terminology and business rules with the user.
- Define entities, value objects, aggregates, and domain services where the use case warrants them. Enforce business invariants within the domain and respect aggregate and bounded-context boundaries.
- Keep domain logic independent of UI frameworks, databases, ORMs, network clients, and other infrastructure concerns.
- Use the application layer to orchestrate use cases and coordinate domain behavior through explicit ports or interfaces. Keep business rules in the domain rather than duplicating them in controllers, persistence code, or UI components.
- Direct source-code dependencies inward: outer layers depend on application and domain abstractions; inner layers must not depend on outer implementations.
- Place persistence and external-service adapters in infrastructure. Implement the interfaces required by the inner layers and connect concrete implementations at the composition root.
- Keep presentation or delivery adapters focused on input/output translation, boundary validation, and invoking application use cases.
- Use clear names, cohesive responsibilities, small focused units, and explicit boundaries. Prefer the simplest design that satisfies the agreed use case without weakening domain rules or architecture.

## Test each use case with red/green cycles

- Develop the tests alongside each vertical use case, before its production behavior, so a genuine red/green cycle is possible. Do not defer testing until multiple use cases or whole layers are implemented.
- Derive tests from the agreed acceptance criteria, business invariants, failure paths, and relevant boundary cases.
- **Red:** write a focused test for the next required behavior and run it. Confirm it fails because that behavior is absent or incorrect. An unrelated setup or environment failure does not demonstrate the intended red state.
- **Green:** implement the minimum behavior across the necessary layers and rerun the test until it passes.
- **Refactor:** improve clarity and structure within the approved scope while keeping tests green. Repeat the cycle for the next behavior in the same use case.
- Use domain unit tests for business rules, application tests for orchestration, integration tests for infrastructure boundaries, and end-to-end or acceptance tests where needed to verify the use case through its entry point. Choose coverage appropriate to the behavior; do not add empty or redundant tests simply to cover every layer.
- Verify observable behavior and meaningful outcomes. Do not weaken tests to make an incorrect implementation pass.
- Before declaring the use case complete, run its corresponding tests and relevant existing regression checks. Report what was tested, the observed red and green results, and any failures or checks that could not run. Never claim tests passed without running them.

## Project reference documents and resources

Before proposing or implementing changes, list the version directories directly under `docs/` and select `docs/v{version_number}/` with the highest numeric version. Compare dot-separated version components numerically (for example, `v1.10` is later than `v1.9`, and `v2.0` is later than `v1.10`); do not use alphabetical order or modification dates. Resolve this version each time project references are needed rather than hardcoding a version.

Look for all project documents and resources within the selected `docs/v{version_number}/` directory, including its subdirectories. Inspect the available materials and read those relevant to the requested work; do not rely on a fixed list of filenames.

If the version cannot be determined unambiguously or information needed for the requested work is missing from the latest version, ask the user before proceeding. Do not silently fall back to an older version or combine documents and resources from different versions.

Treat documented open decisions as unresolved. Ask the user to resolve conflicts or gaps instead of inventing behavior. Use the current applicable documents rather than historical snapshots unless the user requests historical context.
