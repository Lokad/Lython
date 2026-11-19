# B1122 retained rule and compiler workloads against CPython

This diagnostic uses Lython `0d16f8307`, core SHA-256 beginning `99840bea`,
and stock CPython 3.13.16. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 with
.NET 11 RC3 on November 19, 2026 UTC. The complete 32-file Lython image
and five CPython workload goldens passed verification before collection.
Ratios divide Lython elapsed by CPython elapsed across all four
combinations of duplicate process medians. Above one means slower.

| Workload | Boundary | Lython | CPython | Ratio | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Unannotated rule closures | Warm | 2.307–2.321 ms | 0.683–0.688 ms | 3.35–3.40× | B1122 diagnostic. Controls L/C 0.61 / 0.70%. IQR ≤0.74%. |
| Unannotated rule closures | Compile-to-result | 2.372–2.375 ms | 0.728–0.729 ms | 3.25–3.26× | B1122 diagnostic. Controls L/C 0.13 / 0.20%. IQR ≤1.60%. |
| Unannotated rule closures | Compilation only | 0.03072–0.03102 ms | 0.04189–0.04201 ms | 0.73–0.74× | B1122 diagnostic. Controls L/C 1.00 / 0.30%. IQR ≤0.44%. |
| Annotated rule closures | Warm | 1.635–1.698 ms | 0.409–0.411 ms | 3.98–4.16× | B1122 diagnostic. Controls L/C 3.86 / 0.49%. IQR ≤1.94%. Gap size uncertain. |
| Annotated rule closures | Compile-to-result | 1.668–1.726 ms | 0.431–0.435 ms | 3.83–4.00× | B1122 diagnostic. Controls L/C 3.43 / 0.96%. IQR ≤3.97%. |
| Annotated rule closures | Compilation only | 0.02342–0.02344 ms | 0.03100–0.03110 ms | 0.75–0.76× | B1122 diagnostic. Controls L/C 0.07 / 0.30%. IQR ≤1.11%. |
| Captured rule closures | Warm | 2.437–2.454 ms | 0.776–0.790 ms | 3.09–3.16× | B1122 diagnostic. Controls L/C 0.71 / 1.69%. IQR ≤1.69%. |
| Captured rule closures | Compile-to-result | 2.519–2.523 ms | 0.830–0.839 ms | 3.00–3.04× | B1122 diagnostic. Controls L/C 0.15 / 1.09%. IQR ≤1.51%. |
| Captured rule closures | Compilation only | 0.03460–0.03524 ms | 0.04742–0.04748 ms | 0.73–0.74× | B1122 diagnostic. Controls L/C 1.86 / 0.14%. IQR ≤1.31%. |
| Generated validator | Compile-to-result | 2.407–2.410 ms | 1.980–1.986 ms | 1.21–1.22× | B1122 diagnostic. Controls L/C 0.10 / 0.27%. IQR ≤2.05%. |
| Generated validator | Compilation only | 2.20852–2.23467 ms | 1.82797–1.83637 ms | 1.20–1.22× | B1122 diagnostic. Controls L/C 1.18 / 0.46%. IQR ≤1.07%. |
| Configuration manifest | Compile-to-result | 0.450–0.452 ms | 0.464–0.469 ms | 0.96–0.97× | B1122 diagnostic. Controls L/C 0.31 / 1.09%. IQR ≤2.44%. |
| Configuration manifest | Compilation only | 0.15549–0.16076 ms | 0.15733–0.15805 ms | 0.98–1.02× | B1122 diagnostic. Controls L/C 3.39 / 0.46%. IQR ≤0.44%. Spans parity. |

Warm execution includes mediated RAM-backed I/O, golden checks and
previous-result cleanup. Compilation includes compiled-owner disposal.
Compile-to-result adds compilation and compiled-owner disposal to the
execution boundary. CPython uses its native file APIs. The Lython worker
uses shared accounting and checks the returned allowance. Guest clocks
exclude startup, fixture reset, request framing and final cache teardown.
The wider process CPU and control spreads are preserved in the summary.
The measured source later failed unrelated generator-lambda and
iterator-cleanup fixture checks in the broader suite.

All 260 timed batches verified in 289.49 seconds.
Both quiet gates passed, workers joined, fixtures were removed and the
observation archive was verified under the VM lease.

[Benchmark guide](../../../docs/BENCHMARKS.md) · [Summary](summary.json) ·
[Raw observations](observations.tgz) · [Lease receipt](archive-receipt.json) ·
[Procedures](procedures.tar.xz).
