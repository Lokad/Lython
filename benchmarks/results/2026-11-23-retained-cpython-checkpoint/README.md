# B1429 Retained runtime and frontend against CPython

Measured `1b841b597` against stock CPython 3.13.16 on November 23,
2026 UTC on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
The six scripts, eight lanes, CPython executable, protocol and 31
auxiliary image files match B1419. The exact combined core contains
the retained CSV, function metadata and frontend changes.
[Previous checkpoint](../2026-11-22-callable-checkpoint-cpython/README.md).

| Complete script | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Sparse generated validator, 96 guards | Compile | 1.458–1.479 ms | 1.818–1.826 ms | 0.798–0.814× | 1.44 / 0.45% |
| Sparse generated validator, 96 guards | Compile to result | 1.541–1.556 ms | 1.959–1.966 ms | 0.784–0.794× | 0.94 / 0.32% |
| Postponed factories, 4,096 definitions | Warm | 1.014–1.197 ms | 0.541–0.544 ms | 1.865–2.213× | 18.08 / 0.50% |
| Postponed factories, 4,096 definitions | Compile to result | 1.055–1.065 ms | 0.589–0.591 ms | 1.786–1.808× | 0.96 / 0.25% |
| Quote-heavy CSV invoice, 4,096 records | Warm | 7.204–7.225 ms | 6.259–6.322 ms | 1.139–1.154× | 0.30 / 1.01% |
| Unicode word count with a nonlocal helper, 3,072 lines | Warm | 1.411–1.424 ms | 0.949–0.961 ms | 1.468–1.501× | 0.90 / 1.30% |
| Expanded JSON Lines export, 3,072 records | Warm | 5.435–5.468 ms | 6.537–6.568 ms | 0.828–0.836× | 0.60 / 0.47% |
| Small JSON configuration edit | Compile to result | 0.1090–0.1105 ms | 0.1241–0.1253 ms | 0.870–0.891× | 1.41 / 0.98% |

Ratios divide Lython elapsed by CPython elapsed and span pairings
of medians from two independent workers per engine. Workers warm
for eight seconds and supply seven rotated batches. Warm execution
includes applicable RAM-backed file I/O, output and file checks and
prior-result disposal. Lython mediates I/O and accounts for a shared
allowance. CPython uses native file APIs. Compilation includes code
owner disposal. Time to result adds execution and its cleanup.
Fixture resets and protocol contribute to the wider CPU measure.
Startup and final teardown sit outside these elapsed clocks.

The factory warm magnitude remains uncertain with 18.08% Lython
spread. Its first-result gap and the smaller text-report gap are
clear across both engines' workers. The validator and small edit
reach results sooner than CPython in these runs. Historical
cross-session differences leave the effects of individual changes
to the paired Lython comparisons.

The combined core passes 494 focused Release checks per platform
on Windows .NET 11 and Ubuntu .NET 10, and all six local CPython
goldens. All 224 timed requests verify on the VM. Both quiet gates
pass, workers join and the persistent archive verifies after
removing the guest fixtures. The capture took 424 seconds.

[Source and images](source.json) · [Timings and wider CPU](summary.json) ·
[Observations](observations.tgz) · [Checks and procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
