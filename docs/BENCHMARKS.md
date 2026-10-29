# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helper | 4,096 records / 57,361 bytes | 29.402 ms | 3.701 ms | — | Inconclusive; 562% Lython IQR |
| Text summary/helper | 3,072 lines / 35,840 bytes | 28.366 ms | 1.520 ms | 18.66× | Diagnostic; 25% Lython IQR |
| JSON config read/edit/write | 16 paths / 181 bytes | 2.155 ms | 1.551 ms | 1.39× | Diagnostic; 14% Lython IQR |
| JSON-lines helper | 4,096 records / 110,592 bytes | 28.744 ms | 4.608 ms | — | Inconclusive; 79% Lython IQR |
| Copy/checksum/manifest | Two files / 4,107 bytes | 1.812 ms | 0.597 ms | — | Inconclusive; 30% Lython IQR |
| Search with early exit | 42,010 bytes | 2.959 ms | 0.405 ms | — | Inconclusive; 115% Lython IQR |

Measured **2026-10-29**, 0.11.0 development revision **`da0ff811`**, .NET 11 RC3;
[complete results and exact identities](../benchmarks/results/2026-10-29-delivery011/README.md).
The fixed six-job collection takes 49.79 seconds and passes complete output/file
checks and both quiet machine gates. The observations still show substantial
gaps against CPython; several short rows vary severely. Higher development
medians remain visible in the report. No multiplier above is a certified
current-release result. A ratio
divides Lython elapsed time by CPython elapsed time; above one means slower.
IQR is interquartile range divided by median, not a confidence interval.
A dash denotes an inconclusive ratio, not zero. The
[foundation matrix](../benchmarks/results/2026-10-29-foundation/README.md) and
[older diagnostic table](../benchmarks/results/2026-10-18-text-milestone/README.md)
describe earlier revisions and distinct observations; they cannot be substituted
for this source's performance.

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

The separate foundation report separates historical warm and warm-async rows from fresh
compile/run. Its async host operations completed synchronously; it does not
measure suspended I/O. The same report records separate compilation-only rows
(development 146–330 microseconds, CPython 31–57 microseconds), a single
ordinary-tiering empty-worker startup smoke (158/29.5 ms), and frontend allocation
diagnostics. Startup includes loading, empty compilation/execution and process
exit; it is not a generic host estimate. A separate
[runtime-phase and delayed-I/O diagnostic](../benchmarks/results/2026-10-29-runtime-boundaries/README.md)
at revision 1c5cff2a separates setup, guest/host work, projection and cleanup.
Large aggregation cost remains mostly inside execution; setup/projection are
small in that experiment. All owners return to baseline. It uses instrumented
clocks and one injected host delay, supplies no CPython ratio or qualified
speedup, and does not update the primary table.
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

The [prepared-scope consolidation](../benchmarks/results/2026-10-29-prepared-scope-facts/README.md)
at cc988b9d removes repeated lexical analysis. Five representative sources allocate
1.7–3.7% less during compilation; the fixed timing comparison is mixed and noisy.
It establishes no overall elapsed speedup and does not update the primary table.

The [optional-adapter refresh diagnostic](../benchmarks/results/2026-10-29-regex-adapter04/README.md)
compares PythonRe 0.2/0.4 on identical core runtime sources. Finditer improves in
one short observation; pattern reuse remains inconclusive. Both rows retain
their noise and CPython context, with no qualified overall multiplier.
