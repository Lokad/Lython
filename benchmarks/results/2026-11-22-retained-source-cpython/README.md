# B1397 Retained 0.11.0 checkpoint against CPython

Measured production `32c50290d` on November 22, 2026 UTC on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The comparison uses
stock CPython 3.13.16. Both engines verify the same six scripts and
fixtures. The 32-file Lython image is recorded by hash.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Sparse generated validator, 96 guards | Compile | 1.431–1.468 ms | 1.818–1.819 ms | 0.787–0.807× | 2.62 / 0.01% |
| Sparse generated validator, 96 guards | Compile to result | 1.553–1.554 ms | 1.964–1.967 ms | 0.789–0.791× | 0.08 / 0.15% |
| Postponed factories, 4,096 definitions | Warm | 1.044–1.082 ms | 0.551–0.557 ms | 1.873–1.963× | 3.72 / 1.06% |
| Postponed factories, 4,096 definitions | Compile to result | 1.058–1.084 ms | 0.580–0.594 ms | 1.780–1.870× | 2.46 / 2.54% |
| Quote-heavy CSV invoice, 4,096 records | Warm | 7.256–7.279 ms | 6.282–6.314 ms | 1.149–1.159× | 0.32 / 0.52% |
| Unicode word count with a nonlocal helper, 3,072 lines | Warm | 1.578–1.584 ms | 0.950–0.951 ms | 1.660–1.667× | 0.37 / 0.09% |
| Expanded JSON Lines export, 3,072 records | Warm | 5.345–5.470 ms | 6.495–6.526 ms | 0.819–0.842× | 2.34 / 0.48% |
| Small JSON configuration edit | Compile to result | 0.109 ms | 0.125 ms | 0.867–0.875× | 0.44 / 0.45% |

Ratios divide Lython elapsed by CPython elapsed. Above 1× means Lython
takes longer. Ranges span pairings of two independent process medians
per engine. Each worker warms for at least eight seconds and supplies
seven rotated batches. Output verification and prior-result disposal
are included in the warm clock. Lython uses host-mediated RAM-backed
I/O with the shared allowance active. CPython uses native file APIs on
the same RAM backing. Compilation includes compiled-owner disposal.
Time to result includes one compilation and one execution with their
cleanup. Startup, request framing, fixture reset and final teardown
are outside these elapsed clocks. Wider CPU covers the worker request.

All 224 timed batches and both quiet gates passed. The persistent
archive was verified under the lease with all workers joined and guest
fixtures removed. The run took 423 seconds. The comparison measures
external gaps at this source checkpoint. It does not identify the cause
of earlier Lython-to-Lython regression signals.

[Timing and CPU ratios](summary.json) · [Source and image hashes](source.json) ·
[Raw observations](observations.tgz) · [Verification and procedures](procedures.tar.xz).
