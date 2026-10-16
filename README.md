<img src="https://raw.githubusercontent.com/jchristn/Touchstone/main/assets/logo-black.png" alt="Touchstone" width="256" height="256">

# Touchstone

Runner-agnostic test descriptor framework for .NET.

v0.3.0-alpha

## What is Touchstone?

Touchstone is a .NET framework that lets you write **test descriptors** once and run them through any host. Define your unit and integration tests as plain descriptor objects in a shared library, then execute them via xUnit for CI pipelines or via the built-in console runner for fast local development -- without changing a single line of test logic.

A test descriptor carries its identity, name, and execution logic independently of runner attributes. Write a short check as an async delegate, or build a behavior driven development (BDD) scenario from typed Given/When/Then steps. Both produce the same `TestCaseDescriptor` and run through the same engine and adapters.

## Why Touchstone?

- **Write once, run anywhere** -- the same test descriptors work in xUnit, NUnit, MSTest, the console runner, or any custom runner you build.
- **Readable behavior scenarios** -- compose Given/When/Then/And steps with a fresh context per execution, original exception capture, and step-level results in every host.
- **Clean separation between test definitions and test execution** -- descriptors live in a shared project with no runner dependencies.
- **Readable local output without needing `dotnet test`** -- the console runner produces colored, tabular results directly in your terminal.
- **No lock-in to a specific test framework** -- switch runners without rewriting tests.
- **Runner-agnostic core with no dependencies on xUnit, NUnit, etc.** -- Touchstone.Core is a pure .NET library.
- **Optional suite lifecycle hooks (setup/teardown) without making them mandatory** -- add them when you need them, skip them when you don't.
- **First-class skip support with reasons** -- mark descriptors as skipped and the reason propagates to every runner.
- **Full exception capture, not just messages** -- test results preserve the complete exception, including stack traces and inner exceptions.
- **Execution reports** -- use compact or expanded console output, export structured JSON, or generate a self-contained offline HTML report with scenario steps and failure details.
- **Built-in observability** -- every run, suite, setup/teardown stage, and test case emits OpenTelemetry-shaped metrics and traces through the BCL `Meter` and `ActivitySource` named `Touchstone`. No exporter dependency, near-zero cost when nobody listens. See [TELEMETRY.md](TELEMETRY.md).

## Packages

| Package | Description |
|---|---|
| **Touchstone** | Metapackage that includes Core, Cli, and all framework adapters. Install this to get everything. |
| **Touchstone.Core** | Runner-agnostic descriptors, typed BDD scenarios, execution engine, and case/step results. No third-party dependencies. |
| **Touchstone.Cli** | Console runner with compact or live step output, JSON export, offline HTML reports, and exit code contract. |
| **Touchstone.XunitAdapter** | xUnit adapters (theory-driven and fact-style) for running shared descriptors under `dotnet test`. |
| **Touchstone.NunitAdapter** | NUnit adapter (TestCaseSource-driven and single-test) for running shared descriptors under NUnit. Requires NUnit 5.0 or later. |
| **Touchstone.MstestAdapter** | MSTest adapter (DynamicData-driven and single-test) for running shared descriptors under MSTest. Requires MSTest.TestFramework 4.4 or later. |

## Getting Started

### 1. Install Packages

The simplest approach is to install the metapackage, which includes everything:

```bash
dotnet add package Touchstone --version 0.3.0-alpha
```

Or install only the packages you need:

```bash
# Shared test descriptor library (no runner dependency)
dotnet add package Touchstone.Core --version 0.3.0-alpha

# Console runner project
dotnet add package Touchstone.Cli --version 0.3.0-alpha

# xUnit test project
dotnet add package Touchstone.XunitAdapter --version 0.3.0-alpha

# NUnit test project
dotnet add package Touchstone.NunitAdapter --version 0.3.0-alpha

# MSTest test project
dotnet add package Touchstone.MstestAdapter --version 0.3.0-alpha
```

### 2. Define Tests in a Shared Library

Create a shared class library that references `Touchstone.Core` and the application you are testing. The walkthrough uses a shopping cart to show a short delegate check and a BDD scenario in the same suite. Neither test depends on a runner, and the example needs no server or external service.

For a runnable example, put this small application class in `ShoppingCart.cs`. In an existing project, reference your own cart implementation instead.

```csharp
namespace MyTests
{
    using System;

    /// <summary>A cart that tracks item count and total price.</summary>
    public sealed class ShoppingCart
    {
        /// <summary>Total price of the items added.</summary>
        public decimal Total { get; private set; }

        /// <summary>Number of items added.</summary>
        public int ItemCount { get; private set; }

        /// <summary>Add an item at the supplied price.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Price is negative.</exception>
        public void AddItem(decimal price)
        {
            if (price < 0m) throw new ArgumentOutOfRangeException(nameof(price));
            Total += price;
            ItemCount++;
        }
    }
}
```

Put the test definitions in `ShoppingCartSuites.cs`. The first descriptor checks the initial state directly. The second describes a purchase as a sequence of observable behaviors; its builder returns an ordinary descriptor alongside the other cases.

```csharp
namespace MyTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Touchstone.Core;

    /// <summary>Shared cart tests consumed by every host.</summary>
    public static class ShoppingCartSuites
    {
        /// <summary>All suites, with fresh definitions on each access.</summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get { return new[] { CartSuite() }; }
        }

        /// <summary>Direct checks and behavior scenarios in one suite.</summary>
        public static TestSuiteDescriptor CartSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Cart",
                displayName: "Shopping cart",
                cases: new TestCaseDescriptor[]
                {
                    new TestCaseDescriptor(
                        suiteId: "Cart",
                        caseId: "StartsEmpty",
                        displayName: "A new cart starts empty",
                        executeAsync: ct =>
                        {
                            ct.ThrowIfCancellationRequested();
                            ShoppingCart cart = new ShoppingCart();
                            if (cart.Total != 0m || cart.ItemCount != 0)
                                throw new InvalidOperationException("Expected an empty cart.");
                            return Task.CompletedTask;
                        }),

                    new ScenarioBuilder<ShoppingCart>(
                        suiteId: "Cart",
                        caseId: "AddingAnItem",
                        displayName: "Adding an item increases the cart total",
                        createContextAsync: ct =>
                        {
                            ct.ThrowIfCancellationRequested();
                            return Task.FromResult(new ShoppingCart());
                        })
                        .Given("existing", "the cart contains a $10 item", (cart, ct) =>
                        {
                            ct.ThrowIfCancellationRequested();
                            cart.AddItem(10m);
                            return Task.CompletedTask;
                        })
                        .When("add", "a $25 item is added", (cart, ct) =>
                        {
                            ct.ThrowIfCancellationRequested();
                            cart.AddItem(25m);
                            return Task.CompletedTask;
                        })
                        .Then("total", "the cart total is $35", (cart, ct) =>
                        {
                            ct.ThrowIfCancellationRequested();
                            if (cart.Total != 35m)
                                throw new InvalidOperationException(
                                    $"Expected total 35; received {cart.Total}.");
                            return Task.CompletedTask;
                        })
                        .And("count", "the cart contains two items", (cart, ct) =>
                        {
                            ct.ThrowIfCancellationRequested();
                            if (cart.ItemCount != 2)
                                throw new InvalidOperationException(
                                    $"Expected two items; received {cart.ItemCount}.");
                            return Task.CompletedTask;
                        })
                        .Build(),

                    new TestCaseDescriptor(
                        suiteId: "Cart",
                        caseId: "Discounts",
                        displayName: "Discount codes reduce the total",
                        executeAsync: ct => Task.CompletedTask,
                        skip: true,
                        skipReason: "Discount codes are not implemented"),
                });
        }
    }
}
```

Each scenario execution receives a fresh cart, shared only by that execution's steps. A failing step preserves its exception and leaves subsequent steps NotRun. And inherits the preceding step's kind, so the final expectation is reported as Then. The scenario remains one test case, regardless of its step count.

For resource-owning contexts, Touchstone disposes the context after execution, including failure and cancellation. [BDD.md](BDD.md) covers cleanup, reusable steps, typed data examples, metadata, and cancellation in more detail. The following runner examples all consume `ShoppingCartSuites.All` unchanged.

### 3. Run via Console

Create a console application that references your shared test library and `Touchstone.Cli`:

```csharp
using MyTests;
using Touchstone.Cli;

return await ConsoleRunner.RunAsync(ShoppingCartSuites.All);
```

Run it directly:

```bash
dotnet run --project MyTests.Console
```

The runner prints colored tabular results to stdout and returns a non-zero exit code if any test fails.

Enable step output and write both report formats for the same suite:

```csharp
using System.Threading;

using MyTests;
using Touchstone.Cli;

ConsoleResultSink sink = new ConsoleResultSink { ExpandSteps = true };
int exitCode = await ConsoleRunner.RunAsync(
    ShoppingCartSuites.All,
    sink,
    resultsPath: "results.json",
    cancellationToken: CancellationToken.None);

await ScenarioHtmlReport.WriteAsync(
    sink.AllResults,
    "results.html",
    "shopping-cart-run",
    CancellationToken.None);

return exitCode;
```

The direct check, scenario, and skipped placeholder share one summary. JSON includes the scenario's ordered steps, and the HTML report opens offline. See [Console Runner Output](#console-runner-output) for compact and expanded examples.

### 4. Run via xUnit

Create an xUnit test project that references your shared test library and `Touchstone.XunitAdapter`. Use the **theory-driven** pattern to get one xUnit test per non-skipped descriptor. The callback writes the scenario steps to xUnit output before any original failure is rethrown:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyTests;
using Touchstone.Core;
using Xunit;
using Xunit.Abstractions;

public sealed class ShoppingCartTheoryTests
{
    private readonly ITestOutputHelper _Output;

    public ShoppingCartTheoryTests(ITestOutputHelper output)
    {
        _Output = output;
    }

    public static TheoryData<TestCaseDescriptor> TestCases()
    {
        TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();
        foreach (TestSuiteDescriptor suite in ShoppingCartSuites.All)
            foreach (TestCaseDescriptor testCase in suite.Cases)
                if (!testCase.Skip)
                    data.Add(testCase);
        return data;
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task RunTest(TestCaseDescriptor testCase)
    {
        await ScenarioDiagnostics.ExecuteAsync(
            testCase, text => _Output.WriteLine(text), CancellationToken.None)
            .ConfigureAwait(false);
    }
}
```

Or use the **fact-style** pattern to run all descriptors as a single `[Fact]`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using MyTests;
using Touchstone.Core;
using Touchstone.XunitAdapter;
using Xunit;

public sealed class ShoppingCartFactTests : TouchstoneFactBase
{
    protected override IReadOnlyList<TestSuiteDescriptor> Suites
    {
        get { return ShoppingCartSuites.All; }
    }

    [Fact]
    public async Task RunAll()
    {
        await RunAllAsync();
    }
}
```

Run through the standard xUnit pipeline:

```bash
dotnet test
```

### 5. Run via NUnit

Create an NUnit test project that references your shared test library and `Touchstone.NunitAdapter`. Use **TestCaseSource** to get one NUnit test per descriptor. The same scenario definitions write their step diagnostics to NUnit progress output:

```csharp
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using MyTests;
using NUnit.Framework;
using Touchstone.Core;
using Touchstone.NunitAdapter;

[TestFixture]
public sealed class ShoppingCartNunitTests
{
    private static IEnumerable TestCases()
    {
        return new TouchstoneTestCaseSource(ShoppingCartSuites.All);
    }

    [Test]
    [TestCaseSource(nameof(TestCases))]
    public async Task RunTest(TestCaseDescriptor testCase)
    {
        await ScenarioDiagnostics.ExecuteAsync(
            testCase, text => TestContext.Progress.WriteLine(text), CancellationToken.None)
            .ConfigureAwait(false);
    }
}
```

Or use the **single-test** pattern to run all descriptors in one `[Test]`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using MyTests;
using NUnit.Framework;
using Touchstone.Core;
using Touchstone.NunitAdapter;

[TestFixture]
public sealed class ShoppingCartNunitFactTests : TouchstoneNunitBase
{
    protected override IReadOnlyList<TestSuiteDescriptor> Suites
    {
        get { return ShoppingCartSuites.All; }
    }

    [Test]
    public async Task RunAll()
    {
        await RunAllAsync();
    }
}
```

### 6. Run via MSTest

Create an MSTest test project that references your shared test library and `Touchstone.MstestAdapter`. Use **DynamicData** to get one MSTest test per non-skipped descriptor. Send the scenario diagnostics to the native TestContext:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyTests;
using Touchstone.Core;
using Touchstone.MstestAdapter;

[TestClass]
public sealed class ShoppingCartMstestTests
{
    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> TestCases()
    {
        return TouchstoneDynamicData.FromSuites(ShoppingCartSuites.All);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases))]
    public async Task RunTest(TestCaseDescriptor testCase)
    {
        await ScenarioDiagnostics.ExecuteAsync(
            testCase, text => TestContext.WriteLine(text), CancellationToken.None)
            .ConfigureAwait(false);
    }
}
```

Or use the **single-test** pattern to run all descriptors in one `[TestMethod]`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyTests;
using Touchstone.Core;
using Touchstone.MstestAdapter;

[TestClass]
public sealed class ShoppingCartMstestFactTests : TouchstoneMstestBase
{
    protected override IReadOnlyList<TestSuiteDescriptor> Suites
    {
        get { return ShoppingCartSuites.All; }
    }

    [TestMethod]
    public async Task RunAll()
    {
        await RunAllAsync();
    }
}
```

All framework adapters work with `dotnet test`:

```bash
dotnet test
```

## Console Runner Output

With the default sink, the walkthrough's direct check and scenario each occupy one row. The skipped descriptor retains its reason in the collected results. Timings below are illustrative.

```text
Test                                              Result   Runtime
------------------------------------------------  ------  --------
A new cart starts empty                           PASS        1ms
Adding an item increases the cart total            PASS        2ms
Discount codes reduce the total                   SKIP        0ms

OVERALL                                           PASS        3ms
Total: 3  Passed: 2  Failed: 0  Skipped: 1
```

With `ExpandSteps = true`, the scenario also reports progress while each step executes:

```text
  Cart.AddingAnItem GIVEN the cart contains a $10 item...
    Passed 0ms
  Cart.AddingAnItem WHEN a $25 item is added...
    Passed 0ms
  Cart.AddingAnItem THEN the cart total is $35...
    Passed 0ms
  Cart.AddingAnItem THEN the cart contains two items...
    Passed 0ms
Adding an item increases the cart total            PASS        2ms
```

If the total assertion fails, its exception appears in the failure summary and the item-count step becomes NotRun. Collected results and JSON retain the complete step sequence, including unexecuted steps. Set `ConsoleResultSink.Redact` to sanitize names and messages before console or JSON output, and pass the same function to the HTML writer when needed.

The repository's console hosts accept `--steps`, `--results <path>`, and `--html <path>`. Those flags are implemented by the host programs; custom console applications configure the sink and writers as shown above. The sample host also accepts `--fail` to demonstrate intentional failures.

## Telemetry

Touchstone emits metrics and traces on a `Meter` and `ActivitySource` both named `Touchstone`. Nothing leaves the process unless your host subscribes, for example with [Radiant](https://www.nuget.org/packages/Radiant) or the OpenTelemetry SDK:

```csharp
RadiantSettings settings = new RadiantSettings("my-integration-tests");
settings.Otlp.Endpoint = "http://127.0.0.1:4317";
settings.Sources.AddMeter("Touchstone");
settings.Sources.AddActivitySource("Touchstone");

using (RadiantHost host = RadiantHost.Start(settings))
{
    return await ConsoleRunner.RunAsync(ShoppingCartSuites.All);
}
```

What you get:

- **Traces**: `touchstone.run` > `suite:<SuiteId>` > `stage:before_suite`, `case:<Suite>.<Case>`, `sink <event>`, `stage:after_suite`, plus `export json` and `export html` from the reporting layer. Scenario cases add `scenario:context`, `scenario:step`, and `scenario:cleanup` child activities. Failed spans have `Error` status and an `exception` event. The case span is current while the test body runs, so HTTP calls the test makes carry its W3C `traceparent` and the server-side trace joins the test's trace.
- **Metrics**: run, suite, stage, case, sink, and export counters and duration histograms (seconds) by outcome; `touchstone.errors` by stage and `error.type`; `touchstone.cases.active`; last-success timestamps per suite and per run; `touchstone.build.info`. Scenarios also record context, step, and cleanup operation counts and durations with bounded kind/outcome labels.

For theory-style hosts (xUnit `MemberData`, NUnit `TestCaseSource`, MSTest `DynamicData`), call `TestExecutor.ExecuteCaseAsync(testCase, ct)` so each row is instrumented. It rethrows the original exception. `TestExecutor.RunAsync(suites, sink, ct)` runs several suites under one run span, and `TestResultCollector` is a ready-made sink that keeps results in memory.

[TELEMETRY.md](TELEMETRY.md) has the full metrics and spans catalog, configuration (`TouchstoneTelemetry.SuiteLabelEnabled`), recommended PromQL alerts, and suggested dashboard panels.

## Sample App

The repository includes **Touchstone.SampleApp**, a reference Notes API with direct integration checks and BDD scenarios for persistence, missing notes, and invalid input. Every scenario owns an isolated in-process application and runs through the same console, xUnit, NUnit, and MSTest hosts:

```bash
# Run tests via the console runner
dotnet run --project tests/Touchstone.SampleApp.Tests.Console

# Inspect live scenario steps and generate an offline report
dotnet run --project tests/Touchstone.SampleApp.Tests.Console -- --steps --html notes.html

# Run the same definitions through each framework
dotnet test tests/Touchstone.SampleApp.Tests.Xunit
dotnet test tests/Touchstone.SampleApp.Tests.Nunit
dotnet test tests/Touchstone.SampleApp.Tests.Mstest

# Run both back to back
go.bat
```

Browse [NotesScenarios.cs](tests/Touchstone.SampleApp.Tests.Shared/NotesScenarios.cs) and [NotesScenarioContext.cs](tests/Touchstone.SampleApp.Tests.Shared/NotesScenarioContext.cs) for a complete HTTP example. The same pattern used for the cart applies to application clients, responses, assertions, and resource cleanup.

## Testing Touchstone

Touchstone tests itself using ordinary descriptors and BDD scenarios in the same shared suites. Scenario contract checks cover lifecycle, isolation, failures, cancellation, reporting, and telemetry across all hosts. The test projects follow the same layout recommended for consumers:

| Project | Purpose |
|---|---|
| `src/Test.Shared` | Central source of truth. Every suite lives here, exposed through `TouchstoneSuites.All`. No console output. |
| `src/Test.Automated` | Console runner (`ConsoleRunner`) with optional `--results <path>` JSON export. Multi-targeted, so pass `-f net8.0` or `-f net10.0` to `dotnet run`. |
| `src/Test.Xunit` | Runs the shared suites under xUnit (fact-style and theory-style). |
| `src/Test.Nunit` | Runs the shared suites under NUnit (single-test and `TestCaseSource`). |
| `src/Test.Mstest` | Runs the shared suites under MSTest (single-test and `DynamicData`). |

Because Touchstone is the project under test, these projects reference the Touchstone source projects rather than the published NuGet packages. Referencing both would load two assemblies with the same identity.

```bash
dotnet run --project src/Test.Automated -f net10.0
dotnet run --project src/Test.Automated -f net8.0 -- --results results.json
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
dotnet test src/Test.Mstest
```

The [benchmark harness](BENCHMARKING.md) measures delegate and scenario execution overhead with listeners on and off; [recorded results](benchmarks/RESULTS.md) include methodology and limitations.

## Target Frameworks

Touchstone targets both **net8.0** and **net10.0**. The library packages (Core, Cli, and all adapters) and the `src/Test.*` projects are multi-targeted. The sample app and its test projects target net10.0.

## Version History

Refer to [CHANGELOG.md](CHANGELOG.md) for a detailed version history.

## Issues and Feedback

Have a bug report, feature request, or question? Please open an issue:

https://github.com/jchristn/touchstone/issues

## License

Touchstone is available under the MIT license. Refer to [LICENSE.md](LICENSE.md) for the full text.
