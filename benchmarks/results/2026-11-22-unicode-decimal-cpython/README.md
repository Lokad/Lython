# B1388 Unicode CSV and text against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3. The frozen Lython core is
`d45f2ed21`, the B1383 candidate with the retained
Unicode decimal change from `f17ba1c9f`. The fully
qualified `4eb84cf46` additionally changes regex
objects and tests. These workloads use no regex. The external engine is
stock CPython 3.13.16. Each CSV input contains 4,096 records.
The text input contains 3,072 lines. This text script calls a helper
that updates a nonlocal width before returning words. It shares input
and expected report bytes with B1274 but uses a different script.
Each engine's output and files verify against the same catalog.
[Earlier map-based script](../2026-11-21-cpython-casing-checkpoint/README.md).

| Complete script | Phase | Lython | CPython | Lython / CPython | Lython / CPython worker spread |
| --- | --- | ---: | ---: | ---: | ---: |
| Typed Unicode CSV meter report | Warm | 3.712–3.713 ms | 3.139–3.152 ms | 1.178–1.183× | 0.03 / 0.40% |
| Quote-heavy CSV policy invoice | Warm | 7.155–7.287 ms | 6.268–6.277 ms | 1.140–1.162× | 1.84 / 0.14% |
| Unicode word-count report with a nonlocal helper | Warm | 1.573–1.585 ms | 0.931–0.953 ms | 1.652–1.702× | 0.77 / 2.29% |
| Typed Unicode CSV meter report | Compilation only | 0.04218–0.04232 ms | 0.06772–0.06784 ms | 0.622–0.625× | 0.34 / 0.18% |
| Typed Unicode CSV meter report | Compile-to-result | 3.799–3.876 ms | 3.298–3.439 ms | 1.104–1.175× | 2.03 / 4.29% |

Ratios divide Lython elapsed by CPython elapsed. Above 1× means
Lython takes longer. Each range spans all pairings of two process
medians per engine. Each phase uses independent processes, normal
GC and tiering, eight seconds of warmup per worker and seven timed
batches per worker.

Warm execution includes mediated RAM-backed I/O for Lython, native
file I/O for CPython, output checks and prior-result disposal.
Compilation-only includes compiled-owner disposal. Time to result
includes compilation, one execution and their cleanup. Startup and
final teardown use separate boundaries. Process CPU in the raw
results includes worker reset and request handling.

All 140 timed batches verified in
266 seconds. The complete Lython image and the
CPython executable matched their recorded hashes. Both quiet gates
passed, all workers joined and the persistent archive verified under
the lease. These are diagnostics for the measured revision and machine.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Verified inputs and procedures](procedures.tar.xz) ·
[Unicode decimal source pair](../2026-11-22-unicode-decimal-normalization/README.md).
