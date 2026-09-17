# Current Task

## Status
COMPLETE

## Goal
Complete AI Project Review and Ask Project quality contracts with deterministic validation and evidence/interpretation separation.

## Completed
- Added backward-compatible `interpretation` and structured trusted-evidence fields to AI Review and Ask Project contracts.
- Added Core response validators/mappers that reject missing fields, invalid confidence values, and workflow/rule references not present in supplied evidence.
- Added safe failure/insufficient-evidence fallbacks without generating fake provider answers.
- Updated both prompts to require explicit evidence/interpretation separation while preserving technical identifiers.
- Verified minimized AI Review context keeps secrets redacted and does not include raw XAML.
- Added deterministic valid, hallucinated-reference, invalid-confidence, provider-unavailable, and fallback fixtures.
- Marked backlog lines 98 and 103 complete after 56/56 focused tests passed.
- Added scan, workflow discovery, XAML parsing, rule analysis, scoring, and total elapsed-time metrics without changing analysis behavior.
- Exposed performance metrics through the analyze API response.
- Added focused scanner/analyzer/API instrumentation regression tests.
- Benchmarked a temporary copy of the real E-Haciz project (51 workflows, 6,228 activities): five warm-run median total 307.7 ms, scan 55.1 ms, parse 42.4 ms, rules 252.0 ms, scoring 0.1 ms.
- Marked the large-project performance backlog item complete after instrumentation and real-project measurement.
- Added `UiPathReFrameworkAssessment` with backward-compatible detection, `NotDetected/Candidate/Detected` status, evidence, and Pass/Fail/Unknown checklist counts.
- Added a dedicated REFramework analyzer for canonical workflow coverage, root Main, Main State Machine, core parsing, static reachability, static invoke integrity, and Config workbook evidence.
- Preserved `IsReFramework` candidate behavior while preventing duplicate workflow names from inflating the detection signal.
- Classified dynamic Invoke Workflow reachability as Unknown instead of a false failure.
- Exposed `isReFramework` and `reFrameworkAssessment` through the analyze API response without changing existing fields.
- Added focused candidate, canonical, dynamic, malformed, broken-reference, and duplicate-name regression tests.
- Marked the REFramework detailed compliance backlog item complete after 19/19 focused tests passed.
- Extended parsed workflow arguments with backward-compatible `DefaultValue` and `HasDefaultValue` evidence.
- Added parser support for argument defaults serialized as `Default` attributes, `.Default` elements, root workflow argument bindings, expression attributes, literals, and explicit `x:Null` values.
- Preserved the distinction between an empty `InArgument` binding and an explicitly serialized default.
- Updated RPA044 to exclude unmapped In/InOut arguments that have serialized defaults while preserving static-path, runtime-dictionary, output, direction, and ambiguity safeguards.
- Updated RPA044 English/Turkish descriptions and completed both related backlog entries.
- Verified 81/81 focused parser and Variable/Argument tests.
- Added RPA046 Fixed Delays Without State-Based Wait as a workflow-level Reliability Suggestion.
- Limited RPA046 to workflows with at least two literal delays of five seconds or longer, at least one UI activity, and no detected Retry Scope or Check App State in that workflow.
- Added explainable Delay/Retry Scope/Check App State counts without producing one finding per activity.
- Registered RPA046 in API DI, the Default profile (`Weight=1`, `MaxPenalty=5`), Rule Catalog workflow scope, recommendations, and TR/EN backend localization.
- Added focused regression coverage for workflow aggregation, short/configured Delay exclusion, UI-context requirement, state-based wait aliases, profile configuration, and localization completeness.
- Marked the corresponding product backlog item complete after 71/71 focused and 471/471 full backend tests passed.
- Added stable variable scope identity (`ScopeActivityId`) to parser output and scope-aware RPA033 usage resolution for nested/shadowed variables.
- Preserved fail-safe behavior for duplicate variable names when stable scope identity is unavailable.
- Added parser and rule regression coverage for stable scope extraction, wrapper expressions, nested shadowing, and ambiguous scopes.
- Added explicit Findings severity sorting (default, high-to-low, low-to-high) in the modular `FindingsView` without changing filters, pagination, or fix flows.
- Added TR/EN labels and a focused bidirectional severity sorting regression test.
- Verified the backend after scope-aware variable analysis with 397/397 passing tests and the updated Findings UI with 46/46 passing tests.
- Inspected current repository state and identified the in-progress work as the UiPath Config analysis feature set.
- Added a Config Usage Graph tab to the existing frontend Config Analysis view using the already-returned Config usage data.
- Added English/Turkish localization keys for the usage graph.
- Added focused CSS for Config usage graph rendering and connected `panel-card` to the existing card style.
- Marked the Config Usage Graph backlog item complete.
- Refactored monolithic frontend/src/main.tsx (~4,700 lines) into modular components under frontend/src/components/ (Overview, FindingsView, HistoryView, FlowchartShared, WorkflowsView, ConfigAnalysisView, FlowchartConverterView, RulesView, DependenciesView, AskProjectPanel, SettingsView, ReportView, uiUtils) while preserving all state, contracts, test compatibility, and uncommitted local changes.
- Modularized CSS into domain stylesheets under frontend/src/styles/components/ (workflows.css, findings.css, configGraph.css, dialogs.css) imported into styles.css.
- Added resizable split panel handle to WorkflowsView with mouse drag, double-click reset, localStorage persistence, and Escape key drawer close shortcut.
- Added search clear buttons ("✕") and clear filters shortcuts to WorkflowsView and FindingsView with TR/EN localization.
- Added Hard-Coded URL detection rule (RPA035) in ExpandedStaticAnalysisRules.cs to detect unconfigured http/https endpoints (excluding schema namespaces).
- Added Circular Workflow Reference rule (RPA036) in CircularWorkflowReferenceRule.cs to detect circular invocation dependencies (stack overflow / recursion risks) via DFS cycle detection.
- Added Unused Workflow rule (RPA037) in UnusedWorkflowRule.cs to detect orphaned / uninvoked workflows unreachable from project entry points.
- Hardened UiPathProjectsController with path containment validation (IsPathSafe) to eliminate path traversal vulnerabilities on projectPath, workflowPath, configPath, and outputPath.
- Added English/Turkish localizations, profile configurations, and DI registrations for RPA035, RPA036, and RPA037.
- Updated RPA_Dev_Assistant_Product_Backlog.md marking items 35, 59, and 60 complete.
- Verified backend build (`dotnet build -m:1 /nr:false`) for Core, Infrastructure, Api, and Core.Tests with 0 warnings and 0 errors.
- Completed RPA034 caller-contract analysis: static Invoke Workflow argument mappings now count as callee argument usage across case and path-separator differences, caller-relative paths resolve correctly, and runtime `ArgumentsVariable` mappings use fail-safe suppression.
- Added focused RPA034 regression coverage for mapped/unmapped arguments, runtime mappings, and caller-relative paths; 21/21 Variable & Argument tests pass.
- Hardened RPA034 against normalized duplicate workflow paths and broadened dynamic workflow reference recognition; added multiple-caller and ambiguous-path regression coverage.
- Verified the completed caller-contract implementation with 23/23 focused tests and 403/403 full backend tests.
- Added RPA038 Argument Direction Mismatch using deterministic parsed read/write access and RPA039 Unnecessary InOut Argument with narrower In/Out recommendations.
- Registered RPA038/RPA039 in DI, the Default profile, Rule Catalog workflow scope, recommendation metadata, and TR/EN backend localization.
- Added focused read/write, direction mismatch, InOut narrowing, no-duplicate, and profile tests; 31/31 Variable & Argument tests pass.
- Applied Quality review findings: `To` is only a write target on assignment activities, static Selector text is ignored, dynamic Selector expressions remain readable, unknown Invoke mapping directions are not guessed, and CommentOut writes are excluded.
- Final validation for RPA038/RPA039 passed with 35/35 focused tests and 415/415 full backend tests.
- Added RPA040 Invalid Argument Type Declaration with deterministic validation for missing/malformed In, Out, and InOut type wrappers, including nested generic payloads.
- Registered RPA040 in DI, Default profile, Rule Catalog workflow scope, recommendations, and TR/EN localization; 43/43 focused Variable & Argument tests pass.
- Replaced RPA040's shallow regex with a balanced type-expression parser; nested generics, arrays and nullable types are accepted while empty payloads, free text and excess parentheses are rejected.
- Expanded focused Variable & Argument coverage to 49/49 tests after Quality review.
- Added RPA041 Overly Broad Variable Scope using stable scope ancestry and the narrowest shared Sequence/Flowchart/StateMachine that contains all references.
- Added RPA042 Shadowed Variable for case-insensitive nested declarations while excluding sibling and unstable scopes.
- Registered RPA041/RPA042 in DI, Default profile, Rule Catalog workflow scope, recommendations, and TR/EN localization; focused Variable & Argument tests pass 54/54.
- Hardened the variable scope graph after Quality review: duplicate/blank IDs, missing parents and parent cycles now invalidate scope analysis; variable default-expression references participate in common-scope calculation.
- Expanded focused scope regression coverage to 56/56 tests.
- Verified the full backend suite after RPA041/RPA042 hardening with 436/436 passing tests.
- Verified the complete backend regression suite after RPA040 with 429/429 passing tests.
- Added parser-level Invoke Workflow mapping direction preservation without changing the existing mapping value contract.
- Added RPA043 Invalid Invoke Workflow Argument Mapping, RPA044 Missing Invoke Workflow Argument Mapping, and RPA045 Invoke Workflow Argument Direction Mismatch.
- Reused one fail-safe static invocation resolver for project-root/caller-relative paths, path separator normalization, duplicate workflow suppression, dynamic reference suppression, and runtime `ArgumentsVariable` handling.
- Hardened contract analysis after Quality review: `{x:Null}` is not treated as a runtime dictionary, duplicate argument declarations are skipped where direction is ambiguous, and direction checks require a serialized type declaration rather than a naming-prefix guess.
- Added parser-to-rule end-to-end coverage plus unknown key, missing In/InOut, output exclusion, direction mismatch, dynamic path, runtime dictionary, null dictionary and duplicate declaration tests.
- Verified 65/65 focused parser/Variable & Argument tests and 444/444 full backend tests.
- Updated the product backlog: static mapping validation and direction mismatch are complete; missing mapping remains partial until serialized default-value evidence can distinguish optional inputs.
- Fixed Config Analysis rejecting manually selected `.xlsx` workbooks outside the UiPath project boundary; Analyze, Preview, and Generate now consistently accept the explicit workbook while service-level extension, existence, workbook-format, and no-overwrite validation remain active.
- Added an API regression test for external Config workbook selection and verified 25/25 focused Config/API tests.
- Republished the macOS ARM64 backend sidecar, restarted Tauri desktop development mode, and validated the real E-Haciz project against the selected external workbook: 174 keys, 63 unused, 18 missing, 37 hard-coded candidates, zero service messages.
- Added the dedicated TR/EN Process & PDD Analysis page with native TXT/Markdown selection, process summary, systems, ordered workflow flow, project/PDD business rules, deterministic gap comparison, filters, evidence, and read-only guidance.
- Added a process-local current-analysis store so Process/PDD analysis reuses the exact parsed project result instead of reading XAML a second time.
- Added deterministic business-rule extraction that excludes technical retry/timeout control flow, redacts evidence, normalizes system URLs to domains, and preserves PDD section/line references.
- Added focused Core and frontend tests, manually smoke-tested analyze -> PDD analysis over HTTP, and verified 462/462 backend tests, 125/125 frontend tests, frontend production build, API/Core builds, and Tauri cargo check.
- Hardened Process/PDD evidence handling so sensitive business-rule conditions are redacted before reaching the UI, with dedicated regression coverage.
- Completed Process/PDD rule coverage for BusinessRuleException, Switch outcomes, technical control-flow exclusion, low-confidence Needs Review, normal-prose PDD rules, matched PDD evidence, localized business-oriented suggestions, REFramework summary, and static invocation flow evidence.
- Combined system inventory and project business-rule extraction into one parsed-activity traversal, cached normalized PDD rule tokens, and removed false system detections caused by Invoke Workflow File and browser/application overlap.
- Added business-rule documentation-status filtering, empty states, matched PDD source evidence, static-order disclaimer, comparison invalidation tests, and locale propagation coverage.
- Localized Process/PDD validation errors and verified the endpoint with 200 valid analysis and 400 Turkish unsupported-format smoke responses.
- Made short invocation summaries explicit and non-deceptive: runtime branch order is not inferred, omitted workflow count is surfaced, and caller evidence is retained for each displayed static invocation.

## Remaining
- No implementation gap remains in this backlog item.
- Intentional limit: renamed/custom framework workflows are not guessed as canonical; dynamic invocation relationships remain Unknown unless static evidence is available.

## Next Action
Continue reviewing remaining unchecked backlog items only when requested.

## Production Readiness Partial-Item Closure (2026-09-17)
- Completed RPA006 workflow rename impact preview with static caller references and no file mutation.
- Replaced the generic RPA025 advice with a deterministic, workflow-metrics-based refactoring plan.
- Added a canonical executive summary model and localized narrative HTML/PDF rendering.
- Made RPA006, RPA031 and RPA032 naming patterns/prefixes configurable through the selected project rule profile.
- Focused rule/fix/report regression suite passed 138/138 before full repository validation.
- Added typed startup-validated runtime configuration for desktop CORS, local repositories, history retention and official package metadata access.
- Added official UiPath/NuGet metadata-backed dependency checks with cache, timeout and fail-safe behavior.
- Added a Windows 2022 NSIS release workflow with sidecar publish, artifact validation and optional timestamped signing.
- Upgraded Vitest to 5.0.1 without forced audit changes, removed the vulnerable legacy dependency chain and restored deterministic locale isolation across frontend tests.
- Reclassified local audit and local rule/profile governance as completed local desktop capabilities; central multi-user variants remain separate future scope.
- Backlog now contains 149 completed, 75 pending and zero partially completed product items.
- Windows installer acceptance remains externally blocked: the workflow is not yet present on the remote branch and the local Parallels license has expired, so Windows 10/11 installation evidence cannot be produced in this workspace.

## Rules
- Do not redo completed work.
- Preserve existing architecture.
- Update this file after meaningful progress.
