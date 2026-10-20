# Complete text helper: bounded managed profile

2026-10-20 UTC, source `522e2f53347fbf089311ad5400aa53b84634e863` and qualified
runtime `f0cd695fd883915f0989a6fb88844c35f3499d57`. One workload collection ran
384 fresh compile/run invocations of `scripts.text-summary-helpers.large`.
Every invocation checked complete stdout and report bytes through the existing
worker. The batch completed, both processes exited zero, the reader joined and
the owned fixture directory was removed. No runtime or test source changed.

**Performance attribution is inconclusive.** Waiting threads dominate global
managed thread-time reports. Filtering the exporter's `CPU_TIME` sample-state
marker exposes iteration, helper-call, reservation and splitting paths, but CLR
monitor/GC-poll helpers dominate their leaf samples. These interval weights do
not establish hardware CPU shares, lock contention, GC pause counts, allocation
frequency or avoidable work. Inclusive call paths overlap. No runtime edit,
budget relaxation, lock removal, integer shortcut or repeated profile follows.

| Observed boundary | Exported interval weight | Interpretation |
| --- | ---: | --- |
| Actual execution-worker interpreter, inclusive | 3,592 ms | Present during the job; overlaps callees |
| Caller waiting for the synchronous worker, inclusive | 3,616 ms | Waiting overlaps execution; cannot be added as CPU |
| Public compilation call, inclusive | 204 ms | One path; not separate compile-cost qualification |
| Reservation.TryResize, managed-state inclusive | 741 ms | Admission path is present; no measured call frequency or avoidable cost |
| SplitWhitespace, managed-state inclusive | 322 ms | A concrete text boundary, without demonstrated optimization |
| Monitor.Enter_Slowpath, managed-state leaf | 2,121 ms | Does not establish contention or its cause |
| Thread.PollGC, managed-state leaf | 847 ms | Does not establish GC pause time or full collections |

Source inspection retains the shared allowance's synchronized admission: it is a
required concurrency invariant. Ordinary small counter additions already skip
separate magnitude admission. The profile supplies no justification for removing
those checks or reviving rejected arithmetic/casing/dispatch prototypes.
Stop this investigation at the recorded uncertainty.

The maintained 424-byte Python helper script reads 3,072 lines / 35,840 input
bytes, normalizes and counts words, sorts keys and writes the whole report.
Its stdout is `6144\n`; the report contains the independent expected counts for
alpha, beta, café and supplementary Unicode. Source, fixture, stdout and file
digests are checked by the existing worker's verification and compile-run lane.
Verification reused code for two untimed controls; the profiled batch compiled
source afresh per invocation. Input staging/reset, execution, full output checks
and prior-result/per-invocation compilation cleanup remained in that lane.
The last result remained owned through response writing; cached verification
compilation disposal followed the normal quit/finally path.

The 4.263-second batch occurred under tracing and is **not an unprofiled
benchmark**. No CPython timing or ratio was collected. Historical comparison
rows remain revision-specific in [BENCHMARKS.md](../../../BENCHMARKS.md).
The profile does not establish a speedup or attribute the earlier CPython gap.

Machine: Windows 10.0.26200 x64, .NET 10.0.13, 28 reported processors; default
worker GC/tiering and a shared unbounded allowance. The retained identity records
runtime overrides and loaded assembly hashes. Local Release binaries were built
before the runtime commit; their source equivalence to f0cd695f and exact loaded
core hash were checked. Startup/verification preceded profiler launch. Tool
10.0.745401 collected eight seconds of managed sampled thread time with a 64 MiB
trace buffer, including idle time after the batch. The batch started after a
fixed 0.5-second profiler startup delay; alignment is approximate. Normal
cancellation, stack admission, host mediation and ownership remained enabled.
No forced GC, protection change, full benchmark lane or new framework was used.

Preparation evidence is retained: the first supervisor expected the wrong
verification status and exited before tracing; the next waited for an interactive
message omitted by redirected dotnet-trace and captured idle time without a
batch. The corrected collection above is the sole workload profile. Every
started process and the reader ended; owned fixtures were removed in each preparation.
Two report commands also collided on the tool's derived ETLX cache; sequential
reports succeeded on the same immutable trace. No workload reran for analysis.

Evidence: [worker identity, responses and cleanup](profile-receipt.json),
[owned process receipt](process.json), [preparations](preparations.json),
[selected source/fixture/goldens](case.json), [interval analysis](call-profile.json),
[exclusive report](top-exclusive-sequential.txt),
[inclusive report](top-inclusive-sequential.txt), [manifest/hashes](manifest.json),
[artifact/golden verification](verification.json), [verifier](verify-evidence.py),
[raw trace](text.nettrace.gz), [Speedscope export](text.speedscope.json.gz) and
[exact catalog](catalog.json.gz). Compressed artifacts preserve original bytes.

The [one-off supervisor](profile.py) and [analysis](analyze.py) preserve the recipe.
For replay, copy them into an empty capture directory, extract the exact catalog
as catalog.json there, and run from the repository root against the original
immutable Release benchmark/core artifacts identified in the receipt. The script
checks that core hash, creates its collection subdirectory, performs verification
and the existing framed batch, then awaits every user before cleanup. It takes
explicit dotnet and dotnet-trace executable paths. The existing owned 28-second
work/1-second cleanup harness bounded each attempt. Analyze evented exports only
after collection; run dotnet-trace reports sequentially because they share a
derived cache. Rebuilding the same source can change informational metadata and
assembly hashes; no byte-identical fresh build is claimed.
