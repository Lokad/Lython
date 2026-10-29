# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helper | 4,096 records / 57,361 bytes | 16.859 ms | 3.751 ms | 4.49× | Diagnostic; runtime difference unstable |
| Text summary/helper | 3,072 lines / 35,840 bytes | 13.359 ms | 1.470 ms | 9.09× | Diagnostic; 19% Lython IQR |
| JSON config read/edit/write | 16 paths / 181 bytes | 2.129 ms | 1.526 ms | 1.40× | Diagnostic; 20% Lython IQR |
| JSON-lines helper | 4,096 records / 110,592 bytes | 24.471 ms | 4.427 ms | — | Inconclusive; 170% Lython IQR |
| Copy/checksum/manifest | Two files / 4,107 bytes | 1.978 ms | 0.614 ms | — | Inconclusive; 25% Lython IQR |
| Search with early exit | 42,010 bytes | 1.838 ms | 0.393 ms | — | Inconclusive; 184% Lython IQR |

Measured **2026-10-29**, development revision **`bed371f6`**, .NET 11 RC3;
[complete results and exact identities](../benchmarks/results/2026-10-29-foundation/README.md).
All 27 comparison jobs passed complete output checks, but several short rows are
unstable despite quiet machine checks. Separate profiled CSV batches give similar
times on .NET 10/11, unlike the short timing rows. No full comparison qualifies
today's HEAD, and no multiplier above is a certified current-release result. A ratio
divides Lython elapsed time by CPython elapsed time; above one means slower.
IQR is interquartile range divided by median, not a confidence interval.
A dash denotes an inconclusive ratio, not zero. The
[older diagnostic table](../benchmarks/results/2026-10-18-text-milestone/README.md)
describes earlier revisions; it cannot be substituted for current performance.

## Measurement boundary

The table measures **fresh compilation plus one synchronous execution in an
already-running host**. Each job includes invocation setup, complete file work,
reset/output checks and relevant cleanup. Startup/transport are excluded. Both
engines execute identical Python source and validate complete stdout/file results.
The host supplies efficient ranged reads/appends; whole-file compatibility
fallbacks are not an explanation of these gaps.

Machine: dedicated Azure **Standard_F4as_v7**, Ubuntu 24.04.4 x86-64, four AMD
EPYC 9V45 vCPUs, one thread/core, 16 GiB RAM, no swap. Reference: **CPython
3.13.16**, GCC 13.3, PGO/LTO, ordinary GIL/GC, no JIT/debug mode, launched `-I -S`.
Candidate SDK/runtime: 11.0.100-rc.3.26522.148 / 11.0.0-rc.3.26522.148.
The separate published 0.10.0 comparator uses its actual NuGet core on .NET 10.0.12;
producer and harness SDK identities are recorded independently. Engines ran
sequentially with ordinary GC/tiering.

Keep distinct measurements for:

| Boundary | Includes |
| --- | --- |
| Warm execution | Reused compiled code plus public invocation/setup, workload and declared cleanup; excludes compilation/startup |
| Fresh compile/run | Fresh frontend work plus one complete execution in a warm host |
| Compilation | Frontend/code preparation only; not inferred by subtracting unrelated medians |
| Startup | A new process and its explicitly declared first-result boundary |

The linked foundation report separates warm and warm-async rows from fresh
compile/run. Its async host operations completed synchronously; it does not
measure suspended I/O. The same report records separate compilation-only rows
(development 146–330 microseconds, CPython 31–57 microseconds), a single
ordinary-tiering empty-worker startup smoke (158/29.5 ms), and frontend allocation
diagnostics. Startup includes loading, empty compilation/execution and process
exit; it is not a generic host estimate. Runtime-phase attribution remains pending.
Do not infer these boundaries by subtracting unrelated medians.

## Evidence and reproduction

Use the [existing comparison tools](../benchmarks/COMPARISON.md) and complete
workload goldens. Development uses short focused diagnostics; ten-minute lanes
are infrequent milestones. Qualification requires stable controls and declared
machine/execution boundaries, not a favorable point sample.

[Earlier-Lython improvements and measurement history](../benchmarks/HISTORY.md)
are separate from the CPython baseline. [Experiments](../benchmarks/EXPERIMENTS.md)
retain profiles, rejected changes and inconclusive results. They are historical
evidence, not current architecture or supported runtime contracts.

The [explicit-call activation diagnostic](../benchmarks/results/2026-10-29-explicit-call-frames/README.md)
compares development revisions c0846e69 and 4b408097 on the same net11 runtime.
It confirms bounded CLR stack growth for ordinary calls, with unchanged CSV
compile/run, a roughly 3.6% async-CSV increase and inconclusive noisy rows.
It establishes no qualified speedup and does not update the table's revision.
