# Touchstone Telemetry

Touchstone emits metrics and traces for every run, suite, lifecycle stage, test case, result sink callback, and result export. It uses only the .NET base class library (`System.Diagnostics.Metrics.Meter` and `System.Diagnostics.ActivitySource`). Touchstone has no OpenTelemetry SDK, exporter, or Radiant dependency and never opens a connection. Your host subscribes and exports. With no listener attached, emission costs a few nanoseconds and allocates nothing.

What this gives an operator, using only Grafana:

- **Where the time went.** One trace per run: `touchstone.run` > `suite:<SuiteId>` > `stage:before_suite`, `case:<Suite>.<Case>`, `sink <event>`, `stage:after_suite`. Each case span is `Activity.Current` while the test body runs, so `HttpClient` calls made by the test carry a W3C `traceparent`. If the system under test is traced (for example a Watson 7.1 service), the failing test and the server-side request appear in one trace.
- **What failed.** Case, stage, suite, and run outcomes on metrics, `touchstone.errors` by stage and `error.type`, and spans with `Error` status plus an `exception` event (type, message, stack trace).
- **Whether it is still passing.** `touchstone.suite.last_success_time` and `touchstone.run.last_success_time` make it possible to alert when a suite that runs on a schedule (synthetic monitoring, smoke tests) has stopped passing.

All names below are public contract, defined as constants in `Touchstone.Core.TouchstoneTelemetry`.

## Contents

- [Meter and source names](#meter-and-source-names)
- [Subscribing](#subscribing)
- [Configuration](#configuration)
- [Metrics catalog](#metrics-catalog)
- [Spans catalog](#spans-catalog)
- [Recommended alerts (PromQL)](#recommended-alerts-promql)
- [Dashboards](#dashboards)
- [Guarantees](#guarantees)

## Meter and source names

| Kind | Name | Version |
|---|---|---|
| Meter | `Touchstone` | Touchstone package version |
| ActivitySource | `Touchstone` | Touchstone package version |

`Touchstone.Core` (executor) and `Touchstone.Cli` (export) both emit on these names, so one subscription covers everything. The xUnit, NUnit, and MSTest adapters emit through `Touchstone.Core`.

## Subscribing

### Radiant

```csharp
using Radiant;

RadiantSettings settings = new RadiantSettings("my-integration-tests");
settings.Otlp.Endpoint = "http://127.0.0.1:4317";
settings.Sources.AddMeter("Touchstone");
settings.Sources.AddActivitySource("Touchstone");
settings.Sources.AddActivitySource("System.Net.Http"); // optional: client spans for the test's HTTP calls

using (RadiantHost host = RadiantHost.Start(settings))
{
    return await ConsoleRunner.RunAsync(MyApiSuites.All);
}
```

Dispose the host before the process exits so buffered spans and metrics flush. For a short-lived test process, OTLP push is the right choice: Prometheus may never scrape a process that lives for a few seconds.

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

### Ad hoc

```bash
dotnet-counters monitor --counters Touchstone -- dotnet run --project MyTests.Console
```

### Runtime metrics

Touchstone is a library and does not emit runtime metrics. The host should: Radiant enables them by default. On .NET 9 and later the runtime publishes the built-in `System.Runtime` meter. On .NET 8, add `OpenTelemetry.Instrumentation.Runtime`.

## Configuration

| Setting | Default | Effect |
|---|---|---|
| `TouchstoneTelemetry.SuiteLabelEnabled` | `true` | Adds the `suite` label to suite, stage, case, active-case, and error metrics. Suite identifiers are normally a small fixed set in code, so the label is bounded. Set to `false` before running if suites are generated dynamically (one per tenant, per file, and so on). Spans always carry the suite identifier. Process-wide. |

There is nothing to turn on: emission is always enabled and is a no-op until a listener subscribes. To turn Touchstone telemetry off, don't subscribe to the `Touchstone` meter or source.

## Metrics catalog

Prometheus names are what the OpenTelemetry Prometheus exporter produces (dots become underscores, units and `_total` are appended). Durations are histograms in seconds. Derive quantiles in Grafana with `histogram_quantile`. Touchstone computes no quantiles in-process.

| Instrument | Prometheus name | Type | Unit | Labels | Description |
|---|---|---|---|---|---|
| `touchstone.runs` | `touchstone_runs_total` | Counter | `{run}` | `outcome` | Multi-suite runs (`TestExecutor.RunAsync`, `ConsoleRunner.RunAsync`, adapter `RunAllAsync`). |
| `touchstone.run.duration` | `touchstone_run_duration_seconds` | Histogram | `s` | `outcome` | Run duration. |
| `touchstone.run.last_success_time` | `touchstone_run_last_success_time_seconds` | Observable gauge | `s` | none | Unix time of the last run with no failed case. Absent until one passes. |
| `touchstone.suites` | `touchstone_suites_total` | Counter | `{suite}` | `suite`, `outcome` | Suite executions. |
| `touchstone.suite.duration` | `touchstone_suite_duration_seconds` | Histogram | `s` | `suite`, `outcome` | Suite duration, including hooks and sink callbacks. |
| `touchstone.suite.last_success_time` | `touchstone_suite_last_success_time_seconds` | Observable gauge | `s` | `suite` | Unix time of the last suite completion with no failed case. |
| `touchstone.stages` | `touchstone_stages_total` | Counter | `{stage}` | `suite`, `stage`, `outcome` | Lifecycle stage executions. |
| `touchstone.stage.duration` | `touchstone_stage_duration_seconds` | Histogram | `s` | `suite`, `stage`, `outcome` | Lifecycle stage duration (setup and teardown cost). |
| `touchstone.cases` | `touchstone_cases_total` | Counter | `{case}` | `suite`, `outcome` | Test case results. |
| `touchstone.case.duration` | `touchstone_case_duration_seconds` | Histogram | `s` | `suite`, `outcome` | Test case duration (skipped cases record 0). |
| `touchstone.cases.active` | `touchstone_cases_active` | UpDownCounter | `{case}` | `suite` | Cases executing right now. A value stuck at 1 means a hung case. |
| `touchstone.sink.events` | `touchstone_sink_events_total` | Counter | `{event}` | `event`, `outcome` | `ITestResultSink` callbacks. |
| `touchstone.sink.duration` | `touchstone_sink_duration_seconds` | Histogram | `s` | `event`, `outcome` | Sink callback duration (for example a sink that posts results to a server). |
| `touchstone.errors` | `touchstone_errors_total` | Counter | `{error}` | `stage`, `error.type`, `suite` (not on `stage="run"`) | Failures by where they happened and exception type. |
| `touchstone.exports` | `touchstone_exports_total` | Counter | `{export}` | `format`, `outcome`, `error.type` (failures only) | Console runner result exports. |
| `touchstone.export.duration` | `touchstone_export_duration_seconds` | Histogram | `s` | `format`, `outcome` | Console runner result export duration. |
| `touchstone.build.info` | `touchstone_build_info` | Observable gauge | `{info}` | `version` | Always 1. |

Label values:

| Label | Values |
|---|---|
| `outcome` (runs, suites) | `passed`, `failed` (at least one case failed), `error` (an exception escaped: hook, sink, or argument), `canceled` (an `OperationCanceledException` escaped) |
| `outcome` (cases) | `passed`, `failed`, `skipped` |
| `outcome` (stages, sink events, exports) | `ok`, `failed` |
| `stage` (stages) | `before_suite`, `after_suite` |
| `stage` (errors) | `case`, `before_suite`, `after_suite`, `sink`, `run` |
| `event` | `suite_started`, `test_completed`, `suite_completed` |
| `format` | `json` |
| `error.type` | Fully qualified exception type, for example `System.InvalidOperationException` |
| `suite` | `TestSuiteDescriptor.SuiteId` (omitted when `SuiteLabelEnabled` is false) |

Case identifiers, display names, tags, skip reasons, and exception messages never appear on metrics. They appear on spans only.

There is no `queued` stage, pool, queue, cache, or limiter: Touchstone runs suites and cases sequentially and holds no concurrency slots.

## Spans catalog

All spans are `ActivityKind.Internal` on the `Touchstone` source. Status is set explicitly: `Ok` on success, `Error` on failure with the exception message as description. Failed spans carry `error.type` and an `exception` event (`exception.type`, `exception.message` truncated to 4096 characters, `exception.stacktrace`).

| Span name | Parent | Emitted by | Attributes |
|---|---|---|---|
| `touchstone.run` | caller's `Activity.Current` (root otherwise) | `TestExecutor.RunAsync`, `ConsoleRunner.RunAsync`, adapter `RunAllAsync` | `touchstone.suite.count`, `touchstone.case.count`, `touchstone.case.passed`, `touchstone.case.failed`, `touchstone.case.skipped`, `touchstone.outcome` |
| `suite:<SuiteId>` | `touchstone.run` or caller | `TestExecutor.RunSuiteAsync` | `touchstone.suite.id`, `touchstone.suite.display_name`, `touchstone.case.count`, `touchstone.case.passed`, `touchstone.case.failed`, `touchstone.case.skipped`, `touchstone.outcome` |
| `stage:before_suite`, `stage:after_suite` | suite | `TestExecutor.RunSuiteAsync` | `touchstone.suite.id`, `touchstone.outcome` |
| `case:<SuiteId>.<CaseId>` | suite, or caller for `ExecuteCaseAsync` | `TestExecutor.RunSuiteAsync`, `TestExecutor.ExecuteCaseAsync` | `touchstone.suite.id`, `touchstone.case.id`, `touchstone.test.id`, `touchstone.case.display_name`, `touchstone.case.tags`, `touchstone.case.skip_reason`, `touchstone.outcome` |
| `sink suite_started`, `sink test_completed`, `sink suite_completed` | suite | `TestExecutor.RunSuiteAsync` (only when a sink is supplied) | `touchstone.suite.id`, `touchstone.outcome` |
| `export json` | caller's `Activity.Current` (root otherwise) | `ConsoleRunner.RunAsync` with `resultsPath` | `touchstone.export.result_count`, `touchstone.outcome` |

A run whose cases fail (but nothing throws) ends with `Error` status and the description `N test(s) failed`, so Tempo's error filter finds failing runs.

### Context propagation

- Spans nest under whatever `Activity.Current` is when Touchstone is called. A host that starts its own activity (or receives a `traceparent`) gets the whole run inside its trace.
- The case span is current inside the test body. `HttpClient`, gRPC, and any other instrumented client inject its W3C `traceparent` into outbound requests automatically.
- Touchstone does no background hand-off. Everything runs on the caller's async flow.

### Theory-style hosts

xUnit `MemberData`, NUnit `TestCaseSource`, and MSTest `DynamicData` rows call into Touchstone one case at a time. Use `TestExecutor.ExecuteCaseAsync(testCase, ct)` rather than `testCase.ExecuteAsync(ct)` so each row emits its case span and metrics. It rethrows the original exception, so the framework still reports the failure.

## Recommended alerts (PromQL)

```promql
# A scheduled suite has not passed in 30 minutes
time() - max by (suite) (touchstone_suite_last_success_time_seconds) > 1800

# Any case failures in the last 15 minutes
sum by (suite) (increase(touchstone_cases_total{outcome="failed"}[15m])) > 0

# Setup or teardown is failing (environment problem, not a test problem)
sum by (suite, stage) (increase(touchstone_stages_total{outcome="failed"}[15m])) > 0

# Runs aborted by an escaped exception or cancellation
sum(increase(touchstone_runs_total{outcome=~"error|canceled"}[15m])) > 0

# Case p95 regressed above 5 seconds
histogram_quantile(0.95, sum by (le, suite) (rate(touchstone_case_duration_seconds_bucket{outcome!="skipped"}[15m]))) > 5

# A case appears hung
max by (suite) (touchstone_cases_active) > 0 and max by (suite) (changes(touchstone_cases_total[10m])) == 0

# Result sink is failing (for example a results server is down)
sum by (event) (increase(touchstone_sink_events_total{outcome="failed"}[15m])) > 0
```

## Dashboards

Touchstone is a library. It ships no `compose.yaml` or Grafana dashboards, because there is no Touchstone service to deploy. The host that runs the tests owns the stack. Suggested panels for the host's dashboard folder:

| Panel | Query |
|---|---|
| Pass rate by suite | `sum by (suite) (rate(touchstone_cases_total{outcome="passed"}[$__rate_interval])) / sum by (suite) (rate(touchstone_cases_total{outcome!="skipped"}[$__rate_interval]))` |
| Failures by suite | `sum by (suite) (increase(touchstone_cases_total{outcome="failed"}[$__range]))` |
| Case p95 by suite | `histogram_quantile(0.95, sum by (le, suite) (rate(touchstone_case_duration_seconds_bucket[$__rate_interval])))` |
| Setup and teardown p95 | `histogram_quantile(0.95, sum by (le, suite, stage) (rate(touchstone_stage_duration_seconds_bucket[$__rate_interval])))` |
| Errors by type | `sum by (stage, error_type) (increase(touchstone_errors_total[$__range]))` |
| Time since last pass | `time() - touchstone_suite_last_success_time_seconds` |
| Sink and export latency | `histogram_quantile(0.95, sum by (le, event) (rate(touchstone_sink_duration_seconds_bucket[$__rate_interval])))` |

From a failing panel, open Tempo and search `{ resource.service.name = "<host>" && name =~ "case:.*" && status = error }` to find the failing case span and, through propagation, the downstream request it made.

## Guarantees

- Instrumentation is best-effort. A listener or exporter that throws while recording cannot change a test outcome, a summary, or which exceptions propagate. This is covered by `ListenerFailureIsolated` in `src/Test.Shared/TelemetrySuites.cs`.
- With no listener, every code path behaves exactly as before. This is covered by `NoListener`.
- No secrets, payloads, or test inputs are recorded. Spans carry descriptor identifiers and names, tags, skip reasons, and exception type, message, and stack trace from failed tests. If your exception messages can contain sensitive data, filter the `exception` event in your collector.
- Metric labels are bounded: fixed enumerations plus `suite` (controllable with `SuiteLabelEnabled`) and `error.type`.
