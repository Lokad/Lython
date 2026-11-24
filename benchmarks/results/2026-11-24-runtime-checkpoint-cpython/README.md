# B1568 CSV, text, JSON and rules against CPython

This checkpoint uses frozen Lython `95aa40601`, with the runtime and
tests in `8aef89b09`, and stock CPython 3.13.16. The comparison ran on
November 24, 2026 UTC on the dedicated Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3.

| Workload and phase | Lython elapsed | CPython elapsed | Lython / CPython |
| --- | ---: | ---: | ---: |
| Typed Unicode meter, warm | 2.789–2.855 ms | 3.139–3.173 ms | 0.879–0.910× |
| Explicit-float meter, warm | 2.876–2.896 ms | 2.920–2.924 ms | 0.984–0.992× |
| Short nonlocal report, warm | 1.2836–1.3195 ms | 0.9342–0.9414 ms | 1.363–1.412× |
| Quoted invoice, warm | 6.257–6.550 ms | 6.330–6.333 ms | 0.988–1.035× |
| Captured rules, warm | 1.6961–1.7437 ms | 0.7654–0.7955 ms | 2.132–2.278× |
| Expanded JSON export, warm | 5.072–5.107 ms | 6.529–6.552 ms | 0.774–0.782× |
| Typed Unicode meter, compile to result | 3.151–3.295 ms | 3.480–3.556 ms | 0.886–0.947× |
| Short nonlocal report, compile to result | 1.4522–1.4991 ms | 1.1661–1.1756 ms | 1.235–1.286× |
| Short nonlocal report, compilation only | 0.03031–0.03057 ms | 0.05919–0.05970 ms | 0.508–0.517× |

Ratios span all four pairings of medians from two independent workers
per engine. The quoted invoice spans parity, with a 4.68% Lython worker
spread. The text report and captured rules remain slower in execution.
The small explicit-float difference describes this diagnostic collection.

Warm workers run already compiled code. The elapsed clock includes
applicable RAM-backed I/O, golden checks and prior-result disposal.
Lython uses the comparison host's mediated I/O and shared allowance.
CPython uses native file I/O. Compile to result adds compilation and
compiled-owner disposal. Compilation alone measures that phase and
its disposal independently. Startup, request framing, fixture reset
and final teardown stay outside elapsed. Wider process CPU is in the
summary. Earlier startup and generated-rules phase comparisons retain
their original revisions. [Startup](../2026-11-23-branch-cpython-checkpoint/README.md) ·
[Earlier rules](../2026-11-24-rule-closures-cpython/README.md).

The complete 32-file worker image and six local goldens verify before
staging. Each lane warms for eight seconds per worker and contributes
seven rotated samples per worker. All 252 measured batches match their
golden outputs and files. The nine lanes finish in 418 seconds including
settling outside the clocks. Both quiet gates, worker joins, fixture
cleanup and persistent archive verification pass.

[Timing](summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
