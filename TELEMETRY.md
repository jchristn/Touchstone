# Touchstone Telemetry

Touchstone emits metrics and traces for every run, suite, lifecycle stage, case, scenario operation, result sink callback, and report export. It uses only the .NET base class library (`System.Diagnostics.Metrics.Meter` and `System.Diagnostics.ActivitySource`). Touchstone has no OpenTelemetry SDK or exporter dependency and never opens a connection; your host subscribes and exports. With no listener attached, emission costs a few nanoseconds and allocates nothing.

What this gives an operator:

- **Where the time went.** One trace per run: `touchstone.run` contains `touchstone.suite`, which contains `touchstone.stage` (setup and teardown), `touchstone.case`, and `touchstone.sink` spans. Scenario cases add `touchstone.scenario.context`, `touchstone.scenario.step`, and `touchstone.scenario.cleanup`. Each case span is `Activity.Current` while the case runs, so `HttpClient` calls made by the test carry a W3C `traceparent`, and a traced system under test joins the test's trace.
- **What failed.** Outcomes on every metric, `touchstone.errors` by stage and `error.type`, and spans with `Error` status plus an `exception` event.
- **Whether it is still passing.** `touchstone.suite.last_success_time` and `touchstone.run.last_success_time` support alerts when a scheduled suite stops passing.

Every name and value below is public contract, defined as a constant in `Touchstone.Core.TouchstoneTelemetry`, and does not change within a major version.

## Contents

- [Meter and source names](#meter-and-source-names)
- [Subscribing](#subscribing)
- [Configuration](#configuration)
- [Metrics catalog](#metrics-catalog)
- [Label values](#label-values)
- [Spans catalog](#spans-catalog)
- [Recommended alerts (PromQL)](#recommended-alerts-promql)
- [Dashboards](#dashboards)
- [Guarantees](#guarantees)
- [Changes in 1.0](#changes-in-10)

## Meter and source names

| Kind | Name | Version |
|---|---|---|
| Meter | `Touchstone` | Touchstone package version |
| ActivitySource | `Touchstone` | Touchstone package version |

Touchstone.Core (engine and scenarios) and Touchstone.Cli (report export) both emit on these names, so one subscription covers everything. The framework adapters emit through Touchstone.Core.

## Subscribing

### OpenTelemetry SDK

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using TracerProvider tracer = Sdk.CreateTracerProviderBuilder()
    .AddSource("Touchstone")
    .AddOtlpExporter(o => o.Endpoint = new Uri("http://127.0.0.1:4317"))
    .Build();

using MeterProvider meter = Sdk.CreateMeterProviderBuilder()
    .AddMeter("Touchstone")
    .AddOtlpExporter(o => o.Endpoint = new Uri("http://127.0.0.1:4317"))
    .Build();
```

Dispose the providers before the process exits so buffered telemetry flushes. For a short-lived test process, OTLP push is the right choice: Prometheus may never scrape a process that lives for a few seconds. Any host that subscribes to the `Touchstone` meter and source works the same way, for example Radiant.

### Ad hoc

```bash
dotnet-counters monitor --counters Touchstone -- dotnet run --project MyTests.Console
```

### Runtime metrics

Touchstone is a library and does not emit runtime metrics. On .NET 9 and later the runtime publishes the built-in `System.Runtime` meter; on .NET 8, add `OpenTelemetry.Instrumentation.Runtime` to the host.

## Configuration

| Setting | Default | Effect |
|---|---|---|
| `TouchstoneTelemetry.SuiteLabelEnabled` | `true` | Adds the `suite` label to suite, stage, case, active-case, filtered-case, and error metrics. Suite identities are normally a small fixed set in code, so the label is bounded. Set it to `false` before running when suites are generated dynamically (one per tenant, per file, and so on). Spans always carry the suite identity. Process-wide. |

Emission is always on and is a no-op until a listener subscribes. To turn Touchstone telemetry off, do not subscribe to the `Touchstone` meter or source.

## Metrics catalog

Prometheus names are what the OpenTelemetry Prometheus exporter produces (dots become underscores, and units and `_total` are appended). Durations are histograms in seconds; derive quantiles in Grafana with `histogram_quantile`.

| Instrument | Prometheus name | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `touchstone.runs` | `touchstone_runs_total` | Counter | `{run}` | `outcome` | Multi-suite runs (`TestExecutor.RunAsync`, the console runner, adapter run-all bases). |
| `touchstone.run.duration` | `touchstone_run_duration_seconds` | Histogram | `s` | `outcome` | Run duration. |
| `touchstone.run.last_success_time` | `touchstone_run_last_success_time_seconds` | Observable gauge | `s` | none | Unix time of the last successful run. Absent until one succeeds. |
| `touchstone.suites` | `touchstone_suites_total` | Counter | `{suite}` | `suite`, `outcome` | Suite executions. |
| `touchstone.suite.duration` | `touchstone_suite_duration_seconds` | Histogram | `s` | `suite`, `outcome` | Suite duration, including hooks and sink callbacks. |
| `touchstone.suite.last_success_time` | `touchstone_suite_last_success_time_seconds` | Observable gauge | `s` | `suite` | Unix time of each suite's last successful completion. |
| `touchstone.stages` | `touchstone_stages_total` | Counter | `{stage}` | `suite`, `stage`, `outcome` | Setup and teardown executions. |
| `touchstone.stage.duration` | `touchstone_stage_duration_seconds` | Histogram | `s` | `suite`, `stage`, `outcome` | Setup and teardown duration. |
| `touchstone.cases` | `touchstone_cases_total` | Counter | `{case}` | `suite`, `outcome` | Case results, including `not_run`. |
| `touchstone.case.duration` | `touchstone_case_duration_seconds` | Histogram | `s` | `suite`, `outcome` | Case duration. Skipped cases record 0. Not recorded for `not_run`. |
| `touchstone.cases.active` | `touchstone_cases_active` | UpDownCounter | `{case}` | `suite` | Cases executing now. A value stuck above 0 means a hung case. |
| `touchstone.cases.filtered` | `touchstone_cases_filtered_total` | Counter | `{case}` | `suite` | Cases excluded by the filter or by `SkipBehavior.Omit`. |
| `touchstone.sink.events` | `touchstone_sink_events_total` | Counter | `{event}` | `event`, `outcome` | `ITestResultSink` callbacks. |
| `touchstone.sink.duration` | `touchstone_sink_duration_seconds` | Histogram | `s` | `event`, `outcome` | Sink callback duration. |
| `touchstone.errors` | `touchstone_errors_total` | Counter | `{error}` | `stage`, `error.type`, `suite` (not on `stage="run"`) | Failures by where they happened and exception type. |
| `touchstone.scenario.operations` | `touchstone_scenario_operations_total` | Counter | `{operation}` | `operation`, `outcome`, `kind` | Scenario context creation, steps, and cleanup. |
| `touchstone.scenario.operation.duration` | `touchstone_scenario_operation_duration_seconds` | Histogram | `s` | `operation`, `outcome`, `kind` | Scenario operation duration. |
| `touchstone.exports` | `touchstone_exports_total` | Counter | `{export}` | `format`, `outcome`, `error.type` (failures only) | JSON and HTML report exports (Touchstone.Cli). |
| `touchstone.export.duration` | `touchstone_export_duration_seconds` | Histogram | `s` | `format`, `outcome` | Report export duration. |
| `touchstone.build.info` | `touchstone_build_info` | Observable gauge | `{info}` | `version` | Always 1. |

## Label values

| Label | Values |
|---|---|
| `outcome` on runs and suites | `passed` (success), `failed` (a case failed, was canceled, or did not run), `error` (a suite hook failed, or an exception escaped, for example from a sink), `canceled` (caller cancellation stopped the run) |
| `outcome` on cases | `passed`, `failed`, `skipped`, `canceled`, `not_run` |
| `outcome` on stages, sink events, and exports | `ok`, `failed` |
| `outcome` on scenario operations | `passed`, `failed`, `canceled` |
| `stage` on stages | `before_suite`, `after_suite` |
| `stage` on errors | `case`, `before_suite`, `after_suite`, `sink`, `run` |
| `event` | `run_started`, `suite_started`, `case_started`, `case_completed`, `suite_completed`, `run_completed` |
| `operation` | `context`, `step`, `cleanup` |
| `kind` | `given`, `when`, `then`, or `none` for context and cleanup |
| `format` | `json`, `html` |
| `error.type` | Fully qualified exception type, for example `System.InvalidOperationException` |
| `suite` | `TestSuiteDescriptor.SuiteId` (omitted when `SuiteLabelEnabled` is false) |
| `version` | Touchstone version |

Case identities, display names, tags, skip reasons, step names, and exception messages never appear on metrics. They appear on spans only.

Touchstone runs suites and cases sequentially and holds no pools, queues, or concurrency slots.

## Spans catalog

Span names are fixed, so they stay low-cardinality; identities are attributes. All spans are `ActivityKind.Internal` on the `Touchstone` source. Status is explicit: `Ok` on success and `Error` on failure. Failed spans carry `error.type` and an `exception` event (`exception.type`, `exception.message` truncated to 4096 characters, `exception.stacktrace`); scenario operation spans carry only `exception.type`.

| Span name | Parent | Emitted by | Attributes |
|---|---|---|---|
| `touchstone.run` | caller's `Activity.Current` (root otherwise) | `TestExecutor.RunAsync` | `touchstone.suite.count`, `touchstone.case.count`, `touchstone.passed_count`, `touchstone.failed_count`, `touchstone.skipped_count`, `touchstone.canceled_count`, `touchstone.not_run_count`, `touchstone.filtered_count`, `touchstone.outcome` |
| `touchstone.suite` | `touchstone.run` or caller | `TestExecutor.RunSuiteAsync` and `RunAsync` | `touchstone.suite.id`, `touchstone.suite.display_name`, `touchstone.case.count` and the same count attributes, `touchstone.outcome` |
| `touchstone.stage` | suite | setup and teardown | `touchstone.suite.id`, `stage` (`before_suite` or `after_suite`), `touchstone.outcome` |
| `touchstone.case` | suite, or caller for single-case entry points | every case execution | `touchstone.suite.id`, `touchstone.case.id`, `touchstone.test.id`, `touchstone.case.display_name`, `touchstone.case.tags`, `touchstone.case.skip_reason`, `touchstone.outcome` |
| `touchstone.scenario.context` | case | scenario context creation | `touchstone.outcome` |
| `touchstone.scenario.step` | case | each executed scenario step | `touchstone.step.kind`, `touchstone.outcome` |
| `touchstone.scenario.cleanup` | case | scenario cleanup and disposal | `touchstone.outcome` |
| `touchstone.sink` | run, suite, or case | each sink callback | `event`, `touchstone.suite.id` (when in a suite), `touchstone.outcome` |
| `touchstone.export` | caller | `JsonReport.WriteAsync`, `HtmlReport.WriteAsync` | `format`, `touchstone.export.result_count`, `touchstone.outcome` |

A run that did not succeed (but where nothing threw) ends with `Error` status and a description of its failed cases and hook errors, so a trace backend's error filter finds it.

### Context propagation

- Spans nest under whatever `Activity.Current` is when Touchstone is called, so a host that starts its own activity (or receives a `traceparent`) gets the whole run inside its trace.
- The case span is current inside the case body, and the step span inside each step. Instrumented clients inject the W3C `traceparent` automatically.
- Touchstone does no background hand-off. Everything runs on the caller's async flow.

## Recommended alerts (PromQL)

```promql
# A scheduled suite has not passed in 30 minutes
time() - max by (suite) (touchstone_suite_last_success_time_seconds) > 1800

# Any case failures in the last 15 minutes
sum by (suite) (increase(touchstone_cases_total{outcome="failed"}[15m])) > 0

# Setup or teardown is failing (environment problem, not a test problem)
sum by (suite, stage) (increase(touchstone_stages_total{outcome="failed"}[15m])) > 0

# Runs that errored or were canceled
sum(increase(touchstone_runs_total{outcome=~"error|canceled"}[15m])) > 0

# Case p95 regressed above 5 seconds
histogram_quantile(0.95, sum by (le, suite) (rate(touchstone_case_duration_seconds_bucket{outcome!="skipped"}[15m]))) > 5

# A case appears hung
max by (suite) (touchstone_cases_active) > 0 and max by (suite) (changes(touchstone_cases_total[10m])) == 0

# A result sink is failing (for example a results server is down)
sum by (event) (increase(touchstone_sink_events_total{outcome="failed"}[15m])) > 0
```

## Dashboards

Touchstone is a library and ships no dashboards or collector configuration; the host that runs the tests owns its stack. Suggested panels:

| Panel | Query |
|---|---|
| Pass rate by suite | `sum by (suite) (rate(touchstone_cases_total{outcome="passed"}[$__rate_interval])) / sum by (suite) (rate(touchstone_cases_total{outcome!~"skipped\|not_run"}[$__rate_interval]))` |
| Failures by suite | `sum by (suite) (increase(touchstone_cases_total{outcome="failed"}[$__range]))` |
| Case p95 by suite | `histogram_quantile(0.95, sum by (le, suite) (rate(touchstone_case_duration_seconds_bucket[$__rate_interval])))` |
| Setup and teardown p95 | `histogram_quantile(0.95, sum by (le, suite, stage) (rate(touchstone_stage_duration_seconds_bucket[$__rate_interval])))` |
| Slow scenario steps | `histogram_quantile(0.95, sum by (le, kind) (rate(touchstone_scenario_operation_duration_seconds_bucket{operation="step"}[$__rate_interval])))` |
| Errors by type | `sum by (stage, error_type) (increase(touchstone_errors_total[$__range]))` |
| Time since last pass | `time() - touchstone_suite_last_success_time_seconds` |

To go from a failing panel to the failing case, search traces for `name = "touchstone.case" && status = error` and filter on the `touchstone.test.id` attribute.

## Guarantees

- Instrumentation is best-effort. A listener or exporter that throws while recording cannot change a test outcome, a summary, or which exceptions propagate (catalog rows TEL-16 and TEL-15).
- With no listener, every code path behaves the same.
- No secrets, payloads, or test inputs are recorded. Spans carry identities, names, tags, skip reasons, and, on case and stage spans, the exception type, message, and stack trace of failures. If exception messages can contain sensitive data, filter the `exception` event in the collector; report redaction callbacks do not rewrite telemetry.
- Metric labels are bounded: fixed enumerations plus `suite` (controllable with `SuiteLabelEnabled`) and `error.type`.

## Changes in 1.0

1.0 freezes these names. Dashboards and alerts built on 0.3.0-alpha need these updates:

| 0.3.0-alpha | 1.0 |
|---|---|
| Span `suite:<SuiteId>` | `touchstone.suite`, with `touchstone.suite.id` |
| Span `stage:before_suite`, `stage:after_suite` | `touchstone.stage`, with `stage` |
| Span `case:<SuiteId>.<CaseId>` | `touchstone.case`, with `touchstone.test.id` |
| Span `sink <event>` | `touchstone.sink`, with `event` |
| Span `export json`, `export html` | `touchstone.export`, with `format` |
| Spans `scenario:context`, `scenario:step`, `scenario:cleanup` | `touchstone.scenario.context`, `touchstone.scenario.step`, `touchstone.scenario.cleanup` |
| Attributes `touchstone.case.passed`, `.failed`, `.skipped` | `touchstone.passed_count`, `touchstone.failed_count`, `touchstone.skipped_count`, plus `touchstone.canceled_count`, `touchstone.not_run_count`, `touchstone.filtered_count` |
| Sink event `test_completed` | `case_completed`, plus `run_started`, `case_started`, `run_completed` |
| Scenario outcome labels `Passed`, `Failed`, `Canceled` and kinds `Given`, `When`, `Then` | Lowercase: `passed`, `failed`, `canceled`; `given`, `when`, `then` |
| Case outcomes `passed`, `failed`, `skipped` | Adds `canceled` and `not_run` |
| Run and suite outcome after a failed setup hook: exception escaped (`error`) | Still `error`; the run continues and reports the hook failure as a lifecycle error |
| (none) | `touchstone.cases.filtered` |
