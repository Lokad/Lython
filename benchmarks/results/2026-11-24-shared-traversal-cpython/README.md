# B1587 Representative scripts against CPython

Frozen Lython `ccdf43a59` has the runtime source delivered at `2cf721f73`.
The reference is stock CPython 3.13.16. The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.

| Workload and phase | Lython elapsed | CPython elapsed | Lython / CPython |
| --- | ---: | ---: | ---: |
| Captured rules, 2,048 groups, warm | 1.5526–1.5733 ms | 0.7736–0.7761 ms | 2.001–2.034× |
| Defaulted rules, 2,048 groups, warm | 1.3776–1.4193 ms | 0.6798–0.6963 ms | 1.978–2.088× |
| Short text report, 3,072 lines, warm | 1.2813–1.3160 ms | 0.9457–0.9592 ms | 1.336–1.392× |
| Quoted invoice, 4,096 rows, warm | 6.2153–6.3328 ms | 6.2519–6.3261 ms | 0.982–1.013× |
| Expanded JSON Lines, 3,072 records, warm | 5.1299–5.2194 ms | 6.5146–6.7011 ms | 0.766–0.801× |
| Defaulted rules, compile-to-result | 1.4187–1.4535 ms | 0.7339–0.7353 ms | 1.929–1.980× |
| Short text report, compile-to-result | 1.2728–1.4254 ms | 1.0919–1.0922 ms | 1.165–1.305× |
| Expanded JSON Lines, compile-to-result | 5.2734–5.3220 ms | 6.6247–6.7093 ms | 0.786–0.803× |
| Defaulted rules, compilation only | 0.02369–0.02369 ms | 0.04133–0.04169 ms | 0.568–0.573× |
| Short text report, compilation only | 0.02878–0.02890 ms | 0.05530–0.05561 ms | 0.518–0.523× |

The generated rules still take about twice CPython's warm elapsed time.
Quoted invoice spans parity. Expanded JSON Lines leads on warm and
compile-to-result. The short text report compiles faster and takes longer
through result, with 11.99% elapsed spread between the Lython workers on
that phase. The exact through-result difference is uncertain. These are
external ratios for this image. The paired source comparison records the
JSON traversal change separately. [B1586](../2026-11-24-json-traversal/README.md).

Warm elapsed includes applicable RAM-backed I/O, golden verification and
previous-result disposal. Lython uses a mediated host with borrowed memory
admission. CPython uses native files. Compile-to-result adds compilation
and compiled-owner cleanup in the already-running worker. The compilation
lane measures creation and disposal of compiled code. Process launch,
fixture reset, request transport and final teardown sit outside elapsed.
Wider process CPU includes reset and protocol. Each phase is measured
independently.

Ranges span pairings of medians from two workers per engine. Each worker
warms for eight seconds and contributes seven rotated batches per lane.
All 32 frozen image files, five goldens, 280 timed batches, machine gates,
worker joins, fixture removal and persistent archive verification pass.

[Timing and worker spread](summary.json) · [Images](identity.json) ·
[Frozen core](../2026-11-24-json-traversal/candidate-core.dll.gz) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
