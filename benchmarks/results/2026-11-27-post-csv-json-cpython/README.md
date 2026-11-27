# B1844 Shipment, nested JSON, iterable CSV and compilation against CPython

On November 27, 2026 UTC, the quiet Azure F4as_v7 ran Lython
`a1b3a3325`, the comparison host from `05c1059de`
and stock CPython 3.13.16 on Ubuntu 24.04.4 with .NET 11 RC3. Seven complete
scripts match independent CPython output and file goldens locally and on
the VM. Both quiet gates and all 224 timed batches pass.

| Workload | Input | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | --- | ---: | ---: | ---: | --- |
| Five-item starmap shipment report | 2,048 records / 27,350 B | Warm async | 2.22055–2.25324 ms | 1.82279–1.83105 ms | 1.213–1.236× | Diagnostic |
| Nested-object JSON document summary | 2,048 records / 103,331 B | Warm sync | 0.73690–0.74752 ms | 0.67124–0.67384 ms | 1.094–1.114× | Diagnostic |
| CSV summary after reading and splitting lines | 4,096 records / 57,361 B | Warm sync | 2.24651–2.30881 ms | 3.15392–3.21418 ms | 0.699–0.732× | Diagnostic |
| Generated ordinary definitions | 96 definitions / 8,641 B source | Compilation only | 0.57069–0.57212 ms | 0.94880–0.95184 ms | 0.600–0.603× | Diagnostic |
| Nested-object JSON Lines summary | 4,096 records / 159,744 B | Warm async | 3.60192–3.64138 ms | 4.44444–4.46048 ms | 0.808–0.819× | Diagnostic |
| CSV summary through a line generator | 4,096 records / 57,361 B | Warm async | 3.26270–3.36718 ms | 3.36407–3.37740 ms | 0.966–1.001× | Inconclusive around parity |
| Generated guarded report | 96 rules / 24,587 B source | Compilation only | 1.07349–1.09714 ms | 1.86806–1.88237 ms | 0.570–0.587× | Diagnostic |
| Generated guarded report | 96 rules / 24,587 B source | Compile-to-result | 1.12661–1.13546 ms | 1.92108–1.92126 ms | 0.586–0.591× | Diagnostic |

Each lane uses two workers per engine, at least five seconds of warmup per
worker and seven rotated batches per worker. The ratios divide Lython elapsed
by CPython elapsed. Ranges span all four pairings of worker medians. Warm
includes applicable RAM-backed I/O, verification and prior-result disposal.
Process CPU also includes fixture reset and request framing. Lython exercises
host-mediated I/O and shared-memory admission. CPython uses its native file APIs.
Async rows execute the same ordinary Python source. Compilation includes
compiled-owner disposal. Compile to result adds one verified run and cleanup.
Process startup sits outside these lanes.

The wide shipment gap narrows from 1.334–1.409× at the prior quiet
checkpoint to 1.213–1.236× here. The Lython medians move from 2.416–2.539 ms
to 2.221–2.253 ms. Both checkpoints use the same comparison host and
measure the runtime changes together. The remaining gap motivates the next
shipment investigation. [B1834](../2026-11-27-current-cpython/README.md).

All 32 image files verify. Staging streams 6,464,406 bytes and reuses 31
immutable files from the previous checkpoint. It starts with 12,222,464 B
free and leaves 5,668,864 B before collection. There are no archived-image
changes. All workers join and the RAM-backed fixtures are removed before
the persistent archive verifies. The VM has about 4.8 MB free after the run.

[Image](identity.json) · [Ratios and worker spreads](summary.json) ·
[Fixture census](fixture-census.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Staging](stage-custody.json) ·
[Procedures](procedures.tar.xz).
