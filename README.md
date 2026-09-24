# RPA Dev Assistant

RPA Dev Assistant is a local developer assistant for RPA engineers. The long-term goal is to grow into a modular product that can inspect UiPath projects, review implementation quality, generate documentation, support migrations, create tests, and produce quality reports.

## MVP Scope

The first version includes a UiPath Project Scanner. Given a local UiPath project folder, it extracts basic project metadata and workflow information without requiring login, a database, cloud services, a frontend, or any LLM integration.

Current scanner capabilities:

- Checks whether the target folder exists.
- Checks whether `project.json` exists.
- Parses `project.json` and reads project name, compatibility, and dependencies where available.
- Recursively finds `.xaml` workflows.
- Returns workflow count and project-relative workflow paths.
- Parses workflow activity metadata from XAML files.
- Returns total activity count and activity type counts.
- Returns basic folder information.
- Estimates whether the project resembles UiPath REFramework using workflow-name heuristics.
- Reports expected scan problems through errors and warnings.

## UiPath XAML Activity Analysis

The scanner delegates XAML parsing to `IUiPathXamlParser`. This keeps project scanning separate from workflow parsing and prepares the codebase for later rule engine and code review modules.

The parser uses XML/XAML parsing through `System.Xml.Linq`; it does not parse workflows with regular expressions. It extracts:

- Activity name, display name, type name, XML namespace, parser activity path, and UiPath `IdRef` when available.
- Parent activity id and depth for workflow hierarchy.
- Generic activity properties from XML attributes, while skipping common XAML designer metadata.
- Workflow arguments from `x:Members` / `x:Property` declarations where available.
- Per-workflow parse errors and warnings so one broken XAML file does not stop the full project scan.

Known parser limits:

- Stable activity lookup is best when UiPath `sap2010:WorkflowViewState.IdRef` is present. Parser activity paths are deterministic for unchanged XAML structure but can change when activities are inserted, removed, or reordered.
- UiPath Studio versions can emit different XAML shapes, so argument extraction is best-effort.
- Nested property elements are used for hierarchy traversal, but designer-only metadata is intentionally ignored.
- This is not a best-practice analyzer, AI review, or report generator yet.

## Static Analysis

RPA Dev Assistant includes a deterministic UiPath static analysis rule engine. The analysis flow is:

```text
UiPathProjectScanner
↓
IUiPathXamlParser
↓
IUiPathRuleEngine
↓
UiPathStaticAnalysisResult
↓
UiPathQualityScore
```

### UiPath compatibility behavior matrix

Runtime compatibility and design experience are resolved as separate dimensions. `Windows` / `Windows-Legacy` describe the runtime target, while `Modern` / `Classic` describe activity usage detected from parsed workflows. This prevents compatibility-sensitive rules from relying on broad string matching.

| Project signal | Runtime behavior | Activity behavior | Legacy UI activity rule (RPA016) |
| --- | --- | --- | --- |
| Windows | Modern and Classic activities are supported | Modern, Classic, Mixed, or Unknown is derived from activities | Runs because Windows is a modern-capable runtime |
| Windows-Legacy | Classic activities are supported; modernization is a migration concern | Defaults to Classic when no stronger evidence exists | Suppressed to avoid per-activity migration noise |
| Modern | Modern design experience is explicit or detected | Modern UI automation is preferred | Legacy UI activities are reported |
| Classic | Classic design experience is explicit or detected | Classic activities are treated as intentional | Suppressed; migration can be reviewed separately |

Cross-platform/Portable metadata is also recognized. It supports modern activities and treats detected Classic UI activities as incompatible migration candidates. Unknown metadata remains conservative and does not create compatibility-only findings without activity evidence.

Rules implement `IUiPathAnalysisRule` and are registered through dependency injection. Each rule receives a project analysis context and returns findings independently. This keeps the engine ready for future rule enable/disable flags, severity overrides, company-specific rules, custom configuration, and rule profiles without binding the engine to a hard-coded switch statement.

Severity levels:

- `Info`
- `Suggestion`
- `Warning`
- `Error`
- `Critical`

Current rules:

| ID | Name | Category | Severity | Description |
| --- | --- | --- | --- | --- |
| RPA001 | Avoid Delay Activities | Reliability | Warning | Detects fixed Delay activities. |
| RPA002 | Empty Catch Block | ExceptionHandling | Error | Detects Catch blocks with no executable activity. |
| RPA003 | Exception Silently Swallowed | ExceptionHandling | Error | Detects Catch blocks without logging, Throw, or Rethrow. |
| RPA004 | Long Hard-Coded Delay | Performance | Warning | Detects Delay durations of 5 seconds or more. |
| RPA005 | Invalid Invoke Workflow Reference | Architecture | Error | Detects Invoke Workflow File references that do not resolve inside the project. |
| RPA006 | Workflow Naming Convention | Naming | Suggestion | Detects generic/default workflow filenames. |
| RPA007 | Generic Activity Display Name | Maintainability | Suggestion | Detects generic activity display names on common activities. |
| RPA008 | Workflow Has No Logging | Logging | Suggestion | Detects non-trivial workflows without Log Message activities. |
| RPA009 | Hard-Coded Credential-Like Value | Security | Critical | Detects literal credential-like values in sensitive properties. |
| RPA010 | Potential Sensitive Data Logging | Security | Error | Detects Log Message expressions that appear to log sensitive values. |
| RPA011 | Excessive UI Timeout | Performance | Warning | Detects hard-coded UI timeouts of 120000 ms or more. |
| RPA012 | Unrealistically Low UI Timeout | Reliability | Warning | Detects hard-coded UI timeouts below 1000 ms, while skipping ambiguous zero values. |
| RPA013 | ContinueOnError Enabled | ExceptionHandling | Warning | Detects executable activities with ContinueOnError enabled. |
| RPA014 | Excessive ContinueOnError Usage | Reliability | Error | Detects workflows with repeated ContinueOnError usage. |
| RPA015 | Missing Explicit Timeout on Critical UI Activity | Reliability | Suggestion | Conservatively detects legacy UI activities with selectors and no explicit timeout. |
| RPA016 | Legacy UI Automation Activity | UiAutomation | Suggestion | Detects legacy UI automation activities in modern/Windows projects. |
| RPA017 | Selector Uses idx Attribute | UiAutomation | Warning | Detects selectors that depend on positional idx attributes. |
| RPA018 | Potentially Unstable Selector Attribute | UiAutomation | Warning | Detects selectors with high-confidence generated numeric or GUID identifiers. |
| RPA019 | Overly Complex Selector | Maintainability | Suggestion | Detects very long selector literals. |
| RPA020 | Hard-Coded Absolute File Path | Configuration | Warning | Detects hard-coded absolute local or UNC paths. |
| RPA021 | Hard-Coded Email Address | Configuration | Suggestion | Detects literal recipients in mail-related activities. |
| RPA022 | HTTP Request Without Explicit Timeout | Reliability | Warning | Detects HTTP Request activities without a valid explicit timeout. |
| RPA023 | HTTP Request Without Local Error Handling | ExceptionHandling | Warning | Detects HTTP Request activities outside a local TryCatch hierarchy. |
| RPA024 | Excessive Workflow Arguments | Architecture | Suggestion | Detects workflows with more than 10 arguments. |
| RPA025 | Large Workflow | Maintainability | Warning | Detects workflows over 100 executable activities or hierarchy depth over 12. |
| RPA026 | Possibly Unused Dependency | Configuration | Suggestion | Detects known UiPath package families that have no mapped parsed activity usage. |
| RPA027 | Package Version Alignment Risk | Architecture | Warning | Detects significant major-version alignment gaps between modern UiPath packages. |
| RPA028 | Mixed Modern and Classic Activity Usage | UiAutomation | Info | Detects projects that contain both modern and classic UI automation activity signals. |
| RPA029 | Legacy Package Indicator | Architecture | Suggestion | Detects declared packages that match known legacy/classic package indicators. |

Static analysis coverage includes exception handling, logging, UI automation, selector quality, security, configuration, HTTP reliability, workflow architecture, maintainability, and performance.

Reusable analysis helpers live in Core for expression classification, case-insensitive property lookup, selector analysis, workflow metrics, and centralized thresholds. Rule implementations use these helpers instead of re-parsing XAML or duplicating fragile string logic.

Rule metadata is available through:

```bash
curl http://localhost:5000/api/uipath/rules
```

## Dependency / Package Analysis

Dependency analysis is offline-first and uses `project.json` plus the already-parsed in-memory workflow/activity model. It does not query NuGet, UiPath Marketplace, Orchestrator, or the internet, and it does not re-read every XAML file per rule.

Dependency flow:

```text
project.json dependencies
↓
Parsed workflow/activity metadata
↓
IUiPathPackageActivityMapper
↓
IUiPathDependencyAnalyzer
↓
UiPathDependencySummary
↓
Rules, reports, UI, Ask Project, custom rules
```

The mapper keeps package-to-activity knowledge in one place. Known package families include System, UI Automation, Excel, Mail, WebAPI, Database, Credentials, Orchestrator/Persistence, Document Understanding/OCR, and PDF. Matching uses package family, activity namespace fragments, and normalized activity names.

Usage statuses:

| Status | Meaning |
| --- | --- |
| Used | At least one parsed activity maps to the package family. |
| PossiblyUnused | A known UiPath package family is declared, but no parsed activity maps to it. |
| Unknown | The package cannot be safely mapped, usually custom or third-party packages. |

Unknown custom packages are intentionally not reported as unused. They may be used from custom activities, `Invoke Code`, runtime loading, or vendor-specific activity names that are not in the built-in mapper yet.

Dependency-related rules:

| ID | Name | What It Checks |
| --- | --- | --- |
| RPA026 | Possibly Unused Dependency | Known UiPath packages with no mapped activity usage. |
| RPA027 | Package Version Alignment Risk | Significant major-version gaps across modern UiPath package versions. |
| RPA028 | Mixed Modern and Classic Activity Usage | Modern and classic UI automation activity signals in the same project. |
| RPA029 | Legacy Package Indicator | Known legacy/classic package family indicators. |

Version risk analysis is conservative. The current MVP can detect local alignment risks from declared versions, but it does not know whether a package is latest, deprecated, vulnerable, or superseded. Those checks require a future package metadata source.

The analyze response includes `dependencyAnalysis`:

```json
{
  "dependencyAnalysis": {
    "totalDependencies": 6,
    "uiPathDependencies": 6,
    "thirdPartyDependencies": 0,
    "usedDependencies": 5,
    "possiblyUnusedDependencies": 1,
    "potentialConflicts": 0,
    "legacyIndicators": 0,
    "modernClassicMode": "Classic",
    "packages": []
  }
}
```

HTML reports include the same dependency summary and package table. JSON export keeps canonical field names such as `projectName`, `findings`, `qualityScore`, and `dependencyAnalysis`.

Ask Project can answer dependency questions locally without AI, for example:

- `Hangi package'lar kullanılmıyor olabilir?`
- `Which workflows use UiPath.Excel.Activities?`
- `Modern / Classic activity mode nedir?`

To add a new rule:

1. Create a class implementing `IUiPathAnalysisRule`.
2. Give it a stable rule id, severity, category, description, and recommendation-oriented findings.
3. Register it as `IUiPathAnalysisRule` in the host dependency injection setup.
4. Add focused unit tests for both finding and non-finding cases.

## Quality Scoring

Quality scoring is separate from rule detection. Rules produce findings; profiles configure which rules matter and how much they affect the final score; the scoring engine converts findings into an explainable score.

Scoring flow:

```text
Rule
↓
Finding
↓
Rule Configuration / Profile
↓
IUiPathQualityScoringEngine
↓
UiPathQualityScore
```

Default formula:

```text
effectiveSeverity = ruleConfiguration.severityOverride ?? finding.severity
findingPenalty = ruleWeight * severityMultiplier
ruleRawPenalty = findingCount * findingPenalty
ruleAppliedPenalty = min(ruleRawPenalty, maxPenalty)
rawPenalty = sum(ruleAppliedPenalty)
projectSizeFactor = clamp(sqrt(max(1, workflowCount + totalActivityCount / 10)), 1, 5)
normalizedPenalty = rawPenalty / projectSizeFactor
score = clamp(100 - round(normalizedPenalty), 0, 100)
```

`Weight` is the base penalty for each finding of a rule. `MaxPenalty` prevents one noisy rule from dominating the score. Normalization makes the same number of findings less severe in a large project than in a tiny project, while keeping both raw and normalized penalty visible.

Aggregated findings are scored by their visible finding count, while occurrence count remains available for explainability. For example, RPA007 generic `DisplayName` issues are grouped at workflow level: the UI may show 50 findings while preserving 1,694 affected activity occurrences in the finding details and score breakdown. The default profile still caps RPA007 at `MaxPenalty = 10`, so one noisy naming rule cannot dominate a real project score.

The score response also includes:

- `projectSizeFactor`: the normalization factor used for the project.
- `scoreBreakdown`: per-rule finding count, occurrence count, weight, raw penalty, applied penalty, and max penalty.
- `severityBreakdown`: grouped raw/applied penalties by severity.

Severity multipliers:

| Severity | Multiplier |
| --- | ---: |
| Info | 0 |
| Suggestion | 0.5 |
| Warning | 1 |
| Error | 1.5 |
| Critical | 2 |

Grade ranges:

| Score | Grade |
| --- | --- |
| 90-100 | A |
| 80-89 | B |
| 70-79 | C |
| 60-69 | D |
| 0-59 | F |

## Workflow Complexity

Workflow complexity metrics are calculated from the parsed in-memory workflow/activity tree. The scanner does not re-read XAML files for every rule. These metrics are used by RPA025, the Workflows UI, the report exports, and local Ask Project answers.

Complexity inputs:

- total activities
- executable activities
- container activities
- maximum nesting depth
- decision count (`If`, `Switch`, flow decisions)
- loop count
- `Try Catch` count
- `Invoke Workflow File` count
- argument count
- finding count

Default complexity formula:

```text
complexityScore =
  executableActivityCount
  + max(0, maxNestingDepth - 3) * 4
  + decisionCount * 3
  + loopCount * 4
  + tryCatchCount * 2
  + max(0, argumentCount - 5) * 2
  + min(invokeWorkflowCount, 10)
```

Default levels:

| Level | Score |
| --- | ---: |
| Low | 0-39 |
| Medium | 40-89 |
| High | 90-159 |
| Very High | 160+ |

RPA025 (`Large Workflow`) now uses both executable activity count and complexity level. A workflow can be flagged when it is structurally complex even if it is below the raw activity-count threshold.

## Flowchart Conversion Analysis

RPA Dev Assistant can detect Flowchart-based UiPath workflows, produce a Sequence conversion preview, and apply only conversions classified as `Safe`. Risky or unsupported Flowcharts remain preview-only. Apply is controlled, single-workflow only, and always uses stale-file protection, backup creation, post-write validation, and rollback support.

Flowchart analysis flow:

```text
UiPath Workflow
↓
Structure detection
↓
Flowchart graph analysis
↓
Convertibility assessment
↓
Conversion plan
↓
Semantic Sequence preview
↓
Controlled apply for Safe conversions only
↓
Backup / validation / rollback
```

Workflow structure types:

| Type | Meaning |
| --- | --- |
| Sequence | The workflow root is a Sequence. |
| Flowchart | The workflow root is a Flowchart. |
| StateMachine | The workflow root is a State Machine. |
| Mixed | Multiple root-level structure types were detected. |
| Unknown | The structure could not be determined. |

The Flowchart graph model includes nodes, edges, decisions, switches, cycle detection, unreachable-node detection, entry/exit counts, merge count, and maximum path depth. FlowDecision nodes are represented as decision nodes with True/False branches. FlowSwitch nodes are represented with Case/Default branches when those relationships are visible in XAML.

Conversion assessment levels:

| Level | Meaning |
| --- | --- |
| Safe | Acyclic linear or simple structured Flowchart that can be previewed as Sequence/If/Switch. |
| RequiresReview | Previewable, but branch, merge, or switch semantics should be reviewed in UiPath Studio. |
| Complex | Cycles or control-flow patterns exist; no automatic loop conversion is inferred. |
| NotSupported | Required graph information or supported node patterns are missing. |

Safe conversion criteria are intentionally conservative. Linear Flowcharts become a Sequence preview. Simple FlowDecision patterns become an If preview. FlowSwitch patterns become a Switch preview. Shared merge nodes are treated as continuation nodes after branches so they are not duplicated in each branch.

Unsupported patterns include cycles, ambiguous shared subgraphs, unreachable nodes that affect behavior, unknown flow node types, and State Machine conversion. Cycles are never guessed as While or Do While; the tool only reports that manual review is required.

Preview endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/workflows/flowchart-conversion/analyze \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "workflowPath": "Framework/SetTransactionStatus.xaml"}'
```

Controlled apply endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/workflows/flowchart-conversion/apply \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "workflowPath": "Framework/SetTransactionStatus.xaml", "expectedWorkflowHash": "...", "confirmed": true}'
```

Rollback endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/workflows/flowchart-conversion/rollback \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "workflowPath": "Framework/SetTransactionStatus.xaml", "backupId": "...", "expectedCurrentHash": "..."}'
```

The API never accepts generated XAML, arbitrary file content, or arbitrary restore paths from the frontend. The backend regenerates the conversion plan from the current workflow before writing. A stale hash mismatch blocks apply, and rollback only accepts backups created by RPA Dev Assistant for Flowchart conversion.

Example preview shape:

```text
Sequence
└── If - Success
    Condition: [in_BusinessRuleException is Nothing and in_SystemError is Nothing]
```

Ask Project can answer Flowchart questions locally without AI:

- `Hangi workflow'lar Flowchart kullanıyor?`
- `Kaç Flowchart workflow var?`
- `Which Flowchart workflows are safe to convert to Sequence?`
- `Hangi Flowchart'larda cycle var?`

HTML reports include a `Flowchart Analysis` / `Flowchart Analizi` section with Flowchart workflow count, safe conversion candidates, review-required candidates, complex/not-supported counts, and candidate rows.

Default rule profile:

| Rule | Enabled | Severity Override | Weight | Max Penalty |
| --- | --- | --- | ---: | ---: |
| RPA001 Avoid Delay Activities | true |  | 2 | 10 |
| RPA002 Empty Catch Block | true |  | 10 | 30 |
| RPA003 Exception Silently Swallowed | true |  | 12 | 30 |
| RPA004 Long Hard-Coded Delay | true |  | 4 | 12 |
| RPA005 Invalid Invoke Workflow Reference | true |  | 12 | 30 |
| RPA006 Workflow Naming Convention | true |  | 1 | 5 |
| RPA007 Generic Activity Display Name | true |  | 1 | 10 |
| RPA008 Workflow Has No Logging | true |  | 2 | 10 |
| RPA009 Hard-Coded Credential-Like Value | true |  | 15 | 30 |
| RPA010 Potential Sensitive Data Logging | true |  | 10 | 20 |
| RPA011 Excessive UI Timeout | true |  | 3 | 10 |
| RPA012 Unrealistically Low UI Timeout | true |  | 3 | 10 |
| RPA013 ContinueOnError Enabled | true |  | 4 | 12 |
| RPA014 Excessive ContinueOnError Usage | true |  | 6 | 18 |
| RPA015 Missing Explicit Timeout on Critical UI Activity | true |  | 1 | 8 |
| RPA016 Legacy UI Automation Activity | true |  | 1 | 8 |
| RPA017 Selector Uses idx Attribute | true |  | 3 | 12 |
| RPA018 Potentially Unstable Selector Attribute | true |  | 3 | 12 |
| RPA019 Overly Complex Selector | true |  | 1 | 8 |
| RPA020 Hard-Coded Absolute File Path | true |  | 3 | 10 |
| RPA021 Hard-Coded Email Address | true |  | 1 | 8 |
| RPA022 HTTP Request Without Explicit Timeout | true |  | 5 | 15 |
| RPA023 HTTP Request Without Local Error Handling | true |  | 5 | 15 |
| RPA024 Excessive Workflow Arguments | true |  | 2 | 8 |
| RPA025 Large Workflow | true |  | 5 | 15 |

## Rule Catalog and Custom Rules

The rule catalog is the single read model for built-in and locally defined rules. It exposes rule id, localized name and description, category, default severity, scope, default scoring weight, max penalty, aggregation support, fix-preview support, and safe auto-apply availability.

```bash
curl "http://localhost:5000/api/uipath/rules?locale=en"
curl "http://localhost:5000/api/uipath/rules/RPA007?locale=tr"
```

Rule sources:

- `BuiltIn`: compiled deterministic analyzer rules, currently `RPA001` through `RPA029`.
- `Custom`: local declarative rules created by the user.

Custom rules are intentionally declarative. They do not execute user code, scripts, regular expressions, package queries, shell commands, or arbitrary expressions. A custom rule consists of metadata, scope, severity, scoring settings, and simple conditions joined by `All` or `Any`.

Supported custom rule scopes:

| Scope | Target |
| --- | --- |
| Activity | Parsed activity metadata and properties |
| Workflow | Parsed workflow summary and metrics |
| Project | Project metadata and dependencies |

Supported condition fields:

| Field | Notes |
| --- | --- |
| Activity.Name | Activity type/name comparison |
| Activity.DisplayName | DisplayName comparison |
| Activity.Property | Property existence/value heuristics |
| Workflow.Name | Workflow filename |
| Workflow.Path | Project-relative workflow path |
| Workflow.ActivityCount | Numeric total activity count |
| Workflow.ExecutableActivityCount | Numeric executable activity count |
| Workflow.MaxNestingDepth | Numeric depth |
| Project.Compatibility | UiPath compatibility metadata |
| Project.IsReFramework | Boolean REFramework heuristic |
| Dependency.Name | Project dependency name |
| Dependency.Version | Project dependency version |
| Dependency.Category | Offline dependency category such as Excel, Mail, UIAutomation, WebAPI, or Other |
| Dependency.UsageStatus | Offline usage status: Used, PossiblyUnused, or Unknown |
| Dependency.RiskLevel | Offline dependency risk level: Low, Medium, High, or Critical |

Supported operators:

| Operator | Text | Numeric | Boolean |
| --- | --- | --- | --- |
| Equals / NotEquals | yes | yes | yes |
| Contains / StartsWith / EndsWith | yes | no | no |
| GreaterThan / GreaterThanOrEqual | no | yes | no |
| LessThan / LessThanOrEqual | no | yes | no |
| Exists / NotExists | yes | yes | yes |

Custom rule storage is local-first. The backend stores rules in the current user's application data folder as `RPA Dev Assistant/custom-rules.json` with a schema version. Writes are atomic, and corrupt or unreadable files fail safely by returning an empty custom rule list instead of crashing the analyzer.

Custom rule endpoints:

```bash
curl http://localhost:5000/api/uipath/custom-rules

curl -X POST http://localhost:5000/api/uipath/custom-rules/test \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "rule": { "id": "CUSTOM-001", "name": "Large nested workflow", "scope": "Workflow", "category": "Maintainability", "severity": "Warning", "enabled": true, "weight": 3, "maxPenalty": 12, "matchMode": "All", "conditions": [] }}'

curl -X POST http://localhost:5000/api/uipath/custom-rules \
  -H "Content-Type: application/json" \
  -d '{ "id": "CUSTOM-001", "name": "Large nested workflow", "scope": "Workflow", "category": "Maintainability", "severity": "Warning", "enabled": true, "weight": 3, "maxPenalty": 12, "matchMode": "All", "conditions": [] }'

curl http://localhost:5000/api/uipath/custom-rules/export
```

The Default profile includes enabled custom rules automatically. Custom findings use `source = "Custom"` and appear in analysis, score breakdowns, Ask Project local answers, and HTML/JSON reports alongside built-in findings. The Rules screen in the React UI shows built-in and custom rules together and provides a local custom rule builder with test, save, JSON file import, and export actions.

Custom profiles are stored locally as `RPA Dev Assistant/rule-profiles.json`. A custom profile overlays Default: rules not mentioned in the custom profile inherit Default behavior, while included rule configs can change enabled state, severity override, weight, and max penalty. This allows company-specific profiles without mutating the built-in Default profile.

Profile endpoints:

```bash
curl http://localhost:5000/api/uipath/rule-profiles

curl -X POST http://localhost:5000/api/uipath/rule-profiles \
  -H "Content-Type: application/json" \
  -d '{ "id": "company-standard", "name": "Company Standard", "rules": [ { "ruleId": "RPA007", "enabled": false, "weight": 1, "maxPenalty": 10 } ] }'

curl http://localhost:5000/api/uipath/rule-profiles/export
```

Custom rule localization:

- `name`, `description`, and `recommendation` remain the canonical fallback.
- Optional `nameEn` / `nameTr`, `descriptionEn` / `descriptionTr`, and `recommendationEn` / `recommendationTr` are used by locale-aware API and HTML report output.
- Technical values, rule ids, workflow paths, and activity names are not translated.

`Activity.Property` conditions support both explicit and compact syntax:

```json
{ "field": "Activity.Property", "operator": "Equals", "propertyName": "TimeoutMS", "compareValue": "120000" }
{ "field": "Activity.Property", "operator": "Equals", "value": "TimeoutMS=120000" }
{ "field": "Activity.Property", "operator": "GreaterThanOrEqual", "propertyName": "TimeoutMS", "compareValue": "100000" }
```

Deliberate boundaries:

- Custom rule localization is explicit metadata. The app uses `nameEn` / `nameTr`, `descriptionEn` / `descriptionTr`, and `recommendationEn` / `recommendationTr` when supplied, but it does not machine-translate user-entered text.
- `Activity.Property` matching supports parsed property existence, string comparisons, compact `Property=Value` syntax, and numeric comparisons for literal numeric property values. It intentionally does not execute or evaluate VB expressions.
- Local rule/profile storage is complete for the desktop MVP. Cloud sync, marketplace distribution, and multi-user governance require database/auth/cloud infrastructure and remain a separate product epic.

## Architecture

The solution separates host concerns from domain analysis:

- `RpaDevAssistant.Core` contains domain models and scanner services. It is framework-independent and can later be reused by a Web API, desktop app, CLI, CI/CD job, or internal service.
- `RpaDevAssistant.Api` exposes the scanner over ASP.NET Core Web API and delegates analysis to Core through dependency injection.
- `RpaDevAssistant.Core.Tests` validates scanner behavior with generated test projects.

This foundation is intentionally broader than a single code-review feature so future modules can be added independently.

## Solution Structure

```text
RpaDevAssistant/
├── src/
│   ├── RpaDevAssistant.Api/
│   └── RpaDevAssistant.Core/
├── tests/
│   └── RpaDevAssistant.Core.Tests/
├── samples/
│   └── SampleUiPathProject/
├── RpaDevAssistant.sln
├── .gitignore
└── README.md
```

## Build

```bash
dotnet restore
dotnet build
```

## Test

```bash
dotnet test
```

## Run API

```bash
dotnet run --project src/RpaDevAssistant.Api/RpaDevAssistant.Api.csproj
```

## Scan a UiPath Project

```bash
curl -X POST http://localhost:5000/api/uipath/projects/scan \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project"}'
```

Example response:

```json
{
  "isValid": true,
  "projectName": "SampleProject",
  "projectPath": "/path/to/uipath/project",
  "compatibility": "Windows",
  "isReFramework": true,
  "workflowCount": 5,
  "totalActivityCount": 42,
  "activityTypeCounts": {
    "Assign": 12,
    "Click": 3,
    "LogMessage": 7,
    "Sequence": 5
  },
  "workflows": [
    {
      "name": "Main.xaml",
      "relativePath": "Main.xaml",
      "activityCount": 10,
      "analysis": {
        "fileName": "Main.xaml",
        "relativePath": "Main.xaml",
        "activityCount": 10,
        "arguments": [],
        "activities": [],
        "parseErrors": [],
        "parseWarnings": []
      }
    }
  ],
  "dependencies": [],
  "errors": [],
  "warnings": []
}
```

## Analyze a UiPath Project

```bash
curl -X POST http://localhost:5000/api/uipath/projects/analyze \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default"}'
```

Example response:

```json
{
  "isValid": true,
  "projectName": "SampleProject",
  "projectPath": "/path/to/uipath/project",
  "workflowCount": 8,
  "totalActivityCount": 75,
  "analysis": {
    "totalFindings": 12,
    "criticalCount": 0,
    "errorCount": 2,
    "warningCount": 5,
    "suggestionCount": 5,
    "infoCount": 0,
    "findings": [
      {
        "ruleId": "RPA001",
        "ruleName": "Avoid Delay Activities",
        "severity": "Warning",
        "category": "Reliability",
        "message": "Delay activity detected. Prefer state-based waiting mechanisms when possible.",
        "description": "Delay activities can make automations brittle when application response times vary.",
        "recommendation": "Use Check App State, Retry Scope, Element Exists, or timeout-based UI activities where possible.",
        "workflowPath": "Business/DelayExample.xaml",
        "activityName": "Delay",
        "activityDisplayName": "Delay",
        "propertyName": "Duration",
        "currentValue": "00:00:05"
      }
    ]
  },
  "qualityScore": {
    "score": 84,
    "grade": "B",
    "rawPenalty": 24,
    "normalizedPenalty": 16,
    "totalFindings": 12,
    "profileId": "default",
    "profileName": "Default",
    "scoreBreakdown": [
      {
        "ruleId": "RPA001",
        "ruleName": "Avoid Delay Activities",
        "findingCount": 2,
        "severity": "Warning",
        "weight": 2,
        "rawPenalty": 4,
        "appliedPenalty": 4,
        "maxPenalty": 10
      }
    ]
  },
  "errors": [],
  "warnings": []
}
```

## Rule Profiles

```bash
curl http://localhost:5000/api/uipath/rule-profiles
curl http://localhost:5000/api/uipath/rule-profiles/default
```

## Workflow Detail

The React desktop UI can now open a workflow detail panel from the Workflows tab. The panel uses the existing analyzer response; it does not re-parse files in the browser and does not mutate project files.

Workflow detail shows:

- File name and project-relative path.
- Total and executable activity counts.
- Workflow-level finding count and linked findings.
- Workflow arguments with name, direction, and type when the parser exposes them.
- Invoked workflows and callers from static `Invoke Workflow File` metadata.
- Activity type distribution.
- Parent-child activity hierarchy with expandable activity property details.
- A workflow-scoped AI Review action when AI is configured.

The `/api/uipath/projects/analyze` workflow summaries include parsed `activities`, `arguments`, and `parseErrors` so the UI can render workflow details without adding a separate detail endpoint.

Known limits:

- Dynamic `Invoke Workflow File` expressions are shown as dynamic references and are not clickable.
- Variable extraction is still parser-limited and is not shown as a dedicated workflow detail table.
- Very large activity trees are rendered with collapsed branches, but further virtualized rendering may be useful for extremely large workflows.

## Localization

The product uses `tr` and `en` locale support across the frontend and backend-generated user text. The selected locale is persisted in browser/local desktop storage when available and falls back safely to English when storage is unavailable or an unsupported locale is received.

Localized areas include:

- Navigation, project intake, overview, findings, workflow detail, Fix Preview labels, Ask Project labels, AI panel labels, and report UI labels.
- Backend rule names, messages, descriptions, and recommendations for `RPA001` through `RPA025`.
- Analyze responses through the request `locale` field.
- Backend-generated Fix Suggestion title, description, explanation, and controlled availability/error messages.
- Ask Project deterministic answers and AI fallback messages.
- AI Review, Ask Project AI, and AI-assisted fix prompts include an explicit language instruction.
- HTML report static labels and deterministic finding text through the report `locale` request field.

Backend localization contract:

- `SupportedLocale` normalizes supported locales.
- `IRpaDevAssistantLocalizer` resolves centralized translation keys with English fallback.
- Missing translations do not show raw keys to users; English fallback is used.
- JSON field names remain stable and English for API compatibility.
- Technical terms and identifiers are intentionally preserved in Turkish output, including Workflow, XAML, REFramework, Queue, Selector, Retry Scope, Check App State, Try Catch, Throw, Rethrow, Invoke Workflow File, Log Message, HTTP Request, DisplayName, and Rule ID.

Selected UI locale controls the response language. If the UI locale is `tr` and the user asks an English question, the backend still returns Turkish user-facing prose while keeping workflow paths, activity names, and Rule ID values unchanged.

Analyze accepts locale:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/analyze \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "locale": "tr"}'
```

Report export accepts locale:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/report \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "format": "html", "locale": "tr"}'
```

JSON field names remain English for API stability.

Known limits:

- AI model compliance is enforced through prompt instructions, but live model wording can still vary. The backend does not fake AI output when no API key is configured.
- Existing provider-specific parsed AI responses are trusted only after structured JSON parsing; invalid responses return localized controlled failures.
- Some low-level evidence descriptions are still canonical English because evidence is treated as technical trace data rather than prose.

## AI Review

AI Review is an optional interpretation layer that runs after deterministic analysis. It does not replace the scanner, parser, rule engine, rule profiles, or quality score. The rule engine remains the primary source of objective findings; AI Review summarizes risk, prioritizes issues, and suggests improvements from the minimized analysis context.

AI review flow:

```text
UiPath Project
↓
Project Scanner
↓
XAML Parser
↓
Rule Engine
↓
Findings + Quality Score
↓
AI Context Builder
↓
Prompt Builder
↓
AI Review Provider
↓
Structured AI Review Result
```

The Core project owns provider-independent abstractions:

- `IUiPathAiReviewService` orchestrates deterministic analysis and AI review.
- `IUiPathAiReviewContextBuilder` creates a compact project or workflow context.
- `IUiPathAiPromptBuilder` builds central prompts with hallucination-control instructions.
- `IUiPathAiReviewProvider` hides the concrete provider implementation.
- `ISecretRedactor` removes sensitive values before any prompt is sent.

`RpaDevAssistant.Infrastructure` contains the OpenAI-compatible provider implementation used by AI Review, Ask Project, and AI Fix. `AI:Provider` selects either the hosted `OpenAI` service or a localhost-only `Local` endpoint. The existing OpenAI behavior remains the default; its API key is read from `OPENAI_API_KEY`, `OpenAI:ApiKey`, or `.env.local` during local development. Do not commit `.env.local`.

Security and privacy behavior:

- Raw XAML is not sent to the AI provider.
- The context includes only project metadata, workflow summaries, selected activity metadata, deterministic findings, score summary, and selected dependencies.
- Property values are truncated.
- Sensitive property names such as password, token, secret, API key, client secret, and connection string are redacted as `[REDACTED]`.
- AI review is explicit. Running `/analyze` does not automatically call the AI provider.
- Provider failures return a controlled AI error result and do not break deterministic analysis.

Prompt strategy:

- Deterministic findings are treated as authoritative evidence.
- The selected locale is included in the prompt instructions. Turkish prompts require Turkish prose while preserving UiPath technical terms and identifiers.
- The model is instructed not to invent activities, workflows, files, packages, or rule findings.
- If evidence is insufficient, the model should say so instead of guessing.
- Output is parsed as structured JSON into `UiPathAiReviewResult`.

Configuration:

```bash
AI__Provider=OpenAI
OPENAI_API_KEY=sk-...
OpenAI__Model=gpt-5.6-mini
OpenAI__MaxOutputTokens=1200
```

For a local model server exposing an OpenAI-compatible Responses API, select `Local` and provide the full `/v1/responses` endpoint:

```bash
AI__Provider=Local
AI__LocalEndpoint=http://127.0.0.1:11434/v1/responses
AI__LocalModel=your-local-model
# Optional when the local server requires authentication:
AI__LocalApiKey=
```

The local endpoint is accepted only when its host is `localhost` or a numeric loopback address such as `127.0.0.1` or `::1`. User info, query strings, fragments, LAN addresses, remote hosts, and HTTP redirects are rejected. The endpoint is shared by AI Review, Ask Project, and AI Fix; provider secrets and response payloads are not written to logs. Local servers must implement the OpenAI Responses API request and response shapes used by this application.

AI project review endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/ai-review \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "scope": "Project"}'
```

AI workflow review endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/ai-review \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "scope": "Workflow", "workflowPath": "Main.xaml"}'
```

Example AI review response:

```json
{
  "isConfigured": true,
  "isSuccess": true,
  "summary": "The project is generally maintainable, but static findings show brittle wait behavior and weak exception handling in key workflows.",
  "riskLevel": "Medium",
  "strengths": [
    "Workflow names are mostly descriptive.",
    "Quality score remains in the B range."
  ],
  "issues": [
    {
      "title": "Delay-based synchronization",
      "severity": "Medium",
      "description": "Static analysis found Delay activities that can make UI automation timing brittle.",
      "evidence": "RPA001 in Business/DelayExample.xaml",
      "recommendation": "Replace fixed waits with Check App State, Retry Scope, Element Exists, or timeout-based UI activities.",
      "workflowPath": "Business/DelayExample.xaml",
      "relatedRuleIds": ["RPA001", "RPA004"]
    }
  ],
  "recommendations": [
    "Prioritize Error and Warning findings before naming suggestions.",
    "Review exception handling paths before production release."
  ],
  "architectureObservations": [
    "No raw XAML was required for this review; observations are based on parsed metadata and deterministic findings."
  ],
  "confidence": 0.78,
  "reviewedScope": "Project",
  "model": "gpt-5.6-mini"
}
```

## Ask Project / Project Copilot

Ask Project lets a user ask natural-language questions about the selected UiPath project. It is local-first: the backend always scans and analyzes the project, classifies the question, retrieves relevant evidence from local project data, and only calls the AI provider when interpretation is needed.

Ask Project flow:

```text
User Question
↓
Question Classifier
↓
Project Retriever
↓
├───────────────┐
↓               ↓
Direct Answer   AI Required
↓               ↓
Local Answer    Evidence Prompt Builder
                ↓
                Redaction
                ↓
                AI Provider
                ↓
                AI Answer
↓
Answer + Evidence
```

The feature uses these Core abstractions:

- `IUiPathProjectQuestionService` orchestrates the full Ask Project flow.
- `IUiPathProjectQuestionClassifier` maps a question to an intent.
- `IUiPathProjectRetriever` searches workflows, activities, arguments, findings, dependencies, score breakdown, metadata, and invocation data.
- `IUiPathWorkflowGraphBuilder` builds static Invoke Workflow File relationships.
- `IUiPathProjectAssistantAiProvider` hides the concrete AI provider.
- `IUiPathProjectAssistantPromptBuilder` builds evidence-only prompts for analytical questions.

Supported first-version intents:

| Intent | Typical questions |
| --- | --- |
| `FindActivityUsage` | Where is Delay used? / Get Credential nerede kullanılıyor? |
| `FindWorkflow` | Which workflows mention config? |
| `WorkflowSummary` | Process.xaml ne yapıyor? |
| `FindingQuery` | Which workflow has the most findings? |
| `ArchitectureQuestion` | REFramework doğru kullanılmış mı? |
| `InvocationQuery` | Main.xaml hangi workflow'ları çağırıyor? |
| `DependencyQuery` | Which packages are used? |
| `ProjectStatistics` | Kaç workflow var? |
| `ExceptionHandlingQuestion` | Exception handling açısından en riskli workflow hangisi? |
| `FreeFormAnalysis` | Maintainability açısından yorumla. |

Local answers do not use AI. Examples:

- `Delay nerede kullanılıyor?`
- `Where is Get Credential used?`
- `Kaç workflow var?`
- `En fazla finding hangi workflow'da?`
- `Main.xaml hangi workflow'ları çağırıyor?`
- `Process.xaml kim tarafından çağrılıyor?`

Analytical answers can use AI when `OPENAI_API_KEY` is configured. Examples:

- `Bu projede en riskli alan ne?`
- `Exception handling tasarımı nasıl?`
- `REFramework doğru kullanılmış mı?`
- `Bu workflow neden karmaşık görünüyor?`

Retrieval is keyword-based in this MVP. It normalizes lowercase text, punctuation, whitespace, path separators, and common UiPath activity aliases such as `HTTP Request` / `HTTPRequest`, `Get Credential` / `GetCredential`, `Log Message` / `LogMessage`, and `Invoke Workflow` / `Invoke Workflow File`. Embeddings and vector databases are intentionally not used yet; the current retrieval is deterministic, explainable, and easy to test.

Relevance scoring is centralized in `UiPathProjectRetrievalWeights`:

| Match | Score |
| --- | ---: |
| Exact activity match | 10 |
| Workflow path/name match | 8 |
| Rule ID exact match | 10 |
| DisplayName contains term | 5 |
| Finding message contains term | 3 |
| Property contains term | 2 |

Every answer includes evidence so the user can see where it came from. Evidence can include workflow path, activity name/display name, rule id, property, value, description, and relevance score.

Ask Project endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/ask \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "locale": "tr", "question": "Bu projede Queue nerede kullanılıyor?"}'
```

Workflow-context question:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/ask \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "locale": "tr", "preferredWorkflowPath": "Framework/Process.xaml", "question": "Bu workflow ne yapıyor?"}'
```

Example direct response:

```json
{
  "answer": "Delay is used 5 time(s) in 3 workflow(s):\n- Framework/Process.xaml: 2\n- Business/Login.xaml: 2\n- Main.xaml: 1",
  "confidence": "High",
  "answerType": "Aggregated",
  "usedAi": false,
  "relatedWorkflows": ["Business/Login.xaml", "Framework/Process.xaml", "Main.xaml"],
  "relatedActivities": ["Delay"],
  "relatedRuleIds": [],
  "evidence": [
    {
      "type": "Activity",
      "workflowPath": "Main.xaml",
      "activityName": "Delay",
      "activityDisplayName": "Delay 5 seconds",
      "propertyName": "Duration",
      "value": "00:00:05",
      "description": "Delay 5 seconds (Delay) in Main.xaml.",
      "relevanceScore": 10
    }
  ]
}
```

Example AI-assisted response:

```json
{
  "answer": "Exception handling risk is concentrated in Framework/Process.xaml because retrieved findings include RPA002 and RPA003.",
  "confidence": "Medium",
  "answerType": "Analytical",
  "usedAi": true,
  "model": "gpt-5.6-mini",
  "relatedWorkflows": ["Framework/Process.xaml"],
  "relatedRuleIds": ["RPA002", "RPA003"],
  "reasoningSummary": "Based only on retrieved deterministic findings.",
  "evidence": []
}
```

Privacy and no-key behavior:

- Direct lookup questions are answered locally.
- Analytical questions send only a minimized, redacted evidence set when AI is configured.
- Raw XAML is not sent.
- Secret-like values are redacted before prompt construction.
- If `OPENAI_API_KEY` is missing, deterministic questions still work and AI-required questions return a controlled not-configured answer.

## Fix Suggestions

Fix Suggestions provide safe repair guidance for deterministic static analysis findings. Most suggestions are preview-only: they return an explanation, target workflow/activity, risk, confidence, validation notes, steps, risks, user-input hints, and a patch-style preview. Safe Apply Fix v1 adds a narrow, reversible auto-apply path only for `RPA007 Generic Activity Display Name`.

Backend-generated fix title, description, explanation, and controlled availability/error messages are resolved through the selected locale. AI-assisted fix prompts also carry the same locale instruction; no AI fix is generated automatically during analysis.

Fixability levels:

- `NotFixable`: no automated suggestion is available yet.
- `Advisory`: the assistant can explain the safe manual direction, but cannot produce a precise patch.
- `Previewable`: the assistant can show a concrete before/after proposal, but user choice is required.
- `SafeAutomatic`: the suggestion is low-risk enough for the separate Safe Apply pipeline. This currently applies only to RPA007 DisplayName changes.

Fix suggestion flow:

```text
Finding
↓
Fix Registry
↓
Fix Provider
↓
Fix Context
↓
┌─────────────────┐
↓                 ↓
Deterministic     AI Assisted
↓                 ↓
Fix Suggestion    AI Advisor
\                 /
↓               ↓
Validation
↓
Patch Preview
↓
User
```

Deterministic fixes are generated from known rule semantics. For example, `RPA007 Generic Activity Display Name` can suggest changing:

```text
BEFORE
DisplayName = "Click"

AFTER
DisplayName = "Click Login Button"
```

AI-assisted fixes are only generated after explicit user action. The AI advisor receives a minimized, redacted context containing the finding, selected workflow summary, selected activity, nearby activities, related findings, and invocation context. Raw XAML is not sent, and AI output is never applied automatically.

Supported fix providers:

| Rule | Fix Type | Deterministic | Risk |
| --- | --- | --- | --- |
| RPA001 Avoid Delay Activities | ActivityReplacement | Yes | Medium |
| RPA002 Empty Catch Block | ExceptionHandlingChange | Yes | High |
| RPA003 Exception Silently Swallowed | ExceptionHandlingChange | Yes | High |
| RPA004 Long Hard-Coded Delay | ActivityReplacement | Yes | Medium |
| RPA005 Invalid Invoke Workflow Reference | PropertyChange | Yes | Medium |
| RPA006 Workflow Naming Convention | NamingChange | Yes | Low |
| RPA007 Generic Activity Display Name | NamingChange | Yes | Low |
| RPA008 Workflow Has No Logging | ActivityInsertion | Yes | Low |

Patch preview formats:

- `InstructionOnly`: safe manual guidance without generated XAML.
- `PropertyChange`: property-level before/after values and changed property metadata.
- `XamlFragment`: reserved for future trusted fragment previews.
- `Text`, `PropertyDiff`, and `PseudoXaml` remain accepted legacy values for older clients.

Single fix suggestion endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/fix-suggestion \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "ruleId": "RPA007", "workflowPath": "Business/Login.xaml", "activityId": "activity-id"}'
```

The plural `/api/uipath/projects/fix-suggestions` endpoint is retained as a backward-compatible alias for single-finding suggestions.

Bulk deterministic endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/fix-suggestions/all \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "maxSuggestions": 100}'
```

Example response:

```json
{
  "isAvailable": true,
  "suggestion": {
    "id": "1a2b3c4d5e6f7890",
    "ruleId": "RPA007",
    "title": "Use a descriptive activity display name",
    "description": "Rename the generic activity display name to describe intent.",
    "fixType": "NamingChange",
    "fixability": "SafeAutomatic",
    "confidence": "High",
    "riskLevel": "Low",
    "workflowPath": "Business/Login.xaml",
    "activityId": "activity-id",
    "activityName": "Click",
    "activityDisplayName": "Click",
    "propertyName": "DisplayName",
    "currentValue": "Click",
    "suggestedValue": "Click Login Button",
    "beforePreview": "DisplayName = \"Click\"",
    "afterPreview": "DisplayName = \"Click Login Button\"",
    "patchPreview": {
      "format": "PropertyChange",
      "before": "DisplayName = \"Click\"",
      "after": "DisplayName = \"Click Login Button\"",
      "changedProperties": [
        {
          "name": "DisplayName",
          "before": "Click",
          "after": "Click Login Button"
        }
      ]
    },
    "explanation": "Descriptive display names make UiPath workflows easier to review, debug, and maintain.",
    "steps": ["Review the proposed DisplayName.", "Apply the DisplayName change through the Safe Apply flow.", "Re-run analysis."],
    "risks": ["Low risk: DisplayName is designer metadata and should not affect runtime behavior."],
    "validationNotes": ["Review the suggested name in context before changing it in UiPath Studio."],
    "requiresAi": false,
    "canAutoApply": true,
    "expectedFileHash": "sha256..."
  },
  "validation": {
    "isValid": true,
    "errors": [],
    "warnings": []
  }
}
```

Risk and confidence:

- `Risk` estimates how likely the suggestion is to affect workflow behavior.
- `Confidence` estimates how much evidence supports the suggestion.
- High-risk exception handling suggestions must be reviewed carefully in UiPath Studio.

Stale suggestion validation checks that the workflow still exists, the activity still exists, property changes still match the current value, suggested values are not empty, and the suggestion is tied to the same rule id as the finding.

## Safe Apply Fix

Safe Apply Fix v1 uses a whitelist policy. The only auto-applicable mutation is:

```text
RPA007 Generic Activity Display Name
DisplayName property change
RiskLevel.Low
RequiresAi=false
CanAutoApply=true
```

Everything else remains preview-only in the generic auto-apply path. Delay replacement, exception handling changes, logging insertion, and all AI-assisted fixes must be applied manually in UiPath Studio.

Workflow rename is available as a separate, explicitly confirmed RPA006 transaction rather than through the generic auto-apply whitelist. The user supplies the final project-relative `.xaml` path. The service locks project workflows, rejects stale hashes and path traversal, backs up the renamed workflow and every changed caller, updates only statically resolved `Invoke Workflow File` references with XML-aware mutation, preserves caller-relative separator style, validates every resulting XAML file, and rolls the complete transaction back on failure. Dynamic workflow references are never guessed; they are returned for manual review.

```bash
curl -X POST http://localhost:5000/api/uipath/projects/fixes/rename-workflow \
  -H "Content-Type: application/json" \
  -d '{"projectPath":"/path/to/project","workflowPath":"workflow1.xaml","newWorkflowPath":"Business/ProcessInvoice.xaml","expectedFileHash":"sha256..."}'
```

Apply flow:

```text
Fix Suggestion
↓
Mutation Policy
↓
Precondition Validation
↓
Activity Locator
↓
Backup
↓
Atomic XAML Mutation
↓
Post Validation
↓
Success?
├─ Yes → Audit Log
└─ No  → Rollback
```

The mutation service parses XAML with XML APIs and changes only the target `DisplayName` attribute. It does not use regex or string replacement for XAML mutation. Activity lookup prefers UiPath `sap2010:WorkflowViewState.IdRef`, then parser activity path, then a unique element + expected DisplayName fallback. Ambiguous matches are rejected.

Preconditions include project existence, `project.json`, safe workflow path resolution under the project root, `.xaml` existence, whitelist policy, allowed property, current value match, non-empty suggested value, changed value check, expected file hash check when provided, and parseable XAML.

Backups are created by default under:

```text
.rpadevassistant/
└── backups/
    └── yyyyMMdd-HHmmssfff/
        ├── backup.json
        └── Business/
            └── Login.xaml
```

`backup.json` stores backup id, timestamp, product version, original project path, workflow path, SHA-256 original/modified file hashes, rule id, property name, previous value, and new value. Successful mutations are appended to `.rpadevassistant/logs/mutations.jsonl`. The scanner ignores `.rpadevassistant/`, so backups do not inflate workflow or activity counts. Add `.rpadevassistant/` to your UiPath project `.gitignore` if the project is version controlled.

Apply endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/fixes/apply \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "fixSuggestionId": "1a2b3c4d5e6f7890", "ruleId": "RPA007", "workflowPath": "Business/Login.xaml", "activityId": "Click_1", "propertyName": "DisplayName", "expectedCurrentValue": "Click", "suggestedValue": "Click Login", "expectedFileHash": "sha256...", "createBackup": true}'
```

Example apply response:

```json
{
  "success": true,
  "applied": true,
  "message": "Fix applied successfully.",
  "workflowPath": "Business/Login.xaml",
  "ruleId": "RPA007",
  "propertyName": "DisplayName",
  "previousValue": "Click",
  "newValue": "Click Login",
  "backupPath": "/path/to/project/.rpadevassistant/backups/20260830-021530123/Business/Login.xaml",
  "backupId": "20260830-021530123",
  "requiresReanalysis": true
}
```

The Findings screen also offers **Apply all safe fixes** when eligible RPA007 occurrences exist. The bulk endpoint does not broaden the mutation whitelist: it expands aggregated RPA007 details internally, refreshes every suggestion and file hash immediately before applying, delegates each change to the same single-fix backup/validation pipeline, and stops at the first failure. A partially completed batch remains fully auditable and individually undoable.

```bash
curl -X POST http://localhost:5000/api/uipath/projects/fixes/apply-all \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "maxFixes": 100, "createBackup": true}'
```

Support table:

| Rule | Fix | Preview | Auto Apply |
| --- | --- | --- | --- |
| RPA001 | Delay replacement | Yes | No |
| RPA002 | Exception handling | Yes | No |
| RPA003 | Log/rethrow recommendation | Yes | No |
| RPA004 | Long delay replacement | Yes | No |
| RPA005 | Invoke reference guidance | Yes | No |
| RPA006 | Workflow naming | Yes | No |
| RPA007 | DisplayName property | Yes | Yes |
| RPA008 | Logging insertion guidance | Yes | No |

## Undo / Restore

Undo / Restore v1 lets users roll back RPA Dev Assistant safe apply mutations. It only uses backups created by RPA Dev Assistant; users cannot select arbitrary backup files, force restore, or overwrite external changes.

Undo flow:

```text
Change History
↓
Select Backup
↓
Undo Eligibility
↓
Current Hash Validation
↓
Backup Integrity Validation
↓
Safety Backup
↓
Atomic Restore
↓
Post Validation
↓
Success?
├─ Yes → Restore Audit
└─ No  → Rollback to Safety Backup
```

Terminology:

- `Undo` is the user action that reverses a selected RPA Dev Assistant mutation.
- `Restore` is the lower-level operation that copies the original backup bytes back into the workflow after validation.

Before undo, the current workflow SHA-256 must match the backup metadata `modifiedHash`. If the file was changed after the fix was applied, undo is blocked with `UNDO_FILE_CHANGED` to prevent data loss. If the current hash already equals `originalHash`, the operation is treated as a safe no-op: no restore is necessary.

Backup integrity is checked before restore. The backed-up XAML file hash must match metadata `originalHash`, and the backup XAML must be parseable. Invalid or tampered backups appear in Change History as `InvalidBackup` and cannot be undone.

Undo creates a safety backup before modifying the current file:

```text
.rpadevassistant/
└── restore-backups/
    └── yyyyMMdd-HHmmssfff/
        ├── restore-backup.json
        └── Business/
            └── Login.xaml
```

The restore writes exact backup bytes through a temp file and atomic replace, preserving original formatting, namespaces, XML declaration, encoding, and BOM. After restore, the current file hash must equal `originalHash`, the XAML parser must read the workflow, and the project scanner must still find it. If post-restore validation fails, the safety backup is restored and the original backup remains untouched.

Multiple mutations naturally behave like a stack. If a workflow changed from `Click` to `Click Login` and then from `Click Login` to `Click Login Button`, only the newest backup is undoable while current state is `Click Login Button`. Older backups stay visible but disabled until newer changes are undone.

Change History endpoints:

```bash
curl "http://localhost:5000/api/uipath/projects/backups?projectPath=/path/to/uipath/project"
curl "http://localhost:5000/api/uipath/projects/backups/20260830-021530123?projectPath=/path/to/uipath/project"
```

Undo endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/fixes/undo \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "backupId": "20260830-021530123", "workflowPath": "Business/Login.xaml", "expectedCurrentHash": "modified-sha256", "createSafetyBackup": true}'
```

Example backup list response:

```json
{
  "backups": [
    {
      "backupId": "20260830-021530123",
      "createdAtUtc": "2026-08-30T02:15:30.123Z",
      "workflowPath": "Business/Login.xaml",
      "ruleId": "RPA007",
      "propertyName": "DisplayName",
      "previousValue": "Click",
      "newValue": "Click Login Button",
      "originalHash": "original-sha256",
      "modifiedHash": "modified-sha256",
      "status": "Available",
      "canUndo": true
    }
  ]
}
```

Example undo response:

```json
{
  "success": true,
  "restored": true,
  "message": "Change restored successfully.",
  "backupId": "20260830-021530123",
  "workflowPath": "Business/Login.xaml",
  "previousHash": "modified-sha256",
  "restoredHash": "original-sha256",
  "safetyBackupId": "20260830-031500111",
  "requiresReanalysis": true
}
```

Successful undo operations are appended to `.rpadevassistant/logs/restores.jsonl`. Force restore, backup delete, retention cleanup, multi-file transactions, workflow rename undo, activity insertion/removal undo, and AI-driven restore decisions are intentionally out of scope.

## Analysis Reports

Analysis reports turn scanner, rule engine, and quality scoring output into a stable shareable contract. The frontend displays report-oriented summaries, but report creation and HTML generation stay in the backend/Core reporting layer.

Report flow:

```text
UiPath Project
↓
Project Scanner
↓
XAML Parser
↓
Rule Engine
↓
Quality Scoring
↓
Analysis Report Builder
↓
Report Exporters
├── JSON
└── HTML
```

JSON reports are useful for automation, archiving, integrations, and future import flows. HTML reports are standalone files intended for sharing with reviewers or managers; they include embedded CSS, no CDN dependencies, and no JavaScript dependency.

Report exports use schema version `1.0`. The exported filename follows:

```text
{ProjectName}-RPA-Analysis-{yyyyMMdd-HHmmss}.json
{ProjectName}-RPA-Analysis-{yyyyMMdd-HHmmss}.html
```

Invalid Windows filename characters are replaced and very long project names are shortened.

Desktop export:

- User clicks `Export JSON` or `Export HTML`.
- A native Save File dialog opens.
- The report is written only to the user-selected path.

Browser export:

- The same buttons use the browser download mechanism.
- Native save dialogs are only available in the desktop app.

Report endpoint:

```bash
curl -X POST http://localhost:5000/api/uipath/projects/report \
  -H "Content-Type: application/json" \
  -d '{"projectPath": "/path/to/uipath/project", "profileId": "default", "format": "json"}'
```

Small JSON report snippet:

```json
{
  "schemaVersion": "1.0",
  "productName": "RPA Dev Assistant",
  "productVersion": "0.1.0",
  "projectName": "InvoiceAutomation",
  "qualityScore": 84,
  "grade": "B",
  "summary": {
    "totalFindings": 8,
    "errorCount": 1,
    "warningCount": 4,
    "suggestionCount": 3,
    "workflowsWithFindings": 4,
    "cleanWorkflows": 8
  },
  "complexityDistribution": [
    { "name": "Low", "count": 4 },
    { "name": "Medium", "count": 17 },
    { "name": "High", "count": 14 },
    { "name": "VeryHigh", "count": 16 }
  ],
  "topComplexWorkflows": [
    {
      "workflowPath": "DocumentSubTypeApproval.xaml",
      "executableActivities": 423,
      "maxNestingDepth": 15,
      "decisionCount": 24,
      "loopCount": 1,
      "complexityScore": 552,
      "complexityLevel": "VeryHigh"
    }
  ]
}
```

HTML reports include a `Workflow Complexity` section with localized TR/EN labels, complexity distribution, and the top complex workflows table. JSON reports keep canonical English field names while carrying the same complexity data for automation.

## Desktop Application

### Signed Desktop Updates

The packaged desktop app checks the HTTPS GitHub Releases updater endpoint in the background. It never installs an update automatically. When a newer signed version is available, Settings > About shows the version and release notes; installation starts only after the user selects **Download and Install**.

Tauri verifies every updater archive with the public key embedded in `tauri.conf.json`. The matching private key is stored as the GitHub Actions secret `TAURI_SIGNING_PRIVATE_KEY` and is never committed to the repository. A `v*` Windows release build uses `tauri.updater.conf.json`, produces the signed NSIS updater archive, creates `latest.json`, and uploads those files to the matching GitHub Release. Browser development mode never calls the native updater.

RPA Dev Assistant can run as a local desktop app through Tauri. The desktop shell reuses the React + TypeScript + Vite frontend and starts the existing .NET backend as a bundled sidecar. Backend logic is not rewritten in Rust.

Desktop architecture:

```text
Tauri desktop window
↓
React frontend
↓
Native folder picker
↓
.NET backend sidecar on 127.0.0.1
↓
Scanner / XAML parser / rule engine / scoring engine
```

Modes:

- Browser Development Mode: run the Vite frontend in a browser. Native folder browsing is disabled, but manual project path entry still works.
- Desktop Development Mode: run Tauri with the Vite dev server. The desktop shell starts the backend sidecar and exposes its local URL to the frontend.
- Packaged Desktop Mode: Tauri serves the built frontend and bundles the published .NET backend executable as a sidecar.

Requirements:

- .NET 8 SDK
- Node.js and npm
- Rust toolchain with Cargo
- Tauri platform prerequisites
- Windows 10/11 for the initial packaged target

Development:

```bash
npm install
npm run frontend:build
npm run frontend:test
npm run backend:restore
npm run backend:build
npm run backend:test
npm run desktop:dev
```

Production Windows build:

```bash
npm run desktop:build
```

The desktop build script runs:

1. Frontend build.
2. Backend publish for `win-x64`.
3. Tauri NSIS build.

The backend sidecar publish step creates:

```text
frontend/src-tauri/bin/RpaDevAssistant.Api-x86_64-pc-windows-msvc.exe
```

Tauri installer output is expected under:

```text
frontend/src-tauri/target/release/bundle/nsis/
```

The Windows release workflow at `.github/workflows/windows-desktop-release.yml` validates backend/frontend code, publishes the sidecar, builds the NSIS installer, verifies a non-empty artifact, and uploads it. When `WINDOWS_CERTIFICATE_BASE64` and `WINDOWS_CERTIFICATE_PASSWORD` repository secrets are configured, the installer is Authenticode-signed with a trusted timestamp. Unsigned builds may trigger Windows SmartScreen.

Linux packaging is defined by `npm run desktop:build:linux` and `.github/workflows/linux-desktop-release.yml`. It publishes a self-contained `linux-x64` backend sidecar and builds both `.deb` and AppImage artifacts on Ubuntu 22.04. GitHub Actions run `36051909068` completed the backend/frontend validation, package build, artifact checks, and upload successfully. Linux remains a secondary target; Windows 10/11 is the primary supported desktop platform.

Runtime configuration is centralized under the `RpaDevAssistant` configuration section and supports standard .NET environment overrides (`__` separator). Safe defaults are included in `src/RpaDevAssistant.Api/appsettings.json` for local desktop use. Configurable values include the desktop CORS allowlist, custom rule/profile storage files, analysis history root/retention, and official package metadata timeout/cache settings. Startup fails with a clear validation error for malformed origins, non-HTTPS metadata endpoints, or non-positive durations.

Example overrides:

```bash
RpaDevAssistant__AllowedOrigins__0=http://127.0.0.1:5173
RpaDevAssistant__Storage__AnalysisHistoryRoot=/path/to/local/history
RpaDevAssistant__History__MaxSnapshotsPerProject=20
RpaDevAssistant__Features__Ai=false
```

### UiPath Orchestrator Inventory

Orchestrator integration is optional and read-only. Configure it through backend environment variables; the access token is never returned to the frontend or written to diagnostics:

```bash
RpaDevAssistant__Orchestrator__BaseUrl=https://cloud.uipath.com/org/tenant/orchestrator_/
RpaDevAssistant__Orchestrator__AccessToken=your-short-lived-token
RpaDevAssistant__Orchestrator__TenantName=tenant-name
RpaDevAssistant__Orchestrator__FolderId=12345
RpaDevAssistant__Orchestrator__DeploymentType=AutomationCloud
```

For Automation Suite, use its HTTPS Orchestrator base URL and set `DeploymentType=AutomationSuite`. The Orchestrator screen reads Process release metadata, Queue definitions, Asset names/types/scopes, and Machine names/types. Asset values and credential contents are deliberately not requested. A missing configuration produces an explicit not-configured state rather than sample data.

### Declarative Rule Modules

The Rules screen can export and import a versioned JSON module containing custom rules and custom rule profiles. A module includes `schemaVersion`, `moduleId`, `name`, `version`, optional publisher metadata, rules, and profiles. The complete manifest is validated before persistence; duplicate IDs, unsupported schemas, invalid conditions, invalid scoring values, and attempts to use the built-in `RPA` prefix are rejected. Modules are data-only and never load executable DLLs or scripts.

Feature flags are exposed read-only at `GET /api/features`. The `Ai`, `FileMutations`, `ConfigGeneration`, and `FlowchartConversion` flags default to enabled; when disabled, only their mapped endpoints return `FEATURE_DISABLED`, while scanning and health endpoints remain available.

Local diagnostics are privacy-preserving. Crash reporting is enabled by default and stores only timestamp, operation ID, HTTP method/route, and exception type under the local application-data diagnostics directory. Opt-in usage telemetry is disabled by default; when enabled with `RpaDevAssistant__Diagnostics__TelemetryEnabled=true`, it stores only route, status, and duration. Request bodies, query strings, project paths, XAML, and exception messages are never written. `GET /api/diagnostics/summary` exposes in-process counters without returning log contents.

Desktop security notes:

- The sidecar backend binds to `127.0.0.1`, not `0.0.0.0`.
- The frontend learns the runtime backend URL through a single API base URL helper.
- Native folder selection only returns the folder selected by the user.
- Tauri permissions are limited to dialog open and sidecar process lifecycle.

## CI/CD Quality Gate

The quality gate evaluates the same deterministic analysis response used by the desktop app. It fails with exit code `2` when the quality score is below the configured minimum or when enabled severity gates detect `Error`/`Critical` findings. Transport or configuration failures use exit code `1`.

With the backend running locally:

```bash
npm run quality-gate -- --project /path/to/uipath/project --minimum-score 80
```

Configuration can also be supplied through `UIPATH_PROJECT_PATH`, `RPA_API_URL`, `RPA_PROFILE_ID`, `RPA_MINIMUM_SCORE`, `RPA_FAIL_ON_ERROR`, and `RPA_FAIL_ON_CRITICAL`. The reusable `.github/workflows/uipath-quality-gate.yml` workflow restores and starts the analyzer before enforcing this policy. It can be called from a UiPath repository with `workflow_call` or run manually with `workflow_dispatch`.

## Git Commit And Branch Comparison

The Change History screen can compare two local Git refs such as `HEAD~1` and `HEAD`, two commit SHAs, or two branch names. The backend resolves each ref, exports the selected UiPath project from each revision into isolated temporary directories with `git archive`, and runs the existing production analyzer on both snapshots. It reports score and finding deltas, new and resolved findings, and changed files.

The operation is read-only: it does not run checkout, reset, or modify the working tree. The project must already be inside a local Git repository.

### Pull Request Review

GitHub, GitLab, and Azure DevOps pull/merge requests can be reviewed from Change History. Provider credentials remain in the local .NET backend; the frontend sends only the provider, repository identifier, pull request number, selected profile, and local project path.

```bash
RpaDevAssistant__SourceControl__GitHub__AccessToken=github-token
RpaDevAssistant__SourceControl__GitLab__AccessToken=gitlab-token
RpaDevAssistant__SourceControl__AzureDevOps__BaseUrl=https://dev.azure.com/organization/project/
RpaDevAssistant__SourceControl__AzureDevOps__AccessToken=azure-devops-pat
```

The provider API resolves Pull Request metadata and base/head commit SHA values. Analysis remains local and read-only, so both commits must already exist in the selected clone. RPA Dev Assistant does not fetch, checkout, reset, or alter the repository. Publishing the generated review summary as a PR comment is a separate explicit user action.

## UiPath Studio Workflow Opening

In the desktop application, Workflow Detail can open the selected project-relative `.xaml` file with the operating system's registered application, normally UiPath Studio on Windows. The Tauri command canonicalizes both paths, requires `project.json`, rejects files outside the selected project, and accepts only existing `.xaml` files. The action is disabled in browser mode.

The packaged executable can also be registered as a UiPath Studio External Tool. Pass the project root and the selected project-relative workflow:

```text
RPA Dev Assistant.exe --project "C:\\UiPath\\InvoiceAutomation" --workflow "Business\\Login.xaml"
```

After the local backend is ready, the desktop app runs the normal production project analysis and opens the requested Workflow Detail. The External Tool registration itself remains a user/organization Studio setting; no UiPath package is injected into analyzed projects.
