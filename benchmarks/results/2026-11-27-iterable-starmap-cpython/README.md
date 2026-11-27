# B1865 CPython comparison for iterable-row shipment

Lython `aae8992e0` runs against stock CPython 3.13.16 on November 27,
2026 UTC, on an Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
The comparison host is `05c1059de`. The run reuses all 32 verified
files from the B1864 candidate. [Matched Lython source pair](../2026-11-27-iterable-starmap-arguments/README.md).

Both scripts read the same 2,048-record, 27,350-byte CSV file. The
shipment report yields four arguments per record into `starmap` and
sums its callback results. The file-fed map is the existing quantity
sum control. Both independent CPython output and file goldens pass.

| Warm workload | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Iterable-row shipment, `Run` | 2.60896–2.67250 ms | 1.97476–1.98458 ms | 1.315–1.353× |
| Iterable-row shipment, `RunAsync` | 3.03004–3.09430 ms | 1.96767–1.99960 ms | 1.515–1.573× |
| File-fed map, `RunAsync` | 0.40958–0.41425 ms | 1.36354–1.36786 ms | 0.299–0.304× |

Two workers per engine warm for at least five seconds and contribute
seven rotated batches. Ratios span the pairings of worker medians.
Values above 1× mean Lython takes longer. CPython uses its ordinary
synchronous Python and file APIs in every lane. Lython uses mediated
I/O and active shared-memory accounting. Warm elapsed includes
RAM-backed I/O, verification and disposal of the previous result.
Wider process CPU also includes framing and fixture reset. Compilation
and startup are outside these clocks.

The generator-backed report has a larger gap than the earlier exact-row
shipment workload. The file-fed ratio overlaps B1849, whose input and
phase match this run. The ratio comparison is diagnostic.
[Earlier checkpoint](../2026-11-27-post-ready-insertion-cpython/README.md).

Both quiet gates and all 84 batches pass. Collection takes 116.97
seconds. All workers join and the persistent archive verifies after
guest fixtures are removed.

[Timing summary](summary.json) · [Fixture sizes](fixture-census.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Checks and procedures](procedures.tar.xz).
