# RPA Dev Assistant — Codex Multi-Agent Instructions

## Project

RPA Dev Assistant is a desktop application for reviewing and analyzing RPA projects, primarily UiPath projects.

The application supports Turkish and English.

---

# 1. Agent Architecture

The project uses a scoped multi-agent structure.

The main agent acts as the coordinator and should delegate specialized work to the appropriate sub-agent whenever doing so reduces repository context, isolates responsibilities, or avoids unrelated modifications.

Recommended structure:

```text
/
├─ AGENTS.md
│
├─ .agents/
│  ├─ analysis/
│  │  └─ AGENT.md
│  │
│  ├─ ui/
│  │  └─ AGENT.md
│  │
│  ├─ quality/
│  │  └─ AGENT.md
│  │
│  └─ architecture/
│     └─ AGENT.md
│
├─ src/
├─ tests/
└─ ...
```

Initial agents:

```text
Main / Coordinator Agent
│
├─ Analysis Agent
│  ├─ XAML analysis
│  ├─ Rule Engine
│  ├─ Rule Catalog
│  ├─ Dependency / Package analysis
│  ├─ Workflow structure analysis
│  └─ Flowchart / Sequence analysis
│
├─ UI Agent
│  ├─ Desktop UI
│  ├─ Components
│  ├─ Localization
│  └─ User interaction
│
├─ Quality Agent
│  ├─ Unit tests
│  ├─ Integration tests
│  ├─ Regression checks
│  └─ Implementation review
│
└─ Architecture Agent
   ├─ Cross-module changes
   ├─ API contracts
   ├─ Shared models
   └─ Shared infrastructure
```

Do not introduce additional agents unless a domain becomes large enough to justify independent repository context.

---

# 2. Main Agent Responsibilities

The main agent is responsible for task coordination.

Before implementing a task:

1. Understand the requested behavior.
2. Identify the affected domain.
3. Determine whether the task should be handled directly or delegated.
4. Identify the minimum repository context required.
5. Delegate only to agents whose domain is directly relevant.
6. Combine and validate results without performing unnecessary repository-wide analysis.

The main agent may delegate specialized work when delegation
reduces context or isolates a meaningful domain.

Do not delegate small tasks that can be completed safely
with the current context.

The main agent must not ask every sub-agent to inspect every task.

Example:

```text
Task:
Add dependency/package analysis.

Use:
Analysis Agent
Quality Agent when tests are required.

Do not use:
UI Agent unless UI changes are explicitly required.
Architecture Agent unless shared contracts must change.
```

Another example:

```text
Task:
Add filters to the Rules screen.

Use:
UI Agent

Use Analysis Agent only if analyzer behavior or rule data contracts must change.
```

---

# 3. Delegation Rules

Delegate when at least one of the following applies:

* the task belongs clearly to a specialized domain
* multiple independent areas can be inspected separately
* repository context would otherwise become unnecessarily large
* implementation and validation can be isolated
* a dedicated agent can operate on a smaller set of files

Do not delegate merely because an agent exists.

Avoid:

```text
Main Agent
↓
reads entire repository

Analysis Agent
↓
reads entire repository

UI Agent
↓
reads entire repository

Quality Agent
↓
reads entire repository
```

Prefer:

```text
Main Agent
↓
identifies affected module

Analysis Agent
↓
reads only analyzer-related files

Quality Agent
↓
reads changed implementation + relevant tests
```

---

# 4. Core Rules

All agents must follow these rules.

* Preserve the existing architecture.
* Prefer modifying existing code instead of introducing new abstractions.
* Do not refactor unrelated code.
* Do not rename unrelated files, classes, methods, variables, routes, or components.
* Do not change existing API contracts unless the task explicitly requires it.
* Do not introduce new dependencies unless necessary.
* Reuse existing utilities, services, components, models, and patterns whenever possible.

---

# 5. Context Efficiency

All agents must use the minimum repository context necessary to complete their assigned task.

Do not scan the entire repository by default.

# Context Budget

Default exploration budget:
- Start with no more than 3–5 directly relevant files.
- Expand scope only when a concrete dependency requires it.
- Do not perform repository-wide discovery commands unless necessary.
- Do not ask multiple agents to inspect the same files.
- Do not delegate simple single-domain changes.
- Prefer one focused sub-agent over multiple overlapping sub-agents.

Start from:

1. files explicitly mentioned in the task
2. files named by the coordinating agent
3. the closest existing implementation
4. direct imports or references



Follow additional references only when necessary.

Do not repeatedly inspect files whose relevant behavior is already understood.

Do not read large documentation files unless directly relevant.

Do not analyze unrelated features to understand the current task.

Avoid generated/dependency directories unless explicitly required:

```text
node_modules/
bin/
obj/
dist/
build/
coverage/
.next/
.cache/
logs/
temp/
tmp/
test-results/
generated files
package caches
compiled assets
```

Large sample UiPath projects and fixture projects should only be inspected when the current task explicitly requires them.

Sub-agents must not independently perform broad repository discovery unless their assigned task cannot otherwise be completed.

---

# 6. Implementation

Before writing new code:

1. Locate the existing implementation related to the task.
2. Identify the smallest set of files that need modification.
3. Reuse the current architectural pattern.
4. Implement only the requested behavior.

Avoid speculative architecture changes.

Do not create:

* unnecessary wrapper classes
* duplicate helper methods
* duplicate services
* generic abstractions used only once
* new state-management layers when the existing solution is sufficient

If implementation requires a cross-domain architectural change, escalate that portion to the Architecture Agent rather than introducing the change silently.

---

# 7. UI

UI-related work should normally be handled by the UI Agent.

Follow the existing RPA Dev Assistant design system.

* Keep screens simple and desktop-oriented.
* Reuse existing components.
* Preserve spacing, typography, layout, navigation, and interaction patterns.
* Do not redesign unrelated screens while implementing a feature.
* User-facing text must support Turkish and English.
* Do not hardcode translated UI text if the project already has a localization system.

UI Agent should not modify analyzer behavior unless the task explicitly requires it.

---

# 8. RPA / UiPath Terminology

All agents must preserve established technical terminology where appropriate.

Examples:

* Workflow
* Sequence
* Flowchart
* State Machine
* Selector
* Activity
* Queue
* Transaction
* Orchestrator
* Asset
* Business Rule
* Business Exception
* System Exception
* XAML

Do not translate:

* technical identifiers
* enums
* property names
* API fields
* code symbols

---

# 9. Backend / Analyzer Rules

Analyzer-related work should normally be handled by the Analysis Agent.

Analyzer logic should:

* produce deterministic results where possible
* avoid duplicate findings
* reuse existing finding/result models
* preserve existing severity conventions
* include sufficient information for the UI to identify the affected workflow/file
* avoid silently changing existing rule semantics

Do not modify unrelated analyzer rules while implementing another analyzer.

Prefer extending existing analyzer pipelines over introducing parallel analysis systems.

---

# 10. Tests and Validation

Testing should normally be handled by the Quality Agent after implementation when meaningful validation is required.

Run only tests relevant to the changed area first.

Do not run the entire test suite unless:

* the task explicitly requests it
* shared infrastructure changed
* targeted tests cannot provide sufficient confidence

When adding behavior, update or add relevant tests when appropriate.

Do not rewrite unrelated tests.

The Quality Agent should inspect:

```text
changed implementation
+
direct dependencies when necessary
+
relevant tests
```

It should not perform another broad repository analysis unless required.

---

# 11. Architecture Changes

Architecture Agent should only be used when the task affects shared project contracts.

Examples:

* shared API models
* analyzer/UI contracts
* cross-module infrastructure
* application-wide dependency changes
* shared persistence models
* shared navigation architecture
* breaking interface changes

Architecture Agent should not redesign the project proactively.

Its role is to find the smallest safe architectural change required by the task.

Any breaking contract change must be explicitly justified by the task.

---

# 12. Documentation

Do not create documentation unless:

* explicitly requested
* required to explain a new architectural contract
* required by an existing project convention

Do not generate large implementation summaries inside repository files.

Agent instructions belong inside the agent configuration files rather than unrelated project documentation.

---

# 13. Agent Communication

Sub-agents should return concise implementation-oriented results to the coordinating agent.

Preferred result format:

```text
Scope:
Files inspected.

Changes:
Files changed and behavior implemented.

Validation:
Tests/checks performed.

Risks:
Only relevant unresolved concerns.
```

Do not send large file dumps to the coordinating agent.

Do not reproduce source files unless necessary.

Prefer passing:

* conclusions
* changed file paths
* contract implications
* validation results

over full repository context.

---

# 14. Final Response

The main agent owns the final user response.

Keep the final response concise.

Return only:

## Implemented

Short summary of what changed.

## Changed Files

List of modified/created files.

## Validation

Relevant tests/build/checks executed.

## Notes

Only blockers, assumptions, or remaining issues that materially matter.

Do not:

* reproduce large code blocks
* explain every implementation detail
* summarize files that were not changed
* repeat the task description
* provide long architectural explanations unless requested

