# B1784 Representative checkpoint against CPython

Lython `0172ed7cf` and stock CPython 3.13.16 ran on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 26, 2026 UTC. Each phase
uses two processes per engine, five seconds of warmup per process and
seven rotated batches targeting 460 ms per request. The elapsed ratio
divides Lython by CPython and spans the four pairs of process medians.
Above 1× means Lython took longer. [Per-worker spreads and CPU](summary.json)

| Workload | Input or source | Phase | Lython | CPython | Elapsed ratio | Status |
| --- | --- | --- | ---: | ---: | ---: | --- |
| Shipment callback report | 2,048 records / 27,350 B | Warm sync | 1.69475–1.84989 ms | 1.61953–1.63074 ms | 1.039–1.142× | Diagnostic. Lython worker spread is 9.15%. |
| Shipment callback report | 2,048 records / 27,350 B | Warm `RunAsync` | 1.80470–1.82692 ms | 1.62393–1.64475 ms | 1.097–1.125× | Diagnostic. |
| Generated ordinary definitions | 96 definitions, built 16 times | Warm sync | 0.18978–0.19175 ms | 0.14740–0.14775 ms | 1.284–1.301× | Diagnostic. |
| Generated ordinary definitions | 96 definitions, built 16 times | Warm `RunAsync` | 0.19016–0.19559 ms | 0.14794–0.14895 ms | 1.277–1.322× | Diagnostic. |
| JSON service quantity totals | 2,048 records / 78,755 B | Warm sync | 0.65557–0.66258 ms | 0.49285–0.49719 ms | 1.319–1.344× | Diagnostic. |
| Short Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | Warm sync | 0.86614–0.92058 ms | 0.94688–0.95055 ms | 0.911–0.972× | Diagnostic. Lython worker spread is 6.29%. |
| Generated rules with defaults | 2,048 rule groups | Warm sync | 0.84421–0.92076 ms | 0.68387–0.68985 ms | 1.224–1.346× | Diagnostic. Lython worker spread is 9.07%. |
| Generated ordinary definitions | 8,641 B of source | Compilation only | 0.56535–0.57051 ms | 1.09292–1.10590 ms | 0.511–0.522× | Diagnostic. |
| Shipment callback report | 2,048 records / 27,350 B | Compile-to-result | 1.88219–1.90175 ms | 1.90655–1.92215 ms | 0.979–0.997× | Diagnostic, near parity. |
| Generated ordinary definitions | 96 definitions, built 16 times | Compile-to-result | 0.76083–0.76632 ms | 1.12205–1.12544 ms | 0.676–0.683× | Diagnostic. |

Warm execution reuses compiled code in a running process. Its clock includes
applicable RAM-backed guest I/O, output and file verification, and disposal
of the previous result. Lython uses the mediated comparison host with shared
memory admission. CPython uses native file I/O and ordinary cyclic GC. Async
rows compare Lython `RunAsync` with the same synchronous CPython script.
Compile-only includes disposal of the compiled owner. Compile-to-result includes
compilation, synchronous execution, and both owner cleanups. Fixture reset,
request framing and startup sit outside elapsed. Process CPU covers the
wider request. Fresh-process timings remain a separate historical measurement.

This checkpoint reuses the exact source, fixture and lane catalog from
[B1774c](../2026-11-26-callable-checkpoint-cpython/README.md). The two checkpoints
ran separately. The [paired Lython-only trial](../2026-11-26-fixed-row-local-bindings/README.md)
isolates the fixed-row change. All five independent CPython goldens and the
full 32-file Lython image verify. All 280 batches complete. Both quiet gates
pass, workers join, and the persistent archive verifies after fixture cleanup.

[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz)
