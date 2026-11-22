# B1378 JSON delivery against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3. The frozen Lython core is
`a3338b0a8`, the B1376 candidate. The measured encoder
and binding patches are present in production `e4df38816`,
which also includes the separately qualified regex correction.
The external engine is stock CPython 3.13.16. The two JSON Lines
exports read the same 3,072 records in 189,441 bytes and produce the
same output files. The configuration script reads one small file,
edits its fields and writes a verified replacement.

| Complete script | Phase | Lython | CPython | Lython / CPython | Lython / CPython worker spread |
| --- | --- | ---: | ---: | ---: | ---: |
| JSON Lines export with expanded options | Warm | 5.438–5.455 ms | 6.515–6.623 ms | 0.821–0.837× | 0.31 / 1.66% |
| JSON Lines export with explicit options | Warm | 5.432–5.470 ms | 6.340–6.367 ms | 0.853–0.863× | 0.71 / 0.42% |
| Configuration edit | Warm | 0.077 ms | 0.062 ms | 1.236–1.251× | 0.29 / 0.94% |
| JSON Lines export with expanded options | Compilation only | 0.02552–0.02588 ms | 0.04435–0.04447 ms | 0.574–0.584× | 1.43 / 0.27% |
| Configuration edit | Compilation only | 0.01946–0.01953 ms | 0.03399–0.03415 ms | 0.570–0.575× | 0.40 / 0.47% |
| JSON Lines export with expanded options | Compile-to-result | 5.533–5.548 ms | 6.665–6.669 ms | 0.830–0.832× | 0.27 / 0.06% |
| Configuration edit | Compile-to-result | 0.108 ms | 0.125–0.127 ms | 0.848–0.869× | 0.36 / 2.11% |

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

All 196 timed batches verified in
372 seconds. The complete Lython image and the
CPython executable matched their recorded hashes. Both quiet gates
passed, all workers joined and the persistent archive verified under
the lease. The source-pair positional regression remains open.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Verified inputs and procedures](procedures.tar.xz) ·
[JSON source pair](../2026-11-22-json-direct-mapping/README.md) ·
[Positional control](../2026-11-22-json-positional-control/README.md).
