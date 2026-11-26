# B1774c Callable checkpoint against CPython

Lython `088cf9d37` and stock CPython 3.13.16 ran on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 26, 2026 UTC. Each phase
uses two processes per engine. Five seconds of warmup per process precede
seven rotated batches targeting 460 ms per request. Ratios divide Lython
elapsed by CPython elapsed and span the four pairings of process medians.
Above 1× means Lython took longer. [Medians, spreads and CPU](summary.json)

| Workload | Input or source | Phase | Lython | CPython | Elapsed ratio | Status |
| --- | --- | --- | ---: | ---: | ---: | --- |
| Shipment callback report | 2,048 records / 27,350 B | Warm sync | 1.84351–1.85090 ms | 1.63098–1.63596 ms | 1.127–1.135× | Diagnostic. |
| Shipment callback report | 2,048 records / 27,350 B | Warm `RunAsync` | 1.85090–1.92251 ms | 1.61432–1.62772 ms | 1.137–1.191× | Diagnostic. |
| Short Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | Warm sync | 0.83647–0.88107 ms | 0.94564–0.98439 ms | 0.850–0.932× | Diagnostic. Lython worker spread is 5.33%. |
| Generated ordinary definitions | 96 definitions, built 16 times | Warm sync | 0.20024–0.20489 ms | 0.15131–0.15228 ms | 1.315–1.354× | Diagnostic. |
| Generated ordinary definitions | 96 definitions, built 16 times | Warm `RunAsync` | 0.20035–0.20296 ms | 0.14669–0.14685 ms | 1.364–1.384× | Diagnostic. |
| Generated rules with defaults | 2,048 rule groups | Warm sync | 0.86437–0.97649 ms | 0.68070–0.69328 ms | 1.247–1.435× | Diagnostic. Lython worker spread is 12.97%. |
| JSON service quantity totals | 2,048 records / 78,755 B | Warm sync | 0.66179–0.67585 ms | 0.48294–0.49084 ms | 1.348–1.399× | Diagnostic. |
| Generated ordinary definitions | 8,641 B of source | Compilation only | 0.57472–0.57540 ms | 1.10374–1.12026 ms | 0.513–0.521× | Diagnostic. |
| Generated ordinary definitions | 96 definitions, built 16 times | Compile-to-result | 0.77007–0.77256 ms | 1.12773–1.12965 ms | 0.682–0.685× | Diagnostic. |
| Shipment callback report | 2,048 records / 27,350 B | Compile-to-result | 1.99749–2.01314 ms | 1.89114–1.90265 ms | 1.050–1.065× | Diagnostic. |

Warm execution reuses compiled code in a running process. The elapsed
clock covers RAM-backed guest I/O where applicable, verified output and
files, and result disposal. Lython mediates I/O through the comparison
host and uses shared admission. CPython uses native file I/O and ordinary
cyclic GC. The async lane uses Lython `RunAsync` and the same synchronous
CPython guest script. Compile-only and compile-to-result include disposal
of the compiled owner. The combined lane uses synchronous execution.
Startup, fixture reset and request framing sit outside elapsed. Process
CPU covers the wider request.

The original campaign hit its inherited guard after eight lanes. Its
final quiet gate did not run. The corrected campaign keeps the same
binaries, inputs and per-lane settings. All five independent CPython
goldens and all 32 image files verify. Both quiet gates pass, 280 batches
complete, all workers join and the persistent archive verifies after
fixture cleanup. [First attempt](first-attempt/terminal-status.json) ·
[First observations](first-attempt/observations.tar.xz) · [Frozen identity](identity.json) ·
[Complete observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz).
