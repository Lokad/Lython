# B1978 CPython comparison for CSV, file search and configuration

Lython `e78a95b1b` and stock CPython 3.13.16 ran on November 28, 2026 UTC
on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. The frozen Lython
worker includes the optional signed PythonRe 0.5.0 adapter. The timed
scripts exercise CSV, text file and JSON APIs.

| Workload | Input | Boundary | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| Ordinary helper and generator reduction | 2,048 records / 27,350 B | Warm async | 1.99954–2.00830 ms | 1.97528–1.97758 ms | 1.011–1.017× | 1.020–1.031× |
| Iterable-row shipment | 2,048 records / 27,350 B | Warm async | 2.22377–2.23538 ms | 1.96366–1.96626 ms | 1.131–1.138× | 1.135–1.147× |
| File search, match at line 1 | 42,010 B | Warm async | 0.06725–0.06781 ms | 0.05090–0.05093 ms | 1.320–1.332× | 1.186–1.229× |
| Configuration edit | 357 B source / 181 B input | Sync compile to result | 0.09338–0.09448 ms | 0.12413–0.12577 ms | 0.742–0.761× | 0.710–0.733× |

Ratios divide Lython by CPython. Two workers per engine warm for at
least five seconds and contribute seven rotated batches each. Ranges
span pairings of worker medians. Elapsed includes mediated I/O for
Lython, RAM-backed fixtures for both engines, result and file checks,
and previous-result disposal. Process CPU also includes fixture reset
and framing. The configuration lane includes compilation and compiled
owner cleanup. Process launch sits outside both clocks. CPython runs
with `-I -S -X utf8` and native file APIs.

| Workload | Lython elapsed worker spread | CPython elapsed worker spread |
| --- | ---: | ---: |
| Ordinary helper and generator reduction | 0.44% | 0.12% |
| Iterable-row shipment | 0.52% | 0.13% |
| File search, match at line 1 | 0.82% | 0.07% |
| Configuration edit | 1.18% | 1.32% |

All four independent output and file goldens pass locally and on the
VM. Both quiet gates and all 112 batches pass. Staging reuses the 32
verified B1977 files. The first stage stopped at its free-space guard
before copying any files. The retry follows a verified lossless
compaction of a completed image. The workers join, the RAM-backed
fixtures are removed and the persistent archive verifies.

[Timing summary](summary.json) · [Worker identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
