# B1438 Retained runtime against CPython

Measured `9fb7fdcfb` against stock CPython 3.13.16 on November 23,
2026 UTC on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
The current core includes the direct prepared argument path and bounded
materialized casing. The CPython executable, protocol, 31 auxiliary
files and six original scripts match B1429. The wide materialized
report adds one warm lane. Worker order is reversed.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Sparse generated validator, 96 guards | Compile | 1.459–1.462 ms | 1.814–1.817 ms | 0.803–0.806× | 0.18 / 0.14% |
| Sparse generated validator, 96 guards | Compile to result | 1.488–1.521 ms | 1.968–1.972 ms | 0.755–0.773× | 2.17 / 0.20% |
| Postponed factories, 4,096 definitions | Warm | 0.975–0.988 ms | 0.5404–0.5405 ms | 1.803–1.829× | 1.41 / 0.04% |
| Postponed factories, 4,096 definitions | Compile to result | 1.023–1.043 ms | 0.580–0.587 ms | 1.744–1.800× | 1.91 / 1.27% |
| Quote-heavy CSV invoice, 4,096 records | Warm | 7.090–7.256 ms | 6.299–6.420 ms | 1.104–1.152× | 2.35 / 1.92% |
| Unicode word count with a nonlocal helper, 3,072 lines | Warm | 1.381–1.418 ms | 0.945–0.953 ms | 1.449–1.500× | 2.69 / 0.80% |
| Expanded JSON Lines export, 3,072 records | Warm | 5.402–5.483 ms | 6.474–6.512 ms | 0.830–0.847× | 1.50 / 0.59% |
| Small JSON configuration edit | Compile to result | 0.1077–0.1090 ms | 0.1236–0.1253 ms | 0.859–0.882× | 1.26 / 1.34% |
| Wide Unicode materialized word report, 195,585 bytes | Warm | 0.994–0.998 ms | 1.326–1.329 ms | 0.748–0.752× | 0.44 / 0.19% |

Ratios divide Lython elapsed by CPython elapsed and span pairings
of medians from two independent workers per engine. Workers warm
for eight seconds and supply seven rotated batches. Warm execution
includes applicable RAM-backed file I/O, output and file checks and
prior-result disposal. Lython mediates I/O and accounts for a shared
allowance. CPython uses native file APIs. Compilation includes code
owner disposal. Time to result adds execution and its cleanup.
Fixture resets and protocol contribute to wider CPU. Startup and final
teardown are outside these clocks.

The two Unicode reports exercise different scripts and shapes. The new
wide report materializes a list of words before iteration. The nonlocal
report keeps the helper and immediate iteration. The factory gap
persists both warm and from compilation to result, with lower worker
spread in this collection. The 18.08% Lython warm spread in B1429
remains part of the historical evidence. Paired Lython runs establish
changes between individual revisions.

All 252 timed batches verify within 475 seconds. Both quiet gates pass,
all workers join and the persistent archive verifies after the guest
fixtures are removed. Seven local CPython goldens pass with the same
frozen Lython image. The full delivery gate passes both Release targets,
original-scale stress and installed-package consumers on Windows and
Ubuntu. [Delivery gate](https://github.com/Lokad/Lython/actions/runs/45102142966).

[Source and images](source.json) · [Timings and wider CPU](summary.json) ·
[Observations](observations.tgz) · [Checks and procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json) ·
[B1429](../2026-11-23-retained-cpython-checkpoint/README.md) ·
[Retained core and Lython source pair](../2026-11-23-materialized-cased-split-bounded/README.md).
