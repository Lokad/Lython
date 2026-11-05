# Current complete-text CPU cost gate — capture complete

Retained source `afc3189e`, exact core `084aaa38` built before commit metadata;
full4150internal/16004public per actual11/10 pass. CI42088051016 separately
qualifies this source; at capture it was still running. D61's42081657838 is
GREEN/all8/cached. No new elapsed/allocation qualification was performed here.

One28s owned capture2026-11-05, within60s: complete
`scripts.text-summary-helpers.large`,3072invocations after6guest-second warmup,
unchanged workerb94, normal GC/tiering and diagnostic PerfMapEnabled only.
Dedicated Standard_F4as_v7/Ubuntu24.04.4 x64/AMD9V45x4/.NET11RC3. Actual mediated
RAM file IO, full checks and prior-result cleanup included; compilation separate.
Quiet endpoints/exclusive lease/source/library/catalog/fullgolden/Closed/finalzero/
actual guest-TIDs/worker-profiler joins/fixture-removal gates pass.
[Raw capture/maps/stacks](observations.tgz), [validation](validation.json),
[leaf accounting](guest-cpu-leaves.json), [identity](identity.json),
[source contract before collection](source-contract.md).

1332 guest leaf samples/6.7guestCPU seconds. Unknown96(7.21%) and one malformed/
truncated perf-map line preserved. Collector reports1374all-thread samples and
547453wakeups; these are not1332guest samples. Sampling and inline attribution
limit conclusions. Self costs include necessary work and cannot be treated as
removable percentages:

| Observed self cost | Samples | Guest samples |
| --- | ---: | ---: |
| Shared instruction prefix |258|19.37%|
| Ordinary call execution |88|6.61%|
| Cased-word discovery |69|5.18%|
| Subscript assignment |62|4.65%|
| Direct local result completion |52|3.90%|
| Exact integer addition |36|2.70%|
| UTF8 casing writer |31|2.33%|
| Native helper guard |29|2.18%|
| Target / ordinary-value handlers |26each|1.95%each|
| Monitor.Enter / Exit |24/22|1.80/1.65%|
| Native helper evaluator |24|1.80%|
| Hashability enforcement |22|1.65%|
| HashCode.ToHashCode |20|1.50%|
| Name resolution |20|1.50%|
| Whitespace split core |19|1.43%|

The older D52 inclusive native-helper/split shares do not establish current self
costs or removability. Nothing here proves boxing, frame construction, locking or
hashing dominates the whole workload. There is no causal CPUgain, elapsed,
allocation, RSS, startup or embedding-latency claim. D62's original/refined text
and JSON discrepancies remain unresolved; this profile does not explain them.

Bounded next source assessment: complete keyed mutation/validation path, affecting
text/CSV/JSON aggregation and compound-key grouping. Actual source shows
SetSubscriptValue pushes ambient state, dispatches the receiver, validates the
key, attaches its existing governor and stores/checks. ValidateHashableKey checks
shape, recursively validates tuple keys and then EnsureHashableValue computes
PyValueComparer.GetHashCode; native dictionary storage computes that hash again.
These are demonstrated repeated computations, not a measured removable share.

Before any prototype, audit the IPyHashableValue contract/actual storage exception
boundary, tuple cycle/error behavior and guest protocols. Assess removing
redundant hash-enforcement/traversal in favor of the actual hash operation, keeping
preflight-before-mutation, Python-shaped failures, identity/order and per-operation
guest callback counts. Prefer deleting validation machinery over new prehashed
key wrappers, per-object ledgers, caches or special opcodes. Do not assume all
validation is redundant or skip a promised check. Record displaced work/storage,
semantic/token/admission acceptance and bounded complete-workload pair/control
before code. If that proof fails, stop/refine candidate and continue the broader
roadmap; no extra profiling campaign or permanent idea ban.

The substantial whole-script performance objective remains open. Publication is
postponed; all documentation and implementation remain Lython-centric.
