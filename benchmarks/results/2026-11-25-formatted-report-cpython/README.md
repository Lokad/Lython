# B1727 Formatted report and generated definitions against CPython

Measured runtime source `857cc8001`, including the retained field scanner
and cancellable frontend regex encoding. A fresh nonincremental Release
core was built from the parent plus a source patch that matches the
committed runtime. All 32 frozen worker files and the stock CPython
3.13.16 executable match their recorded hashes. The Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.

| Workload | Phase | Lython | CPython | Elapsed Lython / CPython | Wider CPU Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Formatted JSON report, 454 B of source | Compilation only | 0.02121–0.02138 ms | 0.03710–0.03734 ms | 0.568–0.576× | 0.574–0.586× | Diagnostic. |
| Generated ordinary definitions, 96 built 16 times | Warm execution | 0.30543–0.30575 ms | 0.14830–0.15044 ms | 2.030–2.062× | 2.013–2.031× | Diagnostic. |
| Formatted JSON report, 1,024 records / 189,441 B | Warm execution | 2.66299–2.77436 ms | 2.58657–2.58703 ms | 1.029–1.073× | 1.069–1.116× | Diagnostic. Worker spread up to 4.18%. |
| File manifest control, 516 B of source | Compilation only | 0.02907–0.02938 ms | 0.04948–0.04955 ms | 0.587–0.594× | 0.580–0.597× | Diagnostic. |
| Formatted JSON report, 16 records / 2,961 B | Compile-to-result | 0.15713–0.15865 ms | 0.16563–0.17055 ms | 0.921–0.958× | 0.856–0.899× | Diagnostic. |
| Generated ordinary definitions | Compile-to-result | 0.87295–0.90248 ms | 1.13266–1.13426 ms | 0.770–0.797× | 0.784–0.817× | Diagnostic. Worker spread up to 3.38%. |

Two workers per engine warm for at least five seconds and contribute seven
rotated batches per phase. Ratios span all four pairings of worker medians.
Above 1× means Lython takes longer. The complete report loads a JSON array,
formats each record and writes a Markdown file. The read and write paths
use RAM-backed fixtures. Lython uses mediated I/O and an active shared
allowance. CPython uses its native file operations. Output and files are
verified in both workers.

Compilation includes compiled-owner disposal. Compile-to-result includes
compilation, one execution, verification and cleanup in a warm process.
Warm execution uses compiled code. Request framing and fixture reset sit
outside these elapsed boundaries and are included in wider process CPU.
Startup and final process teardown are outside the measurements.

Each of the four full CPython goldens verifies and all 168 timed batches
pass. Both quiet gates pass. All workers join, guest fixtures are removed
and the persistent archive verifies under the lease.

[Identity](identity.json) · [Timing](summary.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).
