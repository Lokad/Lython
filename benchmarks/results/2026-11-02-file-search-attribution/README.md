# Complete file-search attribution (B79)

2026-11-02, source `5b30efd2`, runtime source `4cae38f4`. Frozen benchmark core
matches the explicitly installed correction package. One owned VM service completes
in **10.053 seconds**, including a **3.00184-second** CPU capture. Full goldens,
both quiet gates, final zero allowance, worker/profiler joins and fixture removal
pass. No runtime candidate, retiming, steady-state result or CPython ratio.

## Workload and boundaries

Unchanged `scripts.file-search.large`: 42,010 input bytes, 1,024 ordinary Unicode
lines, one chosen line and an unread trailing region. The script calls a small
`wanted(line)` helper using startswith, exits its protected loop early, then writes
the chosen line. Pinned isolated CPython 3.13.16 verifies full stdout/file goldens
without collecting Python timing.

Azure Standard_F4as_v7 / Ubuntu 24.04, four EPYC 9V45 vCPUs, .NET 11 RC3, ordinary
GC/tiering. The existing success-only phase driver measures 128 warmups and 16
observations per reflected/public warm/public fresh lane. Actual mediated host
I/O and cleanup remain inside their phases; reset/verification are outside these
diagnostic clocks. Compilation is nested in each fresh observation and recorded
separately. Startup is not measured.

| Diagnostic boundary | Median us |
| --- | ---: |
| Reflected total before verification | 4,035.90 |
| Reflected context initialization | 42.40 |
| Reflected execution including host operations | 3,953.60 |
| Reflected cleanup / execution-owner disposal | 0.40 / 0.40 |
| Public warm total | 4,023.65 |
| Host elapsed nested in public warm | 110.85 |
| Public warm less its paired host elapsed | 3,912.04 |
| Public fresh total | 4,476.75 |
| Compilation nested in fresh observations | 469.35 |

These are independent diagnostic medians, not additive CPU shares. Reflection,
decorators and stopwatch overhead remain. In particular, do not subtract these
phase-process observations from timings in the separate profiler worker.

## CPU capture and attribution limits

An unchanged framed worker uses 128 warmups, two-second settling and single-job
requests until a three-second monotonic deadline; 1,364 jobs finish. Compilation
and startup are outside capture; fixture reset, actual I/O, full verification and
declared cleanup remain inside it. perf cpu-clock/fp samples at 199 Hz. Original
maps, JIT bytes and all recorded threads are preserved.

The actual named guest thread has 0.72 seconds CPU / 139 samples, including 30
ExecuteUntilCall self samples and 13 unknown leaves. The separate tiered-JIT thread
has 0.80 seconds CPU / 158 samples. Thread-pool workers have another 0.59 seconds
CPU; precise assembly attribution finds no core self samples there and only one
core-inclusive waiting sample. Their benchmark/transport work is not reassigned
to the interpreter. Active tiering and profiler overhead prevent stable latency
or removable-cost conclusions.

Memory reservation resizing appears in seven monitor-origin chains, with other
monitor origins in stack construction and checked iteration. This is not a lock
CPU fraction. Source already uses one shared budget gate, native UTF-8 line
scanning and direct prefix dispatch. No missing conversion pipeline or overlapping
scope-lock hierarchy is demonstrated. Do not weaken admission/cancellation or
replay closed frame/dispatch/casing proposals from these small distributed counts.

## Corrected symbol classification

A signature substring is insufficient: benchmark methods can mention Lython
parameter or return types. The current offline analyzers inspect the declaring
type before `::` and conservatively reject incomplete generic owners. Four
controls cover real core methods and false parameter, return and generic-argument
matches. B79's thread-pool core-inclusive classification drops from 46 to 1;
the historical B77 core-self totals remain 436/279 on audit. Raw events and
timings are unchanged. The original derivation stays inside the raw archive;
the readable current derivations use the correction.

**Close this bounded assessment without a runtime change.** The source and capture
do not identify a coherent dominant removable mechanism. Fresh compile-to-result
is the next priority by the stated default, pending user steering; a new frontend
design needs its own source/cost gate rather than another local runtime tweak.

[All phases and decisions](summary.json), corrected per-thread/origin records,
[collection plan](pre-collection-plan.md), unchanged driver and
[raw evidence](evidence.tar.gz), SHA256
`2aef588e9b39bb102f3a9fb73b07db8003a423d465feb17d2773940596b8b1df`,
preserve every observation and limit. Windows/Ubuntu delivery of unchanged runtime
`4cae38f4` remains covered by cached terminal workflow 41357098300; it is not rerun.
