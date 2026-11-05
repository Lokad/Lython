# Focused basic-loop cost gate

2026-11-05, f8ebdc7c/same productionb69a0a49, restored/nonincrementally rebuilt
core2f272682, workerb94. D57 prototype absent. One25-second owned collection
within45s, complete10,000-value Python range sum,6guest warmup seconds and8192jobs.
NormalGC/tiering; diagnostic PerfMapEnabled only. Earlier CSV/text capture did
not cover this workload.

| Guest CPU leaf | Samples /912 | Share |
| --- | ---: | ---: |
| Instruction prefix |381|41.78%|
| StoreExecutableLocal |129|14.14%|
| AddExactIntegers |76|8.33%|
| Range Iterate.MoveNext |70|7.68%|
| ExecuteOperandValueOperation |44|4.82%|
| RetireTo |31|3.40%|
| EvaluateAugmentedAssignment |28|3.07%|
| BigInteger constructor |15|1.64%|
| MapExecutableAugmented |12|1.32%|

Actual guest CPU4.6s; unknown43/912(4.71%), one truncated map line retained.
Collector453267wakeups/920total samples. Prefix/store include real Python work,
**not removable overhead percentages**. Inlining/profiler bias limit attribution.
No elapsed/allocation/causalCPUgain/RSS/host-latency claim.

Quiet endpoints/exclusive lease/actual named guest TIDs/source-library-goldens/
verification before-after/Closed/final-zero/worker-profiler joins/fixture removal
pass. Catalog/worker/maps/raw evidence stay persistent; guest fixtures alone use
/dev/shm. All adverse/missing-map conditions remain in the raw archive.

Generic range/boxed integers perform real work but do not justify a broad scalar
representation change. Exact list/tuple native iteration already exists. Store/
worker no-inlining fences have documented temporary/result lifetime purposes;
preserve those fixes. Source review instead exposes terminal entry failure
skipping saved-handler restoration: select bounded D59 frame-exit consolidation.
Larger call/operand/frontend work stays open; local update transport needs its
own complete contract/control. This gate is closed; no general profile replay.

[Contract](source-contract.md), [identities](identity.json), [validation](validation.json),
[raw maps/stacks/source](observations.tgz).
ArchiveSHA256`b5c9af20b7e5b3fc43119a348315ccfbe1a3eacdb45b610af74b6dd8aac7dc9c`.
