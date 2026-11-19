# B1114 retained rule and CSV scripts against CPython

This diagnostic uses Lython `2841f6ac2`, core SHA-256 beginning `a2ec3ec4`,
and stock CPython 3.13.16. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 with
.NET 11 RC3 on November 19, 2026 UTC. The complete 32-file Lython image
and four CPython workload goldens passed verification before collection.
Ratios divide Lython elapsed by CPython elapsed across all four
combinations of duplicate process medians. Above one means slower.

| Workload | Boundary | Lython | CPython | Ratio | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Unannotated rule closures | Warm | 2.421–2.490 ms | 0.687–0.688 ms | 3.52–3.62× | B1114 diagnostic. Controls L/C 2.83 / 0.17%. IQR ≤0.76%. |
| Unannotated rule closures | Compile-to-result | 2.504–2.533 ms | 0.734–0.735 ms | 3.41–3.45× | B1114 diagnostic. Controls L/C 1.15 / 0.05%. IQR ≤1.73%. |
| Unannotated rule closures | Compilation only | 0.03125–0.03151 ms | 0.04178–0.04222 ms | 0.74–0.75× | B1114 diagnostic. Controls L/C 0.83 / 1.05%. IQR ≤0.55%. |
| Annotated rule closures | Warm | 1.745–1.834 ms | 0.429–0.436 ms | 4.01–4.27× | B1114 diagnostic. Controls L/C 5.14 / 1.49%. IQR ≤6.12%. Gap size uncertain. |
| Annotated rule closures | Compile-to-result | 1.795–1.862 ms | 0.463–0.464 ms | 3.86–4.02× | B1114 diagnostic. Controls L/C 3.75 / 0.23%. IQR ≤5.64%. |
| Annotated rule closures | Compilation only | 0.02393–0.02534 ms | 0.03267–0.03286 ms | 0.73–0.78× | B1114 diagnostic. Controls L/C 5.86 / 0.59%. IQR ≤5.99%. Gap size uncertain. |
| Captured rule closures | Warm | 2.738–2.861 ms | 0.775–0.801 ms | 3.42–3.69× | B1114 diagnostic. Controls L/C 4.52 / 3.30%. IQR ≤1.64%. Gap size uncertain. |
| Captured rule closures | Compile-to-result | 2.809–2.942 ms | 0.829–0.857 ms | 3.28–3.55× | B1114 diagnostic. Controls L/C 4.74 / 3.42%. IQR ≤1.20%. Gap size uncertain. |
| Captured rule closures | Compilation only | 0.03588–0.03619 ms | 0.04749–0.04759 ms | 0.75–0.76× | B1114 diagnostic. Controls L/C 0.88 / 0.21%. IQR ≤1.10%. |
| CSV invoice | Warm | 7.174–7.230 ms | 6.439–6.564 ms | 1.09–1.12× | B1114 diagnostic. Controls L/C 0.79 / 1.95%. IQR ≤3.04%. |
| CSV invoice | Compile-to-result | 7.391–7.406 ms | 6.710–6.804 ms | 1.09–1.10× | B1114 diagnostic. Controls L/C 0.21 / 1.40%. IQR ≤0.65%. |
| CSV invoice | Compilation only | 0.09178–0.09497 ms | 0.09994–0.10061 ms | 0.91–0.95× | B1114 diagnostic. Controls L/C 3.48 / 0.67%. IQR ≤0.97%. |

Warm execution includes mediated RAM-backed I/O, golden checks and
previous-result cleanup. Compilation includes compiled-owner disposal.
Compile-to-result adds compilation and compiled-owner disposal to the
execution boundary. CPython uses its native file APIs. The Lython worker
uses shared accounting and checks the returned allowance. Guest clocks
exclude startup, fixture reset, request framing and final cache teardown.
The wider process CPU and control spreads are preserved in the summary.

All 240 timed batches verified in 279.70 seconds.
Both quiet gates passed, workers joined, fixtures were removed and the
observation archive was verified under the VM lease.

[Benchmark guide](../../../docs/BENCHMARKS.md) · [Summary](summary.json) ·
[Raw observations](observations.tgz) · [Lease receipt](archive-receipt.json) ·
[Procedures](procedures.tar.xz).
