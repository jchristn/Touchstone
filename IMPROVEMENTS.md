# Improvements

## After 1.0.0

| Work | Notes |
|---|---|
| Set `PackageValidationBaselineVersion` to 1.0.0 | Makes package validation fail on any breaking change against the shipped 1.0.0 packages. Release step 10 in archive/V1.0_PLAN.md. |
| `TestRunOptions.CaseTimeout` | Per-case timeout. Additive property; reserved in the 1.0 design. |
| `TestRunOptions.MaxDegreeOfParallelism` | Parallel case execution. Additive property; reserved in the 1.0 design. |
| Sample MSTest host duration | `Touchstone.SampleApp.Tests.Mstest` takes about 44 seconds for 11 cases, while the NUnit and xUnit sample hosts take 5 to 8 seconds for the same suites. Investigate WebApplicationFactory start-up under MSTest. |
| Sample suite resources | `SampleAppSuites` creates `WebApplicationFactory` instances while building suites and disposes them in `afterSuiteAsync`, which one-test-per-case hosts never run. Move them into scenario contexts or lazy fixtures. |

# Benchmark follow-up

| Priority | Work | Evidence needed | Status |
|---|---|---|---|
| 1 | Record a baseline on the dedicated CI benchmark machine | Matching hardware/runtime and two full comparable runs | Pending environment provisioning |
| 2 | Evaluate context/result allocation cost before optimizing | Profile the one-step and five-step arms; preserve lifecycle correctness | Pending measured bottleneck |
| 3 | Add representative HTTP workflows if consumer demand warrants it | Stable isolated service workload with independent correctness oracle | Deferred |

The current suite measures synthetic runner overhead, not real service latency. Any optimization must preserve original exceptions, context isolation, cleanup, and host parity. Record a reproduction and before/after measurements here when a regression is found.
