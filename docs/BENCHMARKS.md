# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helper | 4,096 records / 57,361 bytes | 8.083 ms | 3.707 ms | 2.18× | Diagnostic; 4.0% Lython IQR |
| Text summary/helper | 3,072 lines / 35,840 bytes | 6.201 ms | 1.466 ms | — | Inconclusive; 11.7% Lython IQR |
| JSON-lines helper | 4,096 records / 110,592 bytes | 8.413 ms | 4.451 ms | 1.89× | Diagnostic; 10.0% Lython IQR, borderline |

Measured **2026-11-01**, development revision **`830c6251`**, .NET 11 RC3;
[complete observations and exact identities](../benchmarks/results/2026-11-01-representative-checkpoint/README.md).
These rows measure **fresh compilation plus one complete synchronous execution in
an already-running host**. Both engines run identical source and check complete
stdout and file contents. Startup and transport are excluded. Ratio divides
Lython elapsed time by CPython elapsed time; above one means slower. IQR means
interquartile range divided by median. A dash means inconclusive, not zero.
Short observations are diagnostic, not qualified release multipliers.

One fixed seven-job collection took **58.22 seconds**. All jobs and before/after
quiet-machine checks pass. Text's fresh ratio is withheld at the predeclared
10% IQR threshold; JSON is just below it before rounding. No retiming or favorable
sample selection. These measurements describe the named revision, not later HEAD.

## Warm execution, compilation and startup

The same collection separately measures reused compiled code, including public
invocation, file work, complete checks and declared cleanup:

| Warm workload | Lython | CPython | Lython / CPython | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers | 7.320 ms | 3.511 ms | 2.08× | 6.2% / 0.8% |
| Text helpers | 6.539 ms | 1.327 ms | 4.93× | 2.6% / 1.1% |
| JSON-lines helpers | 7.918 ms | 4.307 ms | 1.84× | 6.3% / 0.6% |

CSV **compilation only**: Lython **94.64 µs**, CPython **54.71 µs**, diagnostic
ratio **1.73×**, IQR 3.6% / 0.3%. This directly measured phase includes no guest
execution. No new process-startup measurement was collected.

Independent phase jobs are not additive. Warm text's point median exceeds its
noisy fresh median; do not subtract these medians to infer compilation overhead.
The [historical foundation](../benchmarks/results/2026-10-29-foundation/README.md)
records earlier compilation and startup boundaries; it does not describe this
revision's startup or supply a current startup ratio.

## Machine and execution boundary

Dedicated Azure **Standard_F4as_v7**, Ubuntu 24.04.4 x86-64, four AMD EPYC 9V45
vCPUs, one thread/core, 16 GiB RAM, no swap. Reference: **CPython 3.13.16**,
GCC 13.3, PGO/LTO, ordinary GIL/GC, no JIT/debug mode, launched `-I -S` with UTF-8
file mode. SDK/runtime: **11.0.100-rc.3.26522.148 / 11.0.0-rc.3.26522.148**.
Engines run sequentially with ordinary GC/tiering. One second warmup per engine,
seven rotating-order batches targeting 25 ms each; no statistical confidence
interval or repeatability claim. Quiet gates do not eliminate JIT/GC variance.

Execution jobs include fixture reset/staging, efficient host-mediated file I/O,
complete output checks and prior-result cleanup. Fresh Lython jobs also dispose their
per-invocation compilation inside the timer. The final result stays retained
through response; worker exit disposes remaining results and cached compilations.
Transport and response hashing are outside the worker timer. The host supplies
efficient ranged reads/appends, so whole-file compatibility defaults do not
explain these gaps. Actual suspended-I/O correctness is tested separately; these
timings use the synchronous public API.

## Other workloads and remaining uncertainty

These cases were last measured on **2026-10-30 at `4433986e`**, with the same
fresh compile/run boundary. They remain historical, not measurements of 830c6251:

| Historical workload | Input | Lython | CPython | Ratio | Status |
| --- | --- | ---: | ---: | ---: | --- |
| Config read/edit/write | 16 paths / 181 bytes | 3.057 ms | 1.568 ms | 1.95× | Diagnostic |
| Copy/checksum/manifest | Two files / 4,107 bytes | 2.337 ms | 0.598 ms | 3.91× | Diagnostic |
| Search with early exit | 42,010 bytes | 2.307 ms | 0.394 ms | — | Inconclusive; 147% Lython IQR |

[Historical raw evidence](../benchmarks/results/2026-10-30-native-json-writer/README.md)
preserves all rows, including the inconclusive warm configuration case. Do not
compare independently collected medians as an old/new speedup.

Text warm execution retains the largest observed gap among the current cases.
Report rendering now has an **inconclusive warm observation**: 3,072 JSON records
to Markdown, Lython **10.485 ms** / CPython **2.709 ms**, measured 2026-11-02 on
runtime source `4cae38f4` with workload `c3a0eb1a`. Lython IQR is 28.75%, so no
ratio is reported. This includes complete file work/checks and declared cleanup,
excluding compilation/startup; the [exact diagnostic evidence](../benchmarks/results/2026-11-02-string-format-unqualified/README.md)
records its boundary and uncertainty.

CSV/JSON remain slower too. Compilation has its own measured gap; startup,
suspended I/O and general host latency remain unqualified. No single mechanism
has been shown to explain the whole difference.

Use the [existing comparison tools](../benchmarks/COMPARISON.md) and complete
workload goldens. Development comparisons take seconds; ten-minute lanes belong
at infrequent milestones. [Earlier-Lython comparisons](../benchmarks/HISTORY.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) retain old/new controls,
profiles, rejected changes and qualification procedures separately. CPython is
the primary external baseline.
