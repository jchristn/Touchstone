# Behavior Driven Development Plan

Status: Complete. Implementation, local verification, benchmark baseline/gate, and NuGet package/symbol publication are complete. See BDD_VERIFICATION.md for release evidence and operational follow-ups.

Plan date: 2026-10-16

## Purpose and scope

Add optional behavior driven development (BDD) capabilities to Touchstone so a developer can describe a behavior as a scenario, execute its Given/When/Then steps, and see which step failed through every supported host. Keep existing delegate-based tests as a first-class API. Scenario authors must not need runner attributes, a feature-file parser, or a third-party dependency.

BDD starts with agreed examples of observable behavior. For the Notes sample, agree on examples such as updating an existing note, fetching a missing note, and rejecting invalid input before writing steps. Step names should describe those behaviors, rather than HTTP plumbing. Execution reports will serve as evidence of those examples, with a clear distinction between passed, failed, skipped, canceled, and unexecuted work.

The initial delivery includes code-first scenarios, typed context, reusable steps, data-driven examples, structured results, live events, console and JSON reporting, all three framework adapters, telemetry, and documentation. An offline HTML report is a separate follow-on milestone. Feature-file parsing, natural-language binding discovery, dependency injection containers, retries, parallel steps, nested scenarios, and a new assertion library are deferred.

## Tracking work

Use `[ ]` for unfinished tasks and `[x]` only when the task's acceptance criteria have been verified. Record partial progress in the ledger; an unchecked task may be in progress or blocked. Do not mark a phase complete while required tasks remain. Every completion needs a commit or PR reference plus verification evidence.

| Phase | State | Owner | Started | Completed | Evidence / blocker / next action |
|---|---|---|---|---|---|
| P0: Contracts and baseline | Complete | Codex | 2026-10-16 | 2026-10-16 | BDD_VERIFICATION.md |
| P1: Core models and authoring | Complete | Codex | 2026-10-16 | 2026-10-16 | BDD_VERIFICATION.md |
| P2: Execution and lifecycle | Complete | Codex | 2026-10-16 | 2026-10-16 | BDD_VERIFICATION.md |
| P3: Console and JSON | Complete | Codex | 2026-10-16 | 2026-10-16 | BDD_VERIFICATION.md |
| P4: Framework adapters | Complete | Codex | 2026-10-16 | 2026-10-16 | BDD_VERIFICATION.md |
| P5: Telemetry | Complete | Codex | 2026-10-16 | 2026-10-16 | BDD_VERIFICATION.md |
| P6: Examples and documentation | Complete | Codex | 2026-10-16 | 2026-10-16 | BDD_VERIFICATION.md |
| P7: Verification and packaging | Complete | Codex | 2026-10-16 | 2026-10-16 | 454907e; BDD_VERIFICATION.md; published 6 packages and 6 symbols |
| P8: Offline HTML report | Complete | Codex | 2026-10-16 | 2026-10-16 | Automated coverage and Chrome visual inspection |

Task update template:

```text
Task ID:
State: Not started | In progress | Blocked | Complete
Owner:
Date:
Commit or PR:
Verification command and result:
Evidence location:
Remaining work or blocker:
```

Dependencies: P0 precedes P1; P1 precedes P2; P2 precedes P3, P4, and P5; P6 requires the final authoring and output contracts; P7 requires P1 through P6. P8 follows the initial delivery and has its own acceptance gate.

## Requirements and compatibility

The normative requirements live in `~/Code/Agents/requirements`. Read the applicable files again when implementation begins because they can change independently of this plan. Existing code is useful context, but does not override those requirements.

| Requirement source | Application to this work |
|---|---|
| `BACKEND_TEST_ARCHITECTURE.md` | Shared descriptors depend only on Core and the project under test; no runner dependencies or console output; self-contained fixtures; console success is 0 and failures are non-zero; HTTP loopback uses `127.0.0.1`. |
| `CODE_STYLE.md` | Block namespaces with internal sorted usings, explicit types, one class or enum per file, `_PascalCase` private fields, validated properties, public XML documentation, specific exceptions, cancellation, disposal, and nullable reference types. No em-dashes. |
| `BACKEND_ARCHITECTURE.md` | Apply library-relevant async, typed JSON contracts, packaging, validation, and disposal rules. No new server, database, tenant model, or hosting stack is needed. |
| `TELEMETRY_REQUIREMENTS.md` | Instrument execution and exports with bounded metrics and nested traces; telemetry is best-effort and must not change execution outcomes. No embedded exporter or telemetry backend dependency. |
| `REPOSITORY_REQUIREMENTS.md` | Put new implementation and harness code under `src/`; maintain README, changelog, license, and package assets. Review unconditional housekeeping requirements and record pre-existing gaps. Docker and server API obligations apply if those surfaces are introduced. |
| `VERSIONING.md` | Preserve all current version numbers and labels. Agents must not change them without an explicit user request. Record release recommendations separately. |
| `BENCHMARKING.md` | Commit a reproducible CLI-facing overhead harness, provenance, baselines, and comparison gate. Do not publish unmeasured performance claims. |
| `EXAMPLE_APPLICATIONS.md` | Use the shared-suite and multiple-host patterns as references; requirement text wins if an example differs. |
| `WRITING_DOCUMENTS.md` | Review documentation for clear human wording and no em-dashes; implementation checklists remain appropriate here. |

Authentication, frontend, dashboard, internationalization, installer, and simulated-user requirements do not introduce work for this library-only feature. Reassess their applicability if scope expands to hosted services, user interfaces, or installers. An offline HTML artifact is a report, not a hosted dashboard.

The current build targets `net8.0` and `net10.0`, treats warnings as errors, enables implicit usings, and disables nullable reference types. The last two settings differ from the requirements. P0 must capture that discrepancy; P1 must explicitly disable implicit usings and enable nullable analysis for affected projects, fixing resulting warnings without changing runtime behavior or globally suppressing diagnostics.

## Architecture and public contracts

Keep the scenario authoring API in `Touchstone.Core`. A mandatory scenario package would break the shared-test dependency rule. Optional reporting packages may depend on Core, but shared descriptors must continue to reference only Core. Core remains free of third-party dependencies.

The proposed model has a scenario builder with typed context, ordered step descriptors, immutable execution snapshots, and step results. `Build()` returns a standard `TestCaseDescriptor`; a scenario is one test case in every host. Existing suite and case identities, constructors, execution delegates, tags, skips, and summary counts remain compatible. Adding steps must not inflate case counts.

Proposed types include `ScenarioBuilder<TContext>`, `ScenarioStepDescriptor<TContext>`, `ScenarioStepKind`, `ScenarioStepResult`, and `ScenarioStepOutcome`. Names become final in P0. Add an optional scenario execution contract to the case descriptor so the executor can recognize structured scenarios. Define precedence explicitly and reject ambiguous cases with conflicting executable definitions. Do not hide result collection inside an opaque closure or a process-wide mutable collector.

A context factory runs once per execution. Built descriptors snapshot their step lists; per-run context and results never live on the reusable descriptor. Step delegates receive context and a cancellation token. Given, When, and Then identify intent; And inherits the preceding kind for display and is invalid as the first step. Reusable steps are ordinary typed delegates or methods. No reflection-based name inference is needed.

Represent feature descriptions, requirement references, and example IDs as explicit metadata. Keep stable IDs separate from display names. Parameterized examples expand into independent case descriptors with deterministic example IDs; parameter values must not become metric labels. Fixed JSON shapes use named DTO classes, not DOM navigation.

## P0: Freeze contracts and capture the baseline

The first phase resolves behavior before adding public API. Record decisions in this file so later implementers do not need to reconstruct intent from tests.

- [x] **P0.1** Review current `TestCaseDescriptor`, `TestResult`, `ITestResultSink`, `TestExecutor`, `TestResultCollector`, CLI export, adapters, and shared harnesses. Record signatures and representative console/JSON output before changes.
- [x] **P0.2** Record the requirements audit, including nullable/implicit-usings discrepancies, license packaging, repository housekeeping, and applicable versus inapplicable service requirements. Classify pre-existing gaps separately from feature changes.
- [x] **P0.3** Finalize public type names, descriptor execution precedence, context ownership, result collection, metadata, example identity, and the step-event extension contract. Preserve existing constructor and method overloads.
- [x] **P0.4** Freeze the outcome and cancellation mapping below, including native framework skip behavior and what a direct caller of the scenario descriptor's delegate receives.
- [x] **P0.5** Capture a clean Release build and existing harness results on both target frameworks. Record baseline failures with reproductions before implementation.

Acceptance: documented contracts include a compatibility fixture using the existing APIs, expected result examples, and a requirements gap ledger. No unresolved decision may silently become an implementation default.

## P1: Core models and scenario authoring

- [x] **P1.1** Add documented scenario, step, metadata, context-factory, and result types under `src/Touchstone.Core/`, one class or enum per file. Validate null/empty identities, delegates, duplicate step IDs, empty scenarios, and invalid And placement.
- [x] **P1.2** Implement typed Given/When/Then/And authoring and reusable delegates. Building snapshots definitions; mutating a builder after Build cannot alter a built descriptor.
- [x] **P1.3** Add optional step results and scenario metadata to `TestResult`, with empty defaults for ordinary cases. Keep legacy success and skip fields intact.
- [x] **P1.4** Add typed example expansion with deterministic IDs, duplicate detection, independent execution contexts, and safe display values. If an API returns `IEnumerable`, supply the required async variant with cancellation.
- [x] **P1.5** Enable nullable reference types and explicit usings in affected projects; fix diagnostics with accurate annotations and guards. Document public nullability, thread safety, ownership, and exceptions. Avoid tuples and undocumented public members.

Acceptance: an author can build a standard case using only Core, reuse a step, and expand two examples into two independently discoverable cases. Existing consumer source still compiles without modification.

## P2: Execution and lifecycle

Both `RunAsync`/`RunSuiteAsync` and `ExecuteCaseAsync` must call one scenario execution implementation. Add an additive result-returning single-case API for consumers that need structured results, rather than forcing them through a whole-suite run. Preserve the existing throwing API's original exception and stack trace.

| Condition | Step behavior | Case behavior |
|---|---|---|
| All steps pass | Record Passed for each executed step | Pass once |
| Step throws | Record Failed with original exception; later steps are NotRun | Fail once; throwing API rethrows original exception |
| Descriptor skipped | Do not create context or execute steps; planned steps are Skipped | Preserve existing skip reason and count |
| Matching caller token canceled | Record active step Canceled, remaining steps NotRun | Expose cancellation distinctly; never report success; native host mapping is documented |
| Unrelated cancellation exception | Treat as a failure, preserving exception | Fail once |
| Context creation fails | No step executes; planned steps are NotRun | Fail with a context-creation diagnostic |
| Cleanup fails | Preserve cleanup error separately from any primary error | Fail; do not replace the primary failure |

- [x] **P2.1** Implement the shared execution path, result-returning API, sequential ordering, durations, and the table's outcome mappings. Preserve existing ordinary-case behavior.
- [x] **P2.2** Check cancellation before context creation and each step, and forward tokens into all user operations. Document that cancellation is cooperative and cannot terminate a delegate that ignores its token.
- [x] **P2.3** Implement guaranteed owned-context cleanup after success, failure, and cancellation. Prefer async disposal when available and dispose once. Use a separate bounded cleanup token where cleanup requires one; make the timeout configurable. Do not claim a timeout can forcibly stop user code.
- [x] **P2.4** Preserve primary exceptions and secondary cleanup diagnostics. Keep suite hooks at suite scope; single-case execution must not unexpectedly run them.
- [x] **P2.5** Add optional step-started and step-completed observation without adding required members to `ITestResultSink`. Specify observer-failure behavior consistent with the existing sink contract; keep observer errors distinct from assertion errors.
- [x] **P2.6** Verify repeated and concurrent execution of the same descriptor uses isolated context/results. Document that this does not make externally shared consumer resources thread-safe.

Acceptance: deterministic tests cover every row of the table, exactly-once cleanup, failure plus cleanup failure, observer errors, cancellation before execution and during a step, and ordinary/scenario mixed suites. Case totals remain unchanged.

## P3: Console and JSON reporting

- [x] **P3.1** Add optional expanded scenario output to `ConsoleResultSink`, showing ordered steps, duration, outcome, skip/not-run reasons, and contextual failure diagnostics. Preserve existing compact output as the default.
- [x] **P3.2** Wire live step events into the console and collector. Synchronize output where necessary and keep all `Console.Write*` calls out of Core and shared tests.
- [x] **P3.3** Add named JSON DTOs for metadata, step results, cancellation, and cleanup errors. Preserve existing root array and existing fields; represent new fields additively and document enum encoding and duration units.
- [x] **P3.4** Preserve existing `ConsoleRunner.RunAsync` calls. Add options through a compatible overload or settings object. Document exit behavior for canceled scenarios and export failures, ensuring incomplete execution cannot exit successfully.
- [x] **P3.5** Make parameter-value capture opt-in, support redaction before values reach sinks or exporters, and document that user-supplied step names and exception messages can contain sensitive data.

Acceptance: saved output fixtures cover pass, failure, skip, cancellation, cleanup failure, and mixed suites. Legacy JSON consumers can still deserialize existing fields. Results remain useful without a telemetry listener.

## P4: Framework adapter parity

- [x] **P4.1** Update xUnit fact/theory, NUnit single-test/TestCaseSource, and MSTest single-test/DynamicData paths to execute scenarios through Core.
- [x] **P4.2** Route step diagnostics through each framework's supported per-test output facilities without adding framework references to shared suites. Document host limitations on live output and nested presentation.
- [x] **P4.3** Preserve one native test per case/example in row-based discovery. Verify skip reasons, IDs, exceptions, and cancellation mappings; full-suite wrappers retain their existing aggregation behavior.
- [x] **P4.4** Add adapter coverage for passing/failing steps, skipped scenarios, matching cancellation, cleanup failure, repeated discovery, and data-driven examples.

Acceptance: the same shared scenario definitions execute through console, xUnit, NUnit, and MSTest with equivalent logical outcomes and step diagnostics. Any unavoidable native outcome differences are documented with evidence.

## P5: Telemetry

- [x] **P5.1** Add step spans beneath case spans using the existing `Touchstone` ActivitySource. Outbound HTTP spans created within a step must remain children of that step.
- [x] **P5.2** Add step outcome counters and duration histograms in seconds on the existing Meter. Labels are bounded step kind/outcome values; exclude case/step/example IDs, display names, parameters, and exception messages.
- [x] **P5.3** Instrument context creation, cleanup, and new reporting exports with stable operation names, outcomes, and durations. Mark failures explicitly and record safe exception details under the configured disclosure policy.
- [x] **P5.4** Verify parent restoration, no double-counting, disabled-listener behavior, listener failures, redaction, and best-effort instrumentation that cannot change a scenario result.
- [x] **P5.5** Update `TELEMETRY.md` with instruments, attributes, cardinality rules, cancellation mapping, example traces, and collector subscription guidance. Keep exporters and retention outside Core.

Acceptance: listener-based tests prove the run/suite/case/step hierarchy and metrics. No listener is required to execute or report scenarios.

## P6: Examples and documentation

- [x] **P6.1** Add self-contained scenario suites to `src/Test.Shared`, exposed through `TouchstoneSuites.All`, using project references rather than loading source and published copies of the same assembly.
- [x] **P6.2** Add Notes sample scenarios for create/fetch persistence, missing-note lookup, and invalid input under the existing sample shared project. Keep an ordinary descriptor example. Include an explicitly enabled intentional failure for output inspection.
- [x] **P6.3** Ensure example resources are created and cleaned per execution, HTTP clients and servers use `127.0.0.1`, and step assertions use meaningful specific exceptions.
- [x] **P6.4** Write `BDD.md` with agreed behavior examples, final compilable authoring examples, reusable steps, data tables, lifecycle/ownership, cancellation, outcomes, reporting, telemetry, and migration from a delegate case.
- [x] **P6.5** Update README package/API guidance and add a clearly unreleased changelog entry without assigning a new version. Review affected guidance for accuracy and prohibited punctuation.

Acceptance: a developer can copy a documented example into a shared project referencing only Core and run it through each existing host. Documentation names only implemented capabilities as available.

## P7: Verification, overhead, and packaging

- [x] **P7.1** Run a Release solution build and all existing plus new harnesses for `net8.0` and `net10.0`. Require zero build warnings and explain any pre-existing failures separately.
- [x] **P7.2** Run all four sample hosts on `net10.0`; inspect compact/expanded console output and JSON artifacts. Confirm the intentional failure returns non-zero and normal runs succeed.
- [x] **P7.3** Compile the P0 compatibility fixture against the new assemblies, compare public signatures and legacy JSON fixtures, and inspect package dependency graphs for accidental framework or exporter dependencies in Core.
- [x] **P7.4** Add `src/Test.Benchmark` and `benchmarks/` assets following `BENCHMARKING.md`. Exercise the public console surface in a separate process for ordinary cases versus one-step and multi-step scenarios, with telemetry listeners off/on. Record runtime/SDK, OS, CPU, commit, dirty state, configuration hash, counts, errors, warmups, repetitions, elapsed time, and memory measurement method. Include a no-work process floor and current delegate-case reference.
- [x] **P7.5** Commit baseline artifacts and a comparison command/CI gate. Reject incomplete or incorrect runs; use paired repetitions, documented tolerances and significance checks. Establish the threshold from measured baseline noise in P0/P7, then record it here before accepting results. Do not make performance claims without evidence.
- [x] **P7.6** Pack Release artifacts and inspect README, icon, license file/metadata, XML documentation, symbols, target frameworks, and dependencies. Align packaged license files with the requirements while preserving valid MIT metadata. Leave package versions unchanged.
- [x] **P7.7** Review the final diff for scope, requirements compliance, no suppressed diagnostics, no generated artifacts accidentally staged, and no prohibited framework name. Complete the task ledger with command output and artifact references.

Run these commands from the repository root. Expand each shared runner command for both target frameworks; the sample projects target only `net10.0`.

```sh
dotnet build Touchstone.slnx -c Release
dotnet run --project src/Test.Automated -c Release -f net8.0 -- --results /tmp/touchstone-bdd-net8.json
dotnet run --project src/Test.Automated -c Release -f net10.0 -- --results /tmp/touchstone-bdd-net10.json
dotnet test src/Test.Xunit -c Release -f net8.0
dotnet test src/Test.Xunit -c Release -f net10.0
dotnet test src/Test.Nunit -c Release -f net8.0
dotnet test src/Test.Nunit -c Release -f net10.0
dotnet test src/Test.Mstest -c Release -f net8.0
dotnet test src/Test.Mstest -c Release -f net10.0
dotnet run --project tests/Touchstone.SampleApp.Tests.Console -c Release
dotnet test tests/Touchstone.SampleApp.Tests.Xunit -c Release
dotnet test tests/Touchstone.SampleApp.Tests.Nunit -c Release
dotnet test tests/Touchstone.SampleApp.Tests.Mstest -c Release
dotnet pack Touchstone.slnx -c Release --no-build
```

Acceptance: P0 through P7 are complete with reproducible evidence, all required checks pass, ordinary cases remain compatible, and no version or publication action has occurred without explicit authorization. A missing SDK, unavailable platform, or unexecuted check remains recorded as incomplete rather than passed.

## P8: Offline HTML execution report

The first delivery is usable with console, JSON, and native framework output. HTML reporting follows after those contracts settle and must not hold up initial scenario execution support.

- [x] **P8.1** Add an optional reporter that consumes completed typed results and metadata without re-executing tests or adding dependencies to shared suites.
- [x] **P8.2** Group features, scenarios, examples, and steps; show outcome counts, requirement references, durations, failure diagnostics, and not-run reasons. Identify the run and collection time so a report cannot be mistaken for current proof of behavior.
- [x] **P8.3** Generate a self-contained offline artifact with no remote scripts or tracking. Escape all user text, restrict link schemes, apply the same redaction policy as JSON, and make the report keyboard accessible with readable status text.
- [x] **P8.4** Test hostile names/messages, large suites, empty results, mixed outcomes, canceled runs, and export failures. Inspect representative reports visually and document generation through each supported execution path.

Acceptance: the report opens locally without a network connection, safely renders test-controlled content, preserves execution outcomes, and adds no dependency to Core consumers.

## Decision and exception log

Record requirement conflicts before implementation, with the relevant source, chosen resolution, owner, and verification. A pre-existing mismatch does not authorize weakening the requirement. If an exception needs a human decision, isolate only the dependent work and continue independent tasks.

| ID | Decision / exception | Owner | Date | Rationale and evidence |
|---|---|---|---|---|
| D1 | Scenario authoring remains in Core | Plan | 2026-10-16 | Preserves shared-suite dependency rules and zero third-party dependencies. |
| D2 | Existing versions remain unchanged | Plan | 2026-10-16 | Version changes require an explicit user request. |
| D3 | HTML report is a follow-on milestone | Plan | 2026-10-16 | Execution, host parity, and structured results are the initial delivery gate. |


## Implementation resolutions

The user explicitly authorized implementing this plan, selecting a new version, publishing packages and symbols to NuGet, archiving the plan, and committing/pushing to main on 2026-10-16. Version 0.3.0-alpha follows the pre-1.0 labeling requirement. The earlier version-freeze decision applied before that authorization.

| ID | Resolution | Evidence |
|---|---|---|
| D4 | The sample API has no update operation. Demonstrate persistence through create/fetch rather than adding an unrelated endpoint. | NotesScenarios and BDD.md; all sample hosts pass. |
| D5 | Retain the existing tests/ sample layout; new harnesses and implementation live under src/. | Avoid unrelated repository relocation. |
| D6 | Keep adapter project references in Touchstone's own Test.Shared because the adapters are the project under test. Consumer scenarios still require only Core. | Standalone Core-only documentation example compiles and executes. |
| D7 | Use a dedicated opt-in self-hosted benchmark job. A developer-machine timing baseline cannot gate unrelated hosted hardware honestly. | verify.yaml and BENCHMARKING.md. Provisioning a dedicated runner is an external operational follow-up. |
| D8 | No parameter-value capture API is introduced. Explicit consumer text can be redacted at console/JSON/HTML boundaries; raw in-memory results remain intact. | Reporting and hostile-input tests. |
| D9 | Existing telemetry cases retain full exception events for compatibility. New scenario activities record type-only exception events. | TELEMETRY.md and telemetry tests. |

The requirements audit enabled nullable analysis and explicit usings in affected libraries, added package license assets and Docker Hub repository guidance, and recorded the library-only applicability of hosting, Docker, authentication, and dashboard requirements. Existing sample hosting is unchanged. No new REST, MCP, database, installer, or dashboard surface was introduced.


## Completion evidence

Implementation commit: `454907e4d14c717e6005447b7cb7bc38dda790ef`. All checked implementation tasks map to that commit and the command/results table in BDD_VERIFICATION.md. The completion-evidence commit contains the clean-source benchmark reports, release manifest, publication receipt, and final checklist updates.

P7.5 evidence: both 96-sample benchmark reports under `benchmarks/baselines/` identify the same clean source commit and matching configuration. The paired gate reports no regression; seven comparator contract checks pass. Measured values and interpretation are in `benchmarks/RESULTS.md`. The conservative 20 percent budget remains appropriate for this initial cold-process suite; tightening it requires more matched-machine noise data.

P7.7 evidence: Release build has zero warnings/errors; shared console checks pass on both targets; all nine native framework runs pass; API signatures and an old compiled consumer remain compatible; package ZIP/nuspec/PDB inspection passes. The release manifest identifies all twelve artifacts. NuGet returned HTTP Created for all six package and six symbol uploads. Remote cross-platform CI and provisioning a dedicated benchmark runner remain explicitly documented operational follow-ups.
