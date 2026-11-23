# B1443 Retained compiler and runtime against CPython

Measured `30d956735` against stock CPython 3.13.16 on November 23,
2026 UTC. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3.
The retained core includes the compiler's shared branch snapshots,
nested bound arithmetic and borrowed CSV record construction. The
CPython executable, protocol and 31 auxiliary files match B1438.
This checkpoint adds the read-only branch workload and the native
typed ASCII CSV control.

| Workload | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Generated validator with read-only branches | Compile | 0.4882–0.4970 ms | 0.9270–0.9320 ms | 0.524–0.536× | 1.81 / 0.54% |
| Generated validator with read-only branches | Compile to result | 0.5013–0.5076 ms | 0.9446–0.9524 ms | 0.526–0.537× | 1.25 / 0.82% |
| Sparse generated validator | Compile to result | 1.566–1.570 ms | 1.963–1.972 ms | 0.794–0.800× | 0.27 / 0.45% |
| Postponed factories | Warm | 0.9604–0.9668 ms | 0.5399–0.5447 ms | 1.763–1.791× | 0.66 / 0.90% |
| Postponed factories | Compile to result | 1.050–1.076 ms | 0.5775–0.5831 ms | 1.800–1.864× | 2.56 / 0.98% |
| Quote-heavy CSV policy invoice | Warm | 6.814–6.958 ms | 6.341–6.470 ms | 1.053–1.097× | 2.11 / 2.03% |
| Native typed ASCII CSV meter | Warm | 3.049–3.113 ms | 2.478–2.499 ms | 1.220–1.257× | 2.11 / 0.84% |
| Nonlocal Unicode word count | Warm | 1.379–1.569 ms | 0.9478–0.9562 ms | 1.442–1.655× | 13.77 / 0.89% |
| Expanded JSON Lines export | Warm | 5.400–5.526 ms | 6.476–6.536 ms | 0.826–0.853× | 2.33 / 0.93% |
| Wide Unicode materialized split | Warm | 0.9942–0.9975 ms | 1.310–1.328 ms | 0.749–0.761× | 0.33 / 1.35% |

Two fresh workers per engine supply medians after eight seconds of
warmup and seven rotated batches. Ratios divide Lython elapsed by
CPython elapsed across all four pairings. Warm execution includes
mediated RAM-backed file access where applicable, output checks and
prior-result disposal. CPython uses native file APIs. Compilation
includes code-owner disposal. Compile to result adds execution and
applicable cleanup. Startup and final teardown are outside elapsed.
Wider CPU includes fixture resets and protocol.

The factory gap persists. Its first-result range is higher than B1438,
while the warm range is lower. The sparse validator's first-result time
is higher than B1438. The current workers agree, but the B1442 paired
comparison spans zero and has 5.94% reference-worker spread. Schedule
a matched sparse confirmation if it can distinguish a source effect
from worker variation. The nonlocal word count has 13.77% Lython spread.
The native ASCII meter remains a control for the CSV construction
change. These external ratios do not attribute individual changes.

Nine local cases match independent CPython goldens on the frozen
image. All ten quiet lanes and 280 timed batches verify. Workers join
and the archive verifies under the lease after guest fixture cleanup.
The integrated gate passed both Release targets, original-scale stress
and installed-package consumers on Windows and Ubuntu.

[Source and images](source.json) · [Timing and CPU](summary.json) ·
[Raw observations](observations.tgz) · [Procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json) · [B1438](../2026-11-23-integrated-cpython-checkpoint/README.md) ·
[B1442 source pair](../2026-11-23-abstract-state-snapshots/README.md) ·
[CI](https://github.com/Lokad/Lython/actions/runs/45111635398).
