# B1981 CPython comparison after generator frame deferral

Lython `f231ce76a` and stock CPython 3.13.16 ran on November 28, 2026 UTC
on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Both scripts consume
2,048 CSV records in 27,350 bytes. Lython uses `RunAsync` and the same
host and borrowed allowance as B1978. CPython uses its native file
APIs with `-I -S -X utf8`.

| Warm workload | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | ---: | ---: | ---: | ---: |
| Iterable-row shipment | 2.00750–2.01325 ms | 1.95756–1.96021 ms | 1.024–1.028× | 1.035–1.048× |
| Ordinary tuple reduction | 1.99760–2.00408 ms | 1.99027–2.00561 ms | 0.996–1.007× | 1.003–1.023× |

Ratios divide Lython by CPython. Two workers per engine warm for at
least five seconds each and contribute seven rotated batches. Ranges
span pairings of worker medians. Elapsed includes RAM-backed I/O,
result and file verification, and previous-result disposal. Process
CPU also includes fixture reset and framing. Compilation and process
launch sit outside both clocks. All elapsed worker spreads stay below
0.78%.

Both output and file goldens pass locally and on the VM. Both quiet
gates and all 56 batches verify. The campaign reuses the 32 verified
B1980 image files. The workers join, the RAM-backed fixtures are
removed, and the persistent archive verifies. The earlier external
ratios and the matched measurement of the runtime change remain
available. [B1978](../2026-11-28-current-mixed-cpython/README.md) ·
[B1980](../2026-11-28-deferred-starmap-argument-frame/README.md).

[Timing summary](summary.json) · [Worker identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
