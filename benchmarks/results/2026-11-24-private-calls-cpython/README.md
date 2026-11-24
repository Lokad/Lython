# B1600 Private closure calls against CPython

The 32-file image at `642c8f957` matches the runtime and tests delivered
at `c40540135`. This checkpoint uses stock CPython 3.13.16 and .NET 11 RC3
on the quiet Azure F4as_v7 running Ubuntu 24.04.4 on November 24, 2026 UTC.
Each engine has two workers per lane. Seven rotated batches follow eight
seconds of warmup per worker. Ratios divide Lython elapsed by CPython elapsed
across the four pairings of worker medians. Above 1× means Lython takes longer.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Generated rules with captured locals | Warm | 1.33308–1.37931 ms | 0.77580–0.77780 ms | 1.714–1.778× | 3.47 / 0.26% |
| Postponed nested annotated rules | Warm | 0.58887–0.62014 ms | 0.54503–0.54605 ms | 1.078–1.138× | 5.31 / 0.19% |
| Quote-heavy CSV policy invoice | Warm | 6.21401–6.25986 ms | 6.32403–6.34140 ms | 0.980–0.990× | 0.74 / 0.27% |
| Generated rules with captured locals | Compilation only | 0.02666–0.02686 ms | 0.04730–0.04741 ms | 0.562–0.568× | 0.77 / 0.25% |
| Generated rules with captured locals | Compile to result | 1.33545–1.33750 ms | 0.81862–0.82110 ms | 1.626–1.634× | 0.15 / 0.30% |

Each rule script constructs and applies 2,048 groups and captures stdout.
The invoice processes 4,096 records in a 174,147-byte RAM-backed fixture.
Warm execution uses compiled code and includes applicable I/O, result
verification and previous-result disposal. Compilation includes disposal of
the code owner. Compile to result adds one execution and its cleanup.
Wider process CPU includes request framing and fixture reset. Startup and
final teardown sit outside all five lanes.

Captured rules have their largest remaining gap in warm execution. Their
compile-to-result medians agree more closely across workers. The quote
control is near external parity. The within-Lython comparison in B1599
still records the postponed and quoted regressions against `722b49f2d`.
All three goldens, 140 timed batches, both quiet gates, worker joins,
fixture cleanup and persistent archive verification pass.

[Timings and CPU](summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json) ·
[B1599 within Lython](../2026-11-24-private-definition-call/isolation/README.md) ·
[Earlier CPython checkpoint](../2026-11-24-closed-pairs-cpython/README.md).
