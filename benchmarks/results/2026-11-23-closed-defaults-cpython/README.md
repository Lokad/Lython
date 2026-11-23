# B1516 CPython checkpoint after direct default ownership transfer

Retained Lython `09fc2c0a5` and stock CPython 3.13.16 ran on a quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3, November 23, 2026 UTC.
The exact candidate DLL built at `655674b33` matches the retained
production sources. All 32 image files, the CPython executable and the
comparison protocol verify. [B1515 matched Lython comparison](../2026-11-23-direct-closed-defaults/README.md).

Ratios divide Lython time by CPython time. Ranges pair the medians of two
independent workers per engine. Each warms for eight seconds and supplies
seven rotated batches. Warm elapsed includes applicable mediated RAM-backed
I/O, golden verification and prior-result disposal. Compile to result adds
warm-process compilation and code-owner disposal. Compilation alone
includes code-owner disposal and executes no script. Wider process CPU
includes worker resets and transport. Startup and teardown sit outside
these elapsed clocks. [Earlier startup evidence](../2026-11-23-runtime-cpython-checkpoint/README.md).

| Workload | Phase | Lython | CPython | Lython / CPython | Wider CPU ratio | Elapsed spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Eager nested factories | Warm | 0.74488–0.75320 ms | 0.56928–0.58896 ms | 1.265–1.323× | 1.259–1.337× | 1.12 / 3.46% |
| Postponed nested factories | Warm | 0.69882–0.71872 ms | 0.53707–0.54442 ms | 1.284–1.338× | 1.285–1.334× | 2.85 / 1.37% |
| Quote-heavy invoice | Warm | 7.01970–7.04651 ms | 6.32465–6.48856 ms | 1.082–1.114× | 1.088–1.119× | 0.38 / 2.59% |
| Fully supplied arithmetic | Warm | 0.32027–0.32135 ms | 0.20343–0.20710 ms | 1.547–1.580× | 1.554–1.587× | 0.34 / 1.80% |
| Eager nested factories | Compile to result | 0.75658–0.78979 ms | 0.60699–0.61959 ms | 1.221–1.301× | 1.237–1.289× | 4.39 / 2.08% |
| Postponed nested factories | Compile to result | 0.71363–0.73516 ms | 0.58275–0.58631 ms | 1.217–1.261× | 1.194–1.268× | 3.02 / 0.61% |
| Postponed nested factories | Compilation only | 0.02105–0.02124 ms | 0.03954–0.03961 ms | 0.532–0.537× | 0.540–0.554× | 0.88 / 0.17% |

Postponed compilation is a small share of time to result and is faster
than CPython in this comparison. The warm factory and supplied helper
remain slower. Invoice warm stays close to the earlier comparison.
Both engines' absolute clocks move across checkpoints. Use B1515 to
attribute the effect of the runtime change. [B1510](../2026-11-23-retained-call-cpython/README.md).

Four workload goldens and 196 timed batches verify in 317 seconds.
Both quiet gates pass, all workers join and the collector removes its
fixtures. The persistent archive is verified under the lease. The first
stage stops at the storage floor before creating its work directory.
The corrected stage reuses the exact images and reserves two MiB
for receipts. The original conditions are preserved.

[Identity](identity.json) · [Timing](summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Checks and procedures](procedures.tar.xz).
