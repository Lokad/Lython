# B1419 Callable construction checkpoint against CPython

The November 22, 2026 UTC comparison measures `6ac96ea93` on
the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3.
The six scripts and stock CPython 3.13.16 executable match B1406.
The current Release core, optional adapter and benchmark worker
were rebuilt and verified. The other 29 image files and the
worker protocol match the earlier checkpoint. [B1406](../2026-11-22-current-source-cpython/README.md).

| Complete script | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Sparse generated validator, 96 guards | Compile | 1.463–1.481 ms | 1.822–1.865 ms | 0.784–0.813× | 1.25 / 2.33% |
| Sparse generated validator, 96 guards | Compile to result | 1.545–1.569 ms | 1.964–1.968 ms | 0.785–0.799× | 1.52 / 0.21% |
| Postponed factories, 4,096 definitions | Warm | 1.027–1.094 ms | 0.547–0.563 ms | 1.825–2.000× | 6.48 / 2.91% |
| Postponed factories, 4,096 definitions | Compile to result | 1.078–1.107 ms | 0.587–0.592 ms | 1.820–1.886× | 2.64 / 0.97% |
| Quote-heavy CSV invoice, 4,096 records | Warm | 7.258–7.294 ms | 6.303–6.359 ms | 1.141–1.157× | 0.50 / 0.89% |
| Unicode word count with a nonlocal helper, 3,072 lines | Warm | 1.479–1.572 ms | 0.977–0.980 ms | 1.510–1.609× | 6.29 / 0.26% |
| Expanded JSON Lines export, 3,072 records | Warm | 5.565–5.657 ms | 6.767–6.868 ms | 0.810–0.836× | 1.64 / 1.49% |
| Small JSON configuration edit | Compile to result | 0.1135–0.1140 ms | 0.1303–0.1304 ms | 0.871–0.875× | 0.42 / 0.07% |

Ratios divide Lython elapsed by CPython elapsed. The ranges cover all
four pairings of the medians from two workers per engine after eight
seconds of warmup and seven rotating batches. The two script gaps
with the largest Lython spreads are the postponed factories and the
nonlocal word report.

Warm timing includes applicable mediated RAM-backed reads and writes,
verification and disposal of the previous result. CPython uses native
file APIs. Compilation includes compiled-owner disposal. Time to
result adds one execution and its cleanup. Process startup, fixture
reset and final teardown have separate boundaries. Process CPU covers
the wider worker request and is recorded in the summary.

All 224 timed batches verify. Both machine gates pass, workers join
and the archive verifies after removing guest fixtures. The run uses
424 seconds across eight lanes. The previous same-workload comparison
remains available for historical ratios. Paired Lython comparisons
isolate code changes. [Native selection](../2026-11-22-native-call-selection-single-proof/README.md) ·
[Function storage](../2026-11-22-function-diagnostic-identity/README.md).

[Revisions and image SHA-256](source.json) · [Timings and CPU](summary.json) ·
[Raw observations](observations.tgz) · [Procedures](procedures.tar.xz) ·
[Changed frozen image files](changed-image-files.tar.xz).
