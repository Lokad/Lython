# B1575 Scalar helper checkpoint against CPython

Lython `531499f09`, with runtime and tests matching `f17898c19`,
ran against stock CPython 3.13.16 on November 24, 2026 UTC.
The dedicated Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3.

| Workload and phase | Lython elapsed | CPython elapsed | Lython / CPython |
| --- | ---: | ---: | ---: |
| Captured rules, warm | 1.5302–1.5510 ms | 0.7667–0.7943 ms | 1.926–2.023× |
| Defaulted rules, warm | 1.4250–1.4631 ms | 0.6848–0.6872 ms | 2.074–2.137× |
| Short nonlocal report, warm | 1.2223–1.2588 ms | 0.9300–0.9467 ms | 1.291–1.354× |
| Quoted invoice, warm | 6.2566–6.2964 ms | 6.2693–6.3878 ms | 0.980–1.004× |
| Captured rules, compile to result | 1.5749–1.5790 ms | 0.8204–0.8395 ms | 1.876–1.925× |
| Defaulted rules, compile to result | 1.4840–1.5129 ms | 0.7290–0.7324 ms | 2.026–2.075× |
| Short nonlocal report, compile to result | 1.3674–1.3775 ms | 1.0831–1.0935 ms | 1.250–1.272× |
| Captured rules, compilation only | 0.02673–0.02718 ms | 0.04714–0.04720 ms | 0.566–0.577× |
| Short nonlocal report, compilation only | 0.02877–0.02900 ms | 0.05551–0.05574 ms | 0.516–0.522× |

Ratios span all four pairings of medians from two independent
workers per engine. The quoted invoice spans parity. Both generated
rule scripts still take roughly twice CPython's warm elapsed time.
The captured rules and short report compile faster in independently
timed lanes. The largest elapsed worker spread is 3.61% on CPython
for captured-rule warm execution. These ratios describe this image.
The separate [source comparisons](../2026-11-24-closed-scalar-branches/README.md)
measure the implementation changes and retain their controls.

Warm execution includes applicable RAM-backed file I/O, golden checks
and prior-result disposal. Lython uses mediated I/O with a shared
allowance. CPython uses native file I/O. Compile to result adds
compilation and compiled-owner disposal. Compilation alone measures
creation and disposal of compiled code. Startup, fixture reset,
request transport and final process teardown sit outside elapsed.
Wider process CPU includes request and reset overhead.

All 32 image files and four independent workload goldens verify.
Each lane warms for eight seconds per worker and contributes seven
rotated batches per worker. All 252 batches complete with both quiet
gates, worker joins, fixture removal and archive verification.

[Timing](summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
