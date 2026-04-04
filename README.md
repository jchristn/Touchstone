<img src="https://raw.githubusercontent.com/jchristn/Touchstone/main/assets/logo-black.png" alt="Touchstone" width="256" height="256">

# Touchstone

Runner-agnostic test descriptor framework for .NET.

v0.1.12

## What is Touchstone?

Touchstone is a .NET framework that lets you write **test descriptors** once and run them through any host. Define your integration tests as plain descriptor objects in a shared library, then execute them via xUnit for CI pipelines or via the built-in console runner for fast local development -- without changing a single line of test logic.

The core idea is simple: a test is a description (name, identity, execution delegate, optional setup/teardown) rather than an annotated method bound to a specific framework. Touchstone provides the descriptor model, the execution engine, and adapters that plug into the hosts you already use.

## Why Touchstone?

- **Write once, run anywhere** -- the same test descriptors work in xUnit, NUnit, MSTest, the console runner, or any custom runner you build.
- **Clean separation between test definitions and test execution** -- descriptors live in a shared project with no runner dependencies.
- **Readable local output without needing `dotnet test`** -- the console runner produces colored, tabular results directly in your terminal.
- **No lock-in to a specific test framework** -- switch runners without rewriting tests.
- **Runner-agnostic core with no dependencies on xUnit, NUnit, etc.** -- Touchstone.Core is a pure .NET library.
- **Optional suite lifecycle hooks (setup/teardown) without making them mandatory** -- add them when you need them, skip them when you don't.
- **First-class skip support with reasons** -- mark descriptors as skipped and the reason propagates to every runner.
- **Full exception capture, not just messages** -- test results preserve the complete exception, including stack traces and inner exceptions.
- **JSON export** -- the console runner can write structured results to a file for downstream processing.

## Packages

| Package | Description |
|---|---|
| **Touchstone** | Metapackage that includes Core, Cli, and all framework adapters. Install this to get everything. |
| **Touchstone.Core** | Runner-agnostic descriptor model, execution engine, and result types. No third-party dependencies. |
| **Touchstone.Cli** | Console test runner with colored tabular output, JSON export, and exit code contract. |
| **Touchstone.XunitAdapter** | xUnit adapters (theory-driven and fact-style) for running shared descriptors under `dotnet test`. |
| **Touchstone.NunitAdapter** | NUnit adapter (TestCaseSource-driven and single-test) for running shared descriptors under NUnit. |
| **Touchstone.MstestAdapter** | MSTest adapter (DynamicData-driven and single-test) for running shared descriptors under MSTest. |

## Getting Started

### 1. Install Packages

The simplest approach is to install the metapackage, which includes everything:

```bash
dotnet add package Touchstone
```

Or install only the packages you need:

```bash
# Shared test descriptor library (no runner dependency)
dotnet add package Touchstone.Core

# Console runner project
dotnet add package Touchstone.Cli

# xUnit test project
dotnet add package Touchstone.XunitAdapter

# NUnit test project
dotnet add package Touchstone.NunitAdapter

# MSTest test project
dotnet add package Touchstone.MstestAdapter
```

### 2. Define Test Descriptors

Create a shared class library that references only `Touchstone.Core`. Define your test suites and cases as descriptors:

```csharp
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Touchstone.Core;

public static class MyApiSuites
{
    public static IReadOnlyList<TestSuiteDescriptor> All
    {
        get { return new List<TestSuiteDescriptor> { HealthSuite() }; }
    }

    public static TestSuiteDescriptor HealthSuite()
    {
        HttpClient client = CreateTestClient();

        return new TestSuiteDescriptor(
            suiteId: "Health",
            displayName: "Health Checks",
            afterSuiteAsync: ct =>
            {
                client.Dispose();
                return ValueTask.CompletedTask;
            },
            cases: new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(
                    suiteId: "Health",
                    caseId: "ReturnsOk",
                    displayName: "GET /health returns 200",
                    executeAsync: async ct =>
                    {
                        HttpResponseMessage response = await client.GetAsync("/health", ct);
                        if (response.StatusCode != HttpStatusCode.OK)
                            throw new System.Exception("Expected 200 OK");
                    }),

                new TestCaseDescriptor(
                    suiteId: "Health",
                    caseId: "PaginationNotReady",
                    displayName: "Pagination support",
                    skip: true,
                    skipReason: "Not yet implemented",
                    executeAsync: _ => Task.CompletedTask),
            });
    }

    private static HttpClient CreateTestClient()
    {
        // Replace with WebApplicationFactory or any HttpClient setup.
        return new HttpClient();
    }
}
```

### 3. Run via Console

Create a console application that references your shared test library and `Touchstone.Cli`:

```csharp
using Touchstone.Cli;

return await ConsoleRunner.RunAsync(MyApiSuites.All);
```

Run it directly:

```bash
dotnet run --project MyTests.Console
```

The runner prints colored tabular results to stdout and returns a non-zero exit code if any test fails.

Export results to JSON:

```csharp
return await ConsoleRunner.RunAsync(MyApiSuites.All, resultsPath: "results.json");
```

### 4. Run via xUnit

Create an xUnit test project that references your shared test library and `Touchstone.XunitAdapter`. Use the **theory-driven** pattern to get one xUnit test per descriptor:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Touchstone.Core;
using Xunit;

public sealed class MyApiTheoryTests
{
    public static TheoryData<TestCaseDescriptor> TestCases()
    {
        TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();
        foreach (TestSuiteDescriptor suite in MyApiSuites.All)
            foreach (TestCaseDescriptor testCase in suite.Cases)
                if (!testCase.Skip)
                    data.Add(testCase);
        return data;
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task RunTest(TestCaseDescriptor testCase)
    {
        await testCase.ExecuteAsync(CancellationToken.None);
    }
}
```

Or use the **fact-style** pattern to run all descriptors as a single `[Fact]`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Touchstone.Core;
using Touchstone.XunitAdapter;
using Xunit;

public sealed class MyApiFactTests : TouchstoneFactBase
{
    protected override IReadOnlyList<TestSuiteDescriptor> Suites
    {
        get { return MyApiSuites.All; }
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

Create an NUnit test project that references your shared test library and `Touchstone.NunitAdapter`. Use **TestCaseSource** to get one NUnit test per descriptor:

```csharp
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Touchstone.Core;
using Touchstone.NunitAdapter;

[TestFixture]
public sealed class MyApiNunitTests
{
    private static IEnumerable TestCases()
    {
        return new TouchstoneTestCaseSource(MyApiSuites.All);
    }

    [Test]
    [TestCaseSource(nameof(TestCases))]
    public async Task RunTest(TestCaseDescriptor testCase)
    {
        await testCase.ExecuteAsync(CancellationToken.None);
    }
}
```

Or use the **single-test** pattern to run all descriptors in one `[Test]`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Touchstone.Core;
using Touchstone.NunitAdapter;

[TestFixture]
public sealed class MyApiNunitFactTests : TouchstoneNunitBase
{
    protected override IReadOnlyList<TestSuiteDescriptor> Suites
    {
        get { return MyApiSuites.All; }
    }

    [Test]
    public async Task RunAll()
    {
        await RunAllAsync();
    }
}
```

### 6. Run via MSTest

Create an MSTest test project that references your shared test library and `Touchstone.MstestAdapter`. Use **DynamicData** to get one MSTest test per descriptor:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Touchstone.Core;
using Touchstone.MstestAdapter;

[TestClass]
public sealed class MyApiMstestTests
{
    public static IEnumerable<object[]> TestCases()
    {
        return TouchstoneDynamicData.FromSuites(MyApiSuites.All);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases))]
    public async Task RunTest(TestCaseDescriptor testCase)
    {
        await testCase.ExecuteAsync(CancellationToken.None);
    }
}
```

Or use the **single-test** pattern to run all descriptors in one `[TestMethod]`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Touchstone.Core;
using Touchstone.MstestAdapter;

[TestClass]
public sealed class MyApiMstestFactTests : TouchstoneMstestBase
{
    protected override IReadOnlyList<TestSuiteDescriptor> Suites
    {
        get { return MyApiSuites.All; }
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

The console runner produces a three-column tabular summary:

```
Test                                              Result   Runtime
------------------------------------------------  ------  --------
GET /health returns 200                           PASS       12ms
GET /notes/{id} returns the stored payload        PASS        3ms
Pagination support                                SKIP        0ms
POST /notes with empty body returns 400           FAIL        8ms

OVERALL                                           FAIL       23ms
Total: 4  Passed: 2  Failed: 1  Skipped: 1
Failed Tests:
  POST /notes with empty body returns 400
    Expected 400 BadRequest but got 200 OK
```

Pass `--results <path>` to write JSON results to a file. Pass `--fail` (in the sample app) to toggle an intentional failure for verifying failure rendering.

## Sample App

The repository includes **Touchstone.SampleApp**, a reference Notes CRUD API with shared integration tests that demonstrate both runners side by side:

```bash
# Run tests via the console runner
dotnet run --project tests/Touchstone.SampleApp.Tests.Console

# Run the same tests via xUnit
dotnet test tests/Touchstone.SampleApp.Tests.Xunit

# Run both back to back
go.bat
```

The sample app validates the API surface before adopting it in production consumer repos.

## Target Frameworks

Touchstone targets both **net8.0** and **net10.0**. The library packages (Core, Cli, Xunit) are multi-targeted. Sample and test projects target net10.0.

## Version History

Refer to [CHANGELOG.md](CHANGELOG.md) for a detailed version history.

## Issues and Feedback

Have a bug report, feature request, or question? Please open an issue:

https://github.com/jchristn/touchstone/issues

## License

Touchstone is available under the MIT license. Refer to [LICENSE.md](LICENSE.md) for the full text.
