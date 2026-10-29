---
name: uc-phase-implementation
description: Find and implement the next incomplete use case phase in this project, following its current documentation, approval process, architecture, and test requirements.
---

# Use Case Phase Implementation

Use this skill when asked to implement the next phase of a documented use case in this project.

## Identify the use case and phase

1. Read the repository's `AGENTS.md` and follow its instructions.
2. Identify the use case from the branch name when it is clear. If it is not clear, ask the user which use case to work on.
3. Resolve the latest version under `docs/` using the numeric version ordering required by `AGENTS.md`. Inspect the documents and resources in that version only.
4. Read the selected use case's implementation plan and related specifications. Identify the first phase that is not marked complete. Treat documented open decisions as unresolved.
5. If the use case, current phase, requirements, or expected behavior is unclear, ask the user. Do not infer missing business rules or scope.

## Agree before changing application code

Read the relevant existing code and discuss the intended behavior, scope, acceptance criteria, affected layers, and verification approach with the user. Present a concrete implementation plan and ask for approval before changing application code, tests, dependencies, migrations, or scaffolding.

A prior request to implement a phase does not replace approval of the presented plan. Once the user approves it, stay within that scope; clarify any newly discovered ambiguity or required scope change.

## Implement and verify one phase

Implement only the approved phase and its necessary vertical slice. Keep dependencies directed inward and avoid speculative abstractions or unrelated cleanup.

For each required behavior, write and run a focused test first, confirm it fails for the intended reason, then implement the minimum behavior and rerun it. Refactor only while the relevant tests remain green. Use the test types appropriate to the behavior and keep integration and browser test data synthetic.

Do not claim checks passed unless they were run. Report the changes, how the layers work together, the red/green results, and any checks that could not run. Update the implementation plan's phase status only when the work and its required verification are complete. Pause after the phase for the user's questions; continue to another phase only when the user indicates they are ready.
