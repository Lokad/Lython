# B1467 CPython checkpoint after capture and decimal conversion

The November 23, 2026 UTC checkpoint measures Lython `d969c134f`
against stock CPython 3.13.16. The Lython core was built at
`d3fd58f80` with identical production source. The complete 32-file
image and Python executable hashes verify on the quiet Azure F4as_v7
running Ubuntu 24.04.4 and .NET 11 RC3.

Ratios divide Lython elapsed by CPython elapsed. Ranges span pairings
of process medians from two workers per engine. Each worker warms
over full requests and supplies seven rotated batches. Warm elapsed
includes applicable mediated RAM-backed I/O, output and file
verification and prior-result disposal. Compilation includes disposal
of its code owner. Compile to result includes both phases in a running
process. Reset and request transport sit outside elapsed, and the
separate process CPU covers that wider boundary. Startup stays in its
earlier checkpoint.

| Workload | Boundary | Lython | CPython | Lython / CPython | Worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Native typed ASCII CSV meter | Warm | 2.719–2.735 ms | 2.506–2.543 ms | 1.069–1.091× | 0.58 / 1.45% |
| Configuration edit | Compile | 0.01907–0.01917 ms | 0.03404–0.03428 ms | 0.556–0.563× | 0.56 / 0.69% |
| Postponed nested factories | Compile to result | 0.9618–0.9923 ms | 0.5842–0.5857 ms | 1.642–1.698× | 3.17 / 0.26% |
| CSV meter with explicit float | Warm | 3.876–3.891 ms | 2.855–2.863 ms | 1.354–1.363× | 0.39 / 0.30% |
| Short nonlocal word report | Warm | 1.372–1.434 ms | 0.9695–0.9708 ms | 1.414–1.480× | 4.53 / 0.14% |
| Configuration edit | Compile to result | 0.1068–0.1075 ms | 0.1255–0.1257 ms | 0.850–0.857× | 0.68 / 0.12% |
| Quote-heavy CSV invoice | Warm | 6.884–6.963 ms | 6.315–6.332 ms | 1.087–1.103× | 1.15 / 0.26% |

The typed ASCII meter is 1.069–1.091× CPython, compared with
1.208–1.236× at the earlier external checkpoint. The controlled
source pair measures the specific decimal change. Eager factory and
fully supplied call regressions belong to the separate capture trial.
Current postponed factory time to result remains 1.642–1.698× CPython
with lower worker spread than the previous checkpoint. The short report
still takes about 1.41–1.48× CPython elapsed.

Six independent stdout/file goldens verify on the frozen image. All
196 timed batches verify in 317 seconds. Both quiet gates pass, workers
join, guest fixtures are removed and the raw archive verifies under the
lease. The first staging attempt failed on Python indentation. The
first launched comparison stopped before timing because the manifest
omitted auxiliary hashes. The archived retry restores those hashes
with the same catalog, images and timing boundaries.

[Timings and wider CPU](summary.json) · [Identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and initial failed attempt](procedures.tar.xz) ·
[Measured core](../2026-11-23-exact-short-decimal/candidate-core.dll.gz) ·
[Earlier CPython checkpoint](../2026-11-23-integrated-text-cpython/README.md) ·
[Capture trial](../2026-11-23-direct-owned-capture/README.md).
