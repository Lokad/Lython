# B167 completed-fault instruction prefix

2026-11-03. Retained source **433f7e36b769999dafaa08a5440082328e5ccdb6**,
core4e0c344639200ab9d701a99013b82174e49af940e93b49954c63ed3cf179b554.
Focused checks and bounded diagnostics PASS. Full **41663811073 GREEN** on the
exact retained source: Windows/Ubuntu, .NET 11/.NET 10, installed package consumers
and original stress scales. [Cached delivery receipt](delivery-ci-status.json).
Publication postponed; overall whole-script performance objective remains OPEN.

The shared driver used IsCompletedSuccessfully to select native consumption.
Already-faulted/cancelled operations therefore still traversed async forwarders.
Completion now uses IsCompleted in the existing instruction/call/member/iterator/
disposal plumbing: consume completed results, failures and cancellation once in
the native prefix; only actual incomplete operations enter await forwarders.
Nineteen completion predicates changed; genuine async helpers stay. No new fields,
framework, owners, counters, policies or public API. Original spans/guest routing,
first admission refusal, effective cancellation and awaited cleanup are preserved.

## Demonstrated failure and bounded correction

Prior full41651037909: Windows.NET11/.NET10, package consumers and original Stress
PASS; Ubuntu whitebox4040PASS but public copyhook guard child reaches unchanged
25s process bound, consuming26.81CPU s. This is a real adverse observation, not
proof of the exact cause or a reason to relax the deadline. Terminal failure/log
are retained. Preceding41647907097 had full Ubuntu PASS but Windows direct-Sum
allocation outlier; fixture-only76725065 fixed isolation/warmup with the same
8192 items/4096B limit. That assertion passes both platforms in41651037909.
Do not re-query either terminal attempt.

The original copyhook fixture is unchanged: real asynchronous host I/O, THREE
default-depth rejections/recoveries, no reduced nesting or enlarged timeout.
Quiet VM paired B162/B165 first completes10.43/11.33s (11.08/12.08CPU s). An
initial private fixture symlink canonicalized its appbase and failed to load core;
that failed condition is retained and has no runtime/timing interpretation.
Correct bootstrap copies then run successfully; no production/test weakening.

A separate paired B165/B167 witness through the same fixture and25s bound:

| Source | Elapsed | Process CPU | Result |
| --- | ---: | ---: | --- |
| Beforee25a7353 |11.0607s|11.7362s|exit0, three rejections, real async I/O|
| Candidate4e0c3446 |8.9735s|9.6264s|exit0, same checks/bound|

This one pair supports about19%lower elapsed/18%CPU for this guard/unwind workload.
It does not fully explain the original CI timeout or prove general error-path
latency. No per-fault allocation reduction was measured. Async relay removal is a
source fact; the timing is a bounded diagnostic, not broad qualification.

## Complete normal-path controls

Existing complete text/JSON file scripts, both APIs, before/candidate/unchanged
before-control in one process/CoreLib/hash. Normal GC/tiering, actual RAM file APIs,
128 warmups/image, nine rotated32-job batches and2ssettle/case. Complete timer
includes fixture reset/public invocation/I/O/full stdout/file checks/result disposal;
compilation/startup excluded. Correctness, balances, peaks, quiet gates and joins PASS.

| API/workload | Candidate ms | Elapsed change /unchanged control | CPU change /unchanged control |
| --- | ---: | ---: | ---: |
|sync/text-summary-helpers|3.7497|+1.51% /-2.05%|+0.13% /-0.66%|
|sync/json-summary-helpers|5.6885|-2.99% /-1.10%|-3.48% /-1.51%|
|async/text-summary-helpers|4.0271|+0.41% /-0.40%|+0.26% /+0.09%|
|async/json-summary-helpers|6.3348|+0.35% /-0.39%|+0.52% /-0.43%|

Allocation and accounted peaks are effectively unchanged. Sync text elapsed is
adverse relative to its control while CPU is flat; that uncertainty remains visible.
Do not attribute it to scheduling/code layout or claim a normal-path speedup/zero
impact. JSON sync is lower; async controls are near flat. Retain the simpler
completion invariant and demonstrated guard benefit; no further unchanged replay
is selected. Earlier B165 source-pair/CPython points remain their measured revisions.

## Validation

Release **5652 public/820 internal per actual.NET11/.NET10** PASS, including all34
owned stack scenarios with the original25s deadlines. Covers completed/genuine
async failure, generators/closures/protocols, original Python error spans, cleanup,
first refusal, cancellation, retained aliases and budget reuse. No fixture/input/
timeout/stack-policy changes. Full corrected delivery41663811073 PASS on both platforms and frameworks,
including installed package consumers and original stress scales.

One owned VM service64.541s, exit0; every fixture/control worker and native user
joins, shared lease free. Same quiet Standard_F4as_v7 Ubuntu24.04.4.NET11RC3
conditions as B165. Physical RSS/embedding latency beyond this witness unmeasured.
Exact source hashes/auxiliary images retained; candidate built before its source
commit. Only Lython-owned plumbing changed; no optional producer retiming.

[Normal assessment](assessment.json), [raw observations/guard/gates](observations.tgz),
[identity](identity.json), [source patch](source.patch), [source gate](source-gate.md),
[prior CI receipt](prior-ci-status.json), [prior Ubuntu failure](prior-ubuntu-failure.log),
[prior successful paired guard check](prior-paired-copyhook.json).
