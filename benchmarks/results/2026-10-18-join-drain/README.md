# str.join drain order and cleanup, 2026-10-18 UTC

Both hosting APIs now consume an iterable before checking item types. An invalid
first item no longer prevents later generator effects or masks a later iteration
exception. The synchronous baseline fails seven permanent semantic cases; both
APIs also leave ten bytes of output scratch charged on a later invalid item.
These are reproduced baseline defects, not inferred performance causes.

The [two-file runtime patch](candidate.patch) uses the existing leased drain in
both APIs through output admission, consolidates two item validators and releases
the existing UTF-8 builder in finally. It adds no owner, scope API, tracing,
builder, cache, direct-call route or generic iteration framework. Retained input
aliases and final output keep their existing owners; asynchronous callbacks and
cleanup remain awaited. Conservative capacity/refusal semantics remain terminal.

Baseline: `deefa4a50d3c6d1d0e864557ce00fdaef4c9e4e7`; candidate: that revision plus
the frozen patch, including the new permanent regressions. [Scope](scope.json)
and diagnostic were frozen before production edits. [Baseline failures](baseline-regressions.json)
retain the seven semantic and two cleanup witnesses.

| Native allocation per call, 16 Unicode parts | Baseline | Candidate | Change |
| --- | ---: | ---: | ---: |
| Exact list, Run | 2,040 B | 2,456 B | +416 B |
| Generator, Run | 3,256 B | 3,672 B | +416 B |
| Exact list, RunAsync | 2,456 B | 2,496 B | +40 B |
| Generator, RunAsync | 3,680 B | 3,720 B | +40 B |

This is a correctness/ownership change with a measured allocation cost, not a
performance improvement. Synchronous execution now materializes its input;
async execution retains an existing drain lease instead of releasing scratch
before conversion. Extra temporary capacity and overlap may reject earlier.
Neither identical tiny thresholds nor arbitrary reference reclamation is promised.
See [MEMORY.md](../../../MEMORY.md) and [SPEC.md](../../../SPEC.md).

One short allocation pair used 2 warmups, 8 samples and 64 calls per sample for
each route. [Diagnostic](diagnostic.cs), [allocation arrays](allocation.json),
[baseline TRX](baseline-native.trx), [candidate TRX](candidate-native.trx) and
process receipts preserve the evidence. The baseline TRX includes four successful
allocation routes and the two expected cleanup failures; candidate has six passes.
The temporary diagnostic was removed. The experiment was not repeated.

Local Windows x64 build26200, Intel Core i7-14700KF (28 logical processors),
.NET SDK10.0.300-preview.0.26177.108/runtime10.0.13. [Environment](environment.json)
reuses unchanged configuration with its original capture timestamp. This measures
warm expression allocation on completed operations using one context, excluding
compilation, setup, scheduling, startup and public projection. No elapsed-time,
complete-job, CPython speedup or total pending-I/O allocation claim.

All 528 distinct focused Release cases pass, including58 complete CSV/text/JSON/
file jobs, managed stack, drain/builder cleanup, terminal refusal, caches/retained
results and shared-budget cleanup. Twenty new public cases cover16 pure programs/
API combinations, actual suspended/cancelled host I/O and retained results.
Six new source-linked cases cover invalid-item cleanup and result admission with
both drain and output builder live, under shared and per-run limits.
[Eight isolated CPython3.13.2 goldens](goldens.json) independently match the permanent
expected output; both Lython APIs pass those cases. [Validation](validation.json)
records filters, receipts and TRX hashes. Original Stress scales/assertions/timeouts
remain. Release/package and cross-platform qualification are recorded in PLAN;
regex stays a separate plan-only migration.
