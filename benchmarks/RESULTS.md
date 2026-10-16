# Scenario overhead results

Round: 2026-10-16, release 0.3.0-alpha. Source commit: `454907e4d14c717e6005447b7cb7bc38dda790ef`. Both retained runs used a clean working tree and matching source, dataset, configuration, SDK/runtime, and machine metadata. Each run contains 96 measured samples after two discarded warmup rounds.

The confirmation comparison reports no regression. The paired gate uses a 20 percent tolerance plus bootstrap significance; timing and peak-memory regressions also require a p95 increase. A future baseline should be collected on the actual dedicated CI benchmark machine.

The table shows mean operation time and mean allocated bytes for 10,000 cases per non-floor workload. Timings include runner/result/formatting costs inside a cold process but exclude process startup and descriptor construction. The floor is an empty suite, not 10,000 tests.

| Arm | Listeners | Mean operation ms | Mean allocated bytes |
|---|---|---:|---:|
| floor | off | 4.93 | 18480 |
| floor | on | 5.80 | 26088 |
| delegate | off | 14.44 | 9165504 |
| delegate | on | 22.96 | 21334816 |
| one | off | 26.22 | 19492454 |
| one | on | 45.41 | 46700560 |
| many | off | 39.55 | 35414752 |
| many | on | 74.19 | 84386460 |

Structured steps increase execution time and allocations in this synthetic no-work workload. Keep ordinary descriptors for short checks; adopt scenarios when the additional diagnostic detail is useful. No application-level latency claim follows from these numbers. Both full reports include operation time, process time, allocation, peak memory, and provenance. The matching second run passed the gate; use the saved paired differences and intervals to inspect noise before tightening the budget.

Reproduce with `bash benchmarks/run-baseline.sh`, then compare against `benchmarks/baselines/macos-arm64.json` on a matching machine. `python3 benchmarks/verify-gate.py` checks comparison failure behavior using deterministic mutations of the committed report.
