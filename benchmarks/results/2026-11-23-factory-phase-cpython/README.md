# B1480 Separate factory compilation and execution

The November 23, 2026 UTC phase comparison uses Lython `a94f78fc7`
and stock CPython 3.13.16. The frozen `f5852aa99` worker has the same
production and test sources. The complete 32-file image and Python
executable verify on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.

Ratios divide Lython elapsed by CPython elapsed and span the four
pairings of independent process medians. Each process completes seven
rotated batches after eight seconds of warmup. Compilation includes
compiled-owner disposal. Warm execution includes applicable mediated
RAM-backed I/O, stdout and file verification, and prior-result
disposal. Compile to result includes both phases in a running process.
Resets and protocol are outside elapsed and inside wider CPU.
Each lane uses its own workers.

| Workload | Phase | Lython | CPython | Lython / CPython | Worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Configuration edit | Compilation | 0.01892–0.01905 ms | 0.03379–0.03403 ms | 0.556–0.564× | 0.64 / 0.71% |
| Postponed nested factories | Compilation | 0.02116–0.02135 ms | 0.03961–0.03962 ms | 0.534–0.539× | 0.92 / 0.04% |
| Postponed nested factories | Warm execution | 0.97958–0.98973 ms | 0.54638–0.56598 ms | 1.731–1.811× | 1.04 / 3.59% |
| Configuration edit | Compile to result | 0.10770–0.10849 ms | 0.12486–0.12511 ms | 0.861–0.869× | 0.73 / 0.20% |
| Postponed nested factories | Compile to result | 0.96707–0.97728 ms | 0.58190–0.58664 ms | 1.648–1.679× | 1.06 / 0.81% |

The factory compiles in about 21 µs and takes about 980–990 µs warm.
Its warm ratio is 1.731–1.811×. The execution work dominates this
script's measured gap. Configuration compile and time-to-result
controls remain close to B1478. The factory's compile-to-result
ratio varies across quiet runs, at 1.649–1.680× here and 1.718–1.742×
in B1478. Independently warmed lanes can tier differently, so
subtracting their medians cannot isolate exact per-phase cost.

Both CPython goldens and all 140 timed batches verify in 227 seconds.
Both quiet gates pass, workers join, guest fixtures are removed and
the raw archive verifies under the lease.

[Summary](summary.json) · [Identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz) ·
[Integrated B1478](../2026-11-23-tuple-admission-cpython/README.md) ·
[Retained CPU profile](../2026-11-23-retained-factory-cpu/README.md).
