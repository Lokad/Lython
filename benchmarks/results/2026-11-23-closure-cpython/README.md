# B1499 CPython checkpoint after direct closure assignment

Lython `9e0cf58f8` and stock CPython 3.13.16 ran on the quiet Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.
The measured `40e99e6db` image has the same production source.
The complete 32-file image, worker protocol and CPython executable
hashes verify.

Ratios divide Lython elapsed by CPython elapsed. Ranges pair the
medians of two independent workers per engine. Each worker warms for
eight seconds and supplies seven rotated batches. Warm time includes
applicable RAM-backed I/O, golden verification and disposal of the
prior result. Lython uses an admitted, mediated host. Compilation time
includes code-owner cleanup. Compile to result combines both phases in
a running process. Wider process CPU includes resets and protocol.
Startup and final worker teardown sit outside the elapsed clock.

| Workload | Phase | Lython | CPython | Lython / CPython | Worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Short nonlocal word report | Warm | 1.32543–1.37179 ms | 0.95028–0.95521 ms | 1.388–1.444× | 3.50 / 0.52% |
| Fully supplied helper | Warm | 0.31325–0.31524 ms | 0.19517–0.19681 ms | 1.592–1.615× | 0.64 / 0.84% |
| Quote-heavy CSV invoice | Warm | 6.95285–6.97082 ms | 6.22080–6.37231 ms | 1.091–1.121× | 0.26 / 2.44% |
| Postponed nested factories | Warm | 0.89888–0.91211 ms | 0.53502–0.53512 ms | 1.680–1.705× | 1.47 / 0.02% |
| Short nonlocal word report | Compilation | 0.02873–0.02881 ms | 0.05609–0.05640 ms | 0.509–0.514× | 0.27 / 0.55% |
| Short nonlocal word report | Compile to result | 1.37956–1.52947 ms | 1.09746–1.09938 ms | 1.255–1.394× | 10.87 / 0.18% |

The short report compiles faster than CPython and spends longer on work
after compilation. Its compile-to-result magnitude is uncertain because
Lython worker medians differ by 10.87%. Use the matched B1497–B1498 pairs
for the direct-store effect. The factory and supplied-helper gaps persist
on stable workers. The higher external factory ratio than B1491 includes
movement on both engines and provides no isolated regression attribution.

Four separate CPython goldens and all 168 timed batches verify in 272
seconds. Both quiet gates pass, workers join, guest fixtures are removed
and the persistent raw archive verifies under the exclusive lease.

[Timing](summary.json) · [Identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Direct-store source pairs](../2026-11-23-closure-result-consumer/README.md) ·
[Previous external checkpoint](../2026-11-23-forwarded-global-cpython/README.md).
