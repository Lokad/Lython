# B1109 retained factories and forwarding against CPython

This diagnostic uses Lython `3e67fd010`, core SHA-256 beginning `aa15d342`,
and stock CPython 3.13.16. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 with
.NET 11 RC3 on November 19, 2026 UTC. The complete 32-file Lython image
and four CPython workload goldens passed verification before collection.
Ratios divide Lython elapsed by CPython elapsed across all four
combinations of duplicate process medians. Above one means slower.

| Workload | Boundary | Lython | CPython | Ratio | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Unannotated rule closures | Warm | 2.459–2.470 ms | 0.693–0.699 ms | 3.52–3.57× | B1109 diagnostic. Controls L/C 0.43 / 0.88%. IQR ≤1.47%. |
| Unannotated rule closures | Compile-to-result | 2.526–2.545 ms | 0.736–0.752 ms | 3.36–3.46× | B1109 diagnostic. Controls L/C 0.74 / 2.17%. IQR ≤1.66%. |
| Unannotated rule closures | Compilation only | 0.03122–0.03123 ms | 0.04178–0.04189 ms | 0.75× | B1109 diagnostic. Controls L/C 0.02 / 0.28%. IQR ≤0.85%. |
| Captured rule closures | Warm | 2.860–2.885 ms | 0.782–0.796 ms | 3.59–3.69× | B1109 diagnostic. Controls L/C 0.87 / 1.79%. IQR ≤0.75%. |
| Captured rule closures | Compile-to-result | 2.934–2.960 ms | 0.839–0.848 ms | 3.46–3.53× | B1109 diagnostic. Controls L/C 0.88 / 1.17%. IQR ≤2.11%. |
| Captured rule closures | Compilation only | 0.03553–0.03594 ms | 0.04734–0.04741 ms | 0.75–0.76× | B1109 diagnostic. Controls L/C 1.15 / 0.15%. IQR ≤0.64%. |
| Expanded forwarding report | Warm | 1.853–1.891 ms | 1.180–1.187 ms | 1.56–1.60× | B1109 diagnostic. Controls L/C 2.06 / 0.58%. IQR ≤3.93%. |
| Expanded forwarding report | Compile-to-result | 1.958–2.007 ms | 1.320–1.321 ms | 1.48–1.52× | B1109 diagnostic. Controls L/C 2.50 / 0.04%. IQR ≤2.15%. |
| Expanded forwarding report | Compilation only | 0.04857–0.05090 ms | 0.06165–0.06317 ms | 0.77–0.83× | B1109 diagnostic. Controls L/C 4.81 / 2.47%. IQR ≤3.75%. Gap size uncertain. |
| Retained nonlocal siblings | Warm | 0.169–0.170 ms | 0.188–0.190 ms | 0.89–0.90× | B1109 diagnostic. Controls L/C 0.57 / 0.89%. IQR ≤2.72%. |
| Retained nonlocal siblings | Compile-to-result | 0.221 ms | 0.252–0.255 ms | 0.87–0.88× | B1109 diagnostic. Controls L/C 0.10 / 0.96%. IQR ≤1.02%. |
| Retained nonlocal siblings | Compilation only | 0.04323–0.04410 ms | 0.05508–0.05515 ms | 0.78–0.80× | B1109 diagnostic. Controls L/C 2.01 / 0.12%. IQR ≤2.17%. |

Warm execution includes mediated RAM-backed I/O, golden checks and
previous-result cleanup. Compilation includes compiled-owner disposal.
Compile-to-result adds compilation and compiled-owner disposal to the
execution boundary. CPython uses its native file APIs. The Lython worker
uses shared accounting and checks the returned allowance. Guest clocks
exclude startup, fixture reset, request framing and final cache teardown.
The wider process CPU and control spreads are preserved in the summary.

All 240 timed batches verified in 280.39 seconds.
Both quiet gates passed, workers joined, fixtures were removed and the
observation archive was verified under the VM lease.

[Benchmark guide](../../../docs/BENCHMARKS.md) · [Summary](summary.json) ·
[Raw observations](observations.tgz) · [Lease receipt](archive-receipt.json) ·
[Procedures](procedures.tar.xz).
