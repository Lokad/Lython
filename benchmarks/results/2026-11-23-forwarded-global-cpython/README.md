# B1491 CPython checkpoint after forwarding call operands

Lython `2b501135a` and stock CPython 3.13.16 ran on the quiet Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.
The measured `f9de5fd92` image has identical production and test
sources. All 32 image files and the external executable hash verify.

Ratios divide Lython elapsed time by CPython elapsed time. Ranges
pair medians from two independent workers per engine, each with
an eight-second warmup and seven rotated batches. Warm elapsed
includes applicable RAM-backed I/O, output and file verification,
and prior-result disposal. Lython uses an admitted, mediated host.
Compilation includes code-owner cleanup. Time to result combines
both phases in a running worker. Wider process CPU includes resets
and protocol. Startup and final worker teardown sit outside elapsed.

| Workload | Phase | Lython | CPython | Lython / CPython | Worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Quote-heavy CSV invoice | Warm | 6.87804–6.95278 ms | 6.31851–6.39606 ms | 1.075–1.100× | 1.09 / 1.23% |
| Short nonlocal word report | Warm | 1.34031–1.36424 ms | 0.93947–0.94307 ms | 1.421–1.452× | 1.79 / 0.38% |
| Mixed-width word histogram | Warm | 1.60861–1.64604 ms | 1.72266–1.73797 ms | 0.926–0.956× | 2.33 / 0.89% |
| Postponed nested factories | Warm | 0.87568–0.88718 ms | 0.54030–0.55833 ms | 1.568–1.642× | 1.31 / 3.34% |
| Configuration edit | Compilation | 0.01898–0.01912 ms | 0.03401–0.03426 ms | 0.554–0.562× | 0.72 / 0.72% |
| Short nonlocal word report | Compile to result | 1.36598–1.41688 ms | 1.08078–1.09741 ms | 1.245–1.311× | 3.73 / 1.54% |

The short report's CPython times overlap B1483 and Lython's warm
elapsed is lower. B1489 and B1490 provide controlled relative timings
for the retained change. The external invoice ratio and B1490's small
relative invoice regression describe different source pairs. The
configuration compilation and factory retain earlier comparable
results. These observations describe the measured revision and host.

Five independent CPython goldens and all 168 timed batches verify in
273 seconds. Worker joins, both quiet gates, fixture removal and
persistent archive verification pass under the exclusive lease.

[Timings](summary.json) · [Identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Previous external checkpoint](../2026-11-23-closed-definition-cpython/README.md) ·
[Controlled repeat](../2026-11-23-forwarded-global-confirmation/README.md).
