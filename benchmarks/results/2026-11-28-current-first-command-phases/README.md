# B1987 Refresh first-command startup attribution

November 28, 2026 UTC. The local Ubuntu WSL diagnostic pins .NET 11 RC3
and stock CPython 3.13.16 to one logical CPU. It rotates four workers for
twelve rounds on the 181 B configuration edit. The current core is
`5d1cbb1f3`, and the historical core is `486b6bb60`. Both instrumented
images use the same worker DLL. The current frozen image controls for
instrumentation. All 48 timed output and file goldens verify.

| Fresh process | Median elapsed | Median child CPU |
| --- | ---: | ---: |
| Current frozen Lython | 411.88 ms | 229.91 ms |
| Current instrumented Lython | 404.63 ms | 233.50 ms |
| Historical instrumented Lython | 420.68 ms | 236.23 ms |
| CPython | 234.16 ms | 31.40 ms |

Elapsed spans launch, initialization, compilation, the public run, I/O,
reply, cleanup, exit and pipe drain. Worker images reside on the Windows
mount and fixtures reside in RAM. Parent reply validation follows the
timer. Historical Lython and CPython have observations above one second.
The quiet Azure launch comparison retains its original revision. [B1970](../2026-11-28-fresh-command-cpython/README.md).

| Instrumented phase | Current median | Historical median |
| --- | ---: | ---: |
| Catalog load | 13.09 ms | 12.44 ms |
| Enter once worker | 35.82 ms | 37.99 ms |
| Engine creation and first compilation | 139.70 ms | 141.98 ms |
| First public run | 82.13 ms | 79.14 ms |
| Reply identity, serialization and write | 78.87 ms | 78.60 ms |
| In-worker disposal and return | 0.20 ms | 0.19 ms |
| Launch and exit residual | 45.74 ms | 48.11 ms |

Phase medians do not add to the outer elapsed median. The current core
changes first-compile elapsed by a median −7.63 ms across matched rounds,
and first-run elapsed by +3.81 ms. Both ranges span zero, so the local
result leaves the change in first-command latency unresolved.

Two separate JIT captures verify the same golden on the current frozen
image. Each matches all 2,665 start and load events. The frontend module
catalog initializer has 8,882 IL bytes and records intervals of 7.54 ms
and 3.39 ms. Callable data contract construction has 11,125 IL bytes and
records 2.97 ms and 2.00 ms. Runtime initialization also remains visible.
Intervals record JIT wall time and can overlap or include stalls. They
do not apportion exclusive CPU time. The module catalog still eagerly
builds unrelated member inventories, a candidate for a scoped follow-up.

[Phase summary](summary.json) · [JIT summary](jit-summary.json) ·
[Raw timings, traces and procedures](observations.tar.xz) ·
[Previous phase diagnostic](../2026-11-28-first-command-phases/README.md).
