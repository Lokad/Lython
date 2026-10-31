# Rejected executable activation-reuse prototype

**Rejected and removed.** The fixed two-observation comparison misses the plan's
material-gain criterion. Keep the existing interpreter lifecycle and fresh lexical
contexts; no idle activation cache, eligibility flag or new reservation remains.

Hypothesis: frame/context/array/reservation allocation ticks in the
[07c150a6 profiles](../2026-10-31-architecture-profiles/README.md) might identify a
dominant ordinary-helper cost. The prototype reused one inactive noncapturing
interpreter per code object, retaining operand/cache/control capacity under the
execution allowance. It detached each fresh context, locals and closure cells,
cleared exception/call/receiver state, excluded generators, preserved growth
preflights, and refunded idle capacity during joined execution cleanup.

Baseline is `07c150a6`; candidate is `9af55fe7` plus the exact `source.patch` in
[raw-results.tar.gz](raw-results.tar.gz). Neither label is a measurement of today's
HEAD. Non-regex inputs and hosts are unchanged by the intervening range-contract
commit. The archive preserves the rejected patch, collection source, every sample,
complete stdout/file goldens, loaded assembly hashes, toolchain identities and
machine gates. No dormant production prototype is retained.

## Bounded comparison

2026-10-31 UTC, dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs,
16 GiB; identical .NET 11 RC3 runtime. CPython 3.13.16 remains a context/control in
each existing micro command. This is a Lython old/new redesign experiment, not a
replacement for the [primary CPython table](../../../docs/BENCHMARKS.md).

The fixed 26-job collection takes **211.39 seconds**, within its 300-second
prospective budget, holding the shared VM lease. Both quiet-machine gates pass.
Each job has an external 30-second process-group deadline and joins workers.
Two predetermined observations cover warm execution and fresh compilation/run of
CSV/text/JSON helpers plus configuration/search/manifest controls. Text compilation
has a separate lane. Warmup is one second per engine, then two seconds of settling
and seven rotating-order batches targeting 25 ms. Ordinary worker GC/tiering is
enabled; compilation/startup are excluded from warm execution.

Warm medians, milliseconds per complete script:

| Script | Observation 1 old / new | Observation 2 old / new | Decision |
| --- | ---: | ---: | --- |
| CSV helpers | 7.727 / 7.810 | 7.980 / 7.557 | -1.1% / +5.3% point reductions |
| Text helpers | 6.027 / 5.551 | 5.782 / 5.559 | +7.9% / +3.9% point reductions |
| JSON-lines helpers | 7.871 / 7.869 | 8.223 / 7.773 | +0.0% / +5.5% point reductions |
| Configuration control | 2.807 / 2.799 | 2.950 / 2.998 | Mixed |
| File-search control | 1.984 / 1.840 | 1.986 / 2.318 | Inconclusive, 73–134% IQR |
| File-manifest control | 1.996 / 2.129 | 2.098 / 2.179 | Higher candidate medians |

No affected script reaches the intended repeatable 20% gain. CSV/JSON fresh
compile/run point reductions are only 2.1–5.1%; text fresh compile/run reverses
from 20.574/30.951 ms to 9.712/6.911 ms and is inconclusive. Text compilation-only
also reverses from 178/262 us to 210/182 us. All noisy/increased rows remain in
[collection.json](collection.json) and the raw archive. Quiet gates do not remove
JIT/GC/scheduling variance. No qualified speedup or causal allocation claim follows.

Focused prototype validation passes 397 internal and 540 public tests, plus two
new repeated-call lifetime checks with real host suspension. Those checks cover
closure-cell separation, overlapping native adapters, exception/finally state,
retained results and budget reuse; their useful public behavior is retained after
discarding the prototype. The architecture-specific idle-cache assertion is
removed with the code. Full platform/package qualification is unnecessary for a
discarded implementation and was not repeated.

This experiment stops here. Allocation volume alone did not establish a dominant
elapsed cost, and the small point gains do not justify additional cache/reset and
idle-ownership machinery. Inspect existing call-chain evidence before choosing
the next coherent redesign; do not tune or retime this cache into acceptance.
