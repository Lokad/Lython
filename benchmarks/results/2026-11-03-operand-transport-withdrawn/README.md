# Compiled operator operands — B147 withdrawn

2026-11-03. Source based ond7cda681, runtime baseline exactf7dd0d3a/core2f78a866.
**WITHDRAWN**: no operand-plan types, compiler pass, new instruction fields or
operator/index dispatch changes remain in production. The original compiler
architecture assertion is restored. Twelve independent public semantic regression
cases remain as ExpressionEvaluationTests. The performance objective stays open.

The prototype attaches compiled constant/local/closure/global/name locations and
bound destinations to ordinary Binary/Augmented/Unary instructions, deleting
adjacent transfer instructions within a basic block. No new VM/opcode/type-specific
policy. Native-return bodies stay unchanged; existing workspace admission remains
an upper bound. Capture order, original store checks, callbacks/actual suspension,
refusal, cancellation, closures and indexed cleanup are preserved in focused tests.
Initial async-only failures exposed old scalar-dispatch stack assumptions; capture
and primitive selection were consolidated before timing. A statically rejected
unbound test was corrected against baseline behavior; no runtime diagnostic was relaxed.

## Decision and all timing observations

Entries are candidate elapsed change / identical baseline-control change, percent.
Negative is faster. Same retained baseline and identical auxiliary worker in each
trial. Eight existing complete jobs,4s warmup/image, nine rotating~200ms batches.

| Workload / phase | Initial | Capture refinement | Index-scope correction |
| --- | ---: | ---: | ---: |
| csv-summary-helpers / warm |-1.17 /-2.38|-0.19 /+0.21|-1.18 /-1.09|
| text-summary-helpers / warm |-2.24 /-3.14|-3.58 /-1.59|+4.43 /+5.20|
| report-render / warm |+2.89 /-0.36|+0.24 /-4.31|-1.46 /-4.75|
| csv-summary-helpers / compile-run |-0.87 /-2.41|-3.20 /-2.73|-2.77 /-0.92|
| text-summary-helpers / compile-run |-2.54 /-1.22|-1.15 /-0.74|+1.29 /-2.61|
| report-render / compile-run |-1.03 /+1.21|-0.78 /-2.02|-2.07 /-0.90|
| loops.integer / warm |-31.37 /-22.20|-14.11 /+0.14|-15.18 /+1.00|
| filename-filter-ascii / warm |+0.20 /-2.00|+4.79 /+1.97|+0.09 /-0.24|

The first range control drifts−22.20%; that result alone is inconclusive. The two
later controls are steadier and show a useful roughly14–15% range-loop improvement.
That benefit is real evidence in favor of operand work, not a portable multiplier.
Complete CSV/text/report do not establish a useful general gain. Report trails its
unchanged control consistently by roughly3–5%; fresh-script CPU/control drift also
limits attribution. Initial whole-job allocation changes are essentially zero
(text/ASCII exactly0%, CSV+0.003%, report−0.002%). Allocation was not retimed after
refinement; no allocation gain is claimed for any revision.

The capture refinement removes an extra cleanup/EH layer around pure operand
reads. A subsequent source audit found subscript/slice operands were captured
redundantly and then read from stack again despite having no plans. The scope
correction restores their original borrow/fast paths and limits new capture to
operators. Both refinements and all adverse observations are preserved; neither
is credited with a causal gain by comparing independent medians.

Decision weighs whole-script results and added metadata/analysis/dispatch
complexity, not a fixed percentage threshold. A range-only benefit does not justify
this implementation's added mechanism and script regression debt. Remove the
prototype, keep independent correctness tests and return to other coherent runtime
costs. This is not a permanent ban on operand/register work or a universal failure
claim. A later design needs a different source-backed simplification/cost model.

## Evidence and qualification

[Initial assessment](assessment.json), [capture refinement](refined/assessment.json),
[scoped assessment](scoped/assessment.json), source patches and complete raw responses
preserve every median/IQR, CPU sample, identity and golden. CPU is Linux process
user+system ticks including reset/transport/JIT, not guest-only CPU. Command-v2
elapsed excludes fixture reset; actual guest I/O, full checks and prior-result /
fresh-compilation disposal remain timed. The separate existing allocation driver
includes complete reset/check/result cleanup in one process with isolated images.
Its exact source/measurement limitations remain those of B141; no new framework.

Baseline core2f78a866; candidatesdb60977d /1a224ee6 /99e99b57. Full hashes and
source patches are in identities for each trial. No producer/policy/GC/tiering
configuration changes. Dedicated quiet F4as_v7 Ubuntu24.04.4/.NET11RC3, ordinary
worker GC/tiering; no external CPython refresh or source speedup from old guide rows.
All outputs/files/loaded identities and before/after gates PASS. Timing collections
177.150/178.080/180.577s; first owned service including allocation222.845s, later
178.682/181.178s, all exit0/inactive/lease free, workers/fixtures joined/removed.
The initial launch failed before timing due to a missing catalog filename; the
wiring correction and original failed conditions remain, with no discarded samples.

Prototype focused Release455 internal/390 public per actual.NET11/10 PASS, then
455/472 including indexed protocols for the final scope. The restored runtime is
rebuilt nonincrementally,0 warnings/errors;314 focused public cases including the
retained regressions PASS on each actual target. Source exactly matches the
retained runtime; no operand-plan hooks remain. Cached
41566551688 still qualifies the unchanged runtime; full platform/stress/package
qualification is not replayed for a withdrawn implementation. No publication/tag.
