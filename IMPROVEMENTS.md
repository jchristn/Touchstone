# Benchmark follow-up

| Priority | Work | Evidence needed | Status |
|---|---|---|---|
| 1 | Record a baseline on the dedicated CI benchmark machine | Matching hardware/runtime and two full comparable runs | Pending environment provisioning |
| 2 | Evaluate context/result allocation cost before optimizing | Profile the one-step and five-step arms; preserve lifecycle correctness | Pending measured bottleneck |
| 3 | Add representative HTTP workflows if consumer demand warrants it | Stable isolated service workload with independent correctness oracle | Deferred |

The current suite measures synthetic runner overhead, not real service latency. Any optimization must preserve original exceptions, context isolation, cleanup, and host parity. Record a reproduction and before/after measurements here when a regression is found.
