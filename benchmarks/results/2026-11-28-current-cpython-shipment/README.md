# B1937 Quiet shipment comparison after direct frame transfer

This checkpoint measures exact Lython `e036d6597` against stock CPython
3.13.16 on November 28, 2026 UTC. The four-vCPU Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3. All 32 worker files match the recorded
image, and all four output and file goldens and 140 timed batches
verify. Both machine gates pass on the completed run.

| Warm workload | Lython API | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Iterable shipment | `Run` | 2.01506–2.07370 ms | 1.97285–1.97362 ms | 1.021–1.051× | 1.020–1.045× |
| Iterable shipment | `RunAsync` | 2.20086–2.20510 ms | 1.93885–1.94706 ms | 1.130–1.137× | 1.126–1.145× |
| Ordinary helper and generator reduction | `RunAsync` | 3.13237–3.19689 ms | 1.97443–1.97768 ms | 1.584–1.619× | 1.567–1.592× |
| File-fed CSV quantity map | `RunAsync` | 0.42560–0.43160 ms | 1.35824–1.36216 ms | 0.312–0.318× | 0.346–0.352× |
| File search, match at line 1 | `RunAsync` | 0.06913–0.06951 ms | 0.04994–0.05086 ms | 1.359–1.392× | 1.213–1.249× |

Each ratio divides Lython by CPython. Ranges span pairings of worker
medians. The shipment and map scripts use 2,048 records from a
27,350-byte CSV. The first-line search uses a 42,010-byte file. Warm
elapsed includes applicable RAM-backed I/O, verification and previous
result disposal. Process CPU also includes framing and fixture reset.
Compilation and startup are outside both clocks.

Async iterable elapsed remains close to the earlier quiet range. The
independent snapshots leave attribution of the difference open. The
ordinary reduction now has the largest non-regex CPU gap in this
checkpoint. The simpler map on the same file remains substantially
faster than CPython. Profile generator reduction before selecting a
further change. [Prior quiet snapshot](../2026-11-28-current-cpython-regex-checkpoint/README.md) ·
[Matched Lython change](../2026-11-28-staged-generator-activation/README.md).

The first launch failed its idle CPU gate before timing. A retry reused
the exact candidate image, catalog and thresholds. An inactive test
build was archived and verified before it was removed from the VM,
recovering 51.9 MiB for persistent staging. The failed machine gate,
archive verification and terminal receipts are in the procedures.

[Timing](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures and first attempt](procedures.tar.xz).
