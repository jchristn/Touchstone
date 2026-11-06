# Scenario overhead results

Round: 2026-11-05, release 1.0.0, .NET SDK 10.0.403 and runtime 10.0.14. Source commit: `434e19be40c1747623f567c3b0bdaa844db8c397`. The first 1.0.0 round, on SDK 10.0.401 at commit `a2991d4`, is in git history. Both retained runs used a clean working tree and matching source, dataset, configuration, SDK/runtime, and machine metadata. Each run contains 96 measured samples after two discarded warmup rounds.

The confirmation comparison reports no regression. The paired gate uses a 20 percent tolerance plus bootstrap significance; timing and peak-memory regressions also require a p95 increase. A future baseline should be collected on the actual dedicated CI benchmark machine.

The table shows mean operation time and mean allocated bytes for 10,000 cases per non-floor workload. Timings include runner/result/formatting costs inside a cold process but exclude process startup and descriptor construction. The floor is an empty suite, not 10,000 tests.

| Arm | Listeners | Mean operation ms | Mean allocated bytes |
|---|---|---:|---:|
| floor | off | 7.13 | 22312 |
| floor | on | 7.76 | 29368 |
| delegate | off | 29.87 | 10602120 |
| delegate | on | 43.56 | 28848124 |
| one | off | 50.06 | 19798976 |
| one | on | 74.17 | 52844336 |
| many | off | 75.27 | 35975480 |
| many | on | 112.53 | 89822784 |

Compared with the 0.3.0-alpha round (delegate off 14.44 ms, one off 26.22 ms, many off 39.55 ms), 1.0 costs about 1.6 to 3.7 microseconds more per case. Two things changed. The workload now builds 10,000 distinct descriptors, because 1.0 rejects the duplicate identities the old workload reused. The engine now validates identities, detects duplicates, applies the filter, and snapshots options for every run. The empty-suite floor also rose by about 2 ms, so part of the difference is the machine rather than the engine. The 1.0 baseline replaces the 0.3.0-alpha baseline; the confirmation comparison against it passes.

Structured steps increase execution time and allocations in this synthetic no-work workload. Keep ordinary descriptors for short checks; adopt scenarios when the additional diagnostic detail is useful. No application-level latency claim follows from these numbers. Both full reports include operation time, process time, allocation, peak memory, and provenance. The matching second run passed the gate; use the saved paired differences and intervals to inspect noise before tightening the budget.

Reproduce with `bash benchmarks/run-baseline.sh`, then compare against `benchmarks/baselines/macos-arm64.json` on a matching machine. `python3 benchmarks/verify-gate.py` checks comparison failure behavior using deterministic mutations of the committed report.
