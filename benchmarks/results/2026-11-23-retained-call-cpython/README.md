# B1510 CPython checkpoint on retained native calls

Lython `b7fdb4fec` and stock CPython 3.13.16 ran on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3, November 23, 2026 UTC.
The Lython core was built from the integrated source. All 32 image files,
the worker protocol and the CPython executable verify.

Ratios divide Lython time by CPython time. Ranges pair the medians of two
independent workers per engine. Workers warm for eight seconds and supply
seven rotated batches each. Warm elapsed includes applicable RAM-backed
I/O, golden verification and prior-result disposal. Lython uses an admitted
mediated host. Compile to result adds compilation and code-owner disposal
in already-running processes. Wider CPU includes reset and transport.
Startup and final teardown are outside elapsed.

| Workload | Boundary | Lython | CPython | Lython / CPython | Wider CPU ratio | Worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Eager nested factories | Warm | 0.78323–0.83364 ms | 0.57753–0.57799 ms | 1.355–1.443× | 1.328–1.457× | 6.44 / 0.08% |
| Postponed nested factories | Warm | 0.75055–0.77166 ms | 0.58057–0.58883 ms | 1.275–1.329× | 1.286–1.346× | 2.81 / 1.42% |
| Fully supplied arithmetic | Warm | 0.30810–0.31095 ms | 0.19990–0.20321 ms | 1.516–1.555× | 1.552–1.586× | 0.92 / 1.65% |
| Quote-heavy invoice | Warm | 7.18688–7.27982 ms | 6.55458–6.61577 ms | 1.086–1.111× | 1.093–1.125× | 1.29 / 0.93% |
| Eager nested factories | Compile to result | 0.79208–0.87387 ms | 0.61336–0.63294 ms | 1.251–1.425× | 1.251–1.419× | 10.33 / 3.19% |
| Postponed nested factories | Compile to result | 0.75216–0.77976 ms | 0.57806–0.58705 ms | 1.281–1.349× | 1.286–1.393× | 3.67 / 1.55% |
| Fully supplied arithmetic | Compile to result | 0.32343–0.33148 ms | 0.21693–0.22356 ms | 1.447–1.528× | 1.440–1.558× | 2.49 / 3.06% |
| Short nonlocal word report | Compile to result | 1.42310–1.52081 ms | 1.08575–1.09720 ms | 1.297–1.401× | 1.279–1.369× | 6.87 / 1.06% |

The supplied arithmetic gap persists on stable workers through both
boundaries. Eager and short-report worker spreads
limit their magnitudes. Absolute times move on both engines between
checkpoints. Use the matched B1504–B1505 source pairs for attribution.
Earlier compile-only, startup and short-report warm measurements remain
at their original revisions.

Five workload goldens and 224 timed batches verify in 363 seconds.
Both quiet gates pass, workers join, and the persistent archive verifies
under the lease after fixture removal. The first staging attempt has
insufficient space at its original staging floor and starts no worker.
The archived log includes retirement of one already archived core replica.
The successful stage changes the disk reservation and reuses the same
image and timings configuration.

[Timing](summary.json) · [Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Validation and procedures](procedures.tar.xz) ·
[Matched source comparison](../2026-11-23-supplied-scalar-return/README.md) ·
[Earlier external checkpoint](../2026-11-23-computed-factory-cpython/README.md).
