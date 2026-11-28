# B1968 Compare short scripts with CPython after frontend consolidation

Lython `83042273c` and stock CPython 3.13.16 ran November 28, 2026 UTC
on an Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Each range
spans pairings of medians from two workers per engine. Ratios divide
Lython by CPython.

| Workload | Boundary | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| First-line file search, 42,010 B input | Warm | 0.06511–0.06544 ms | 0.05099–0.05152 ms | 1.264–1.283× | 0.996–1.006× |
| First-line file search, 319 B source | Compile to result | 0.09550–0.09688 ms | 0.12091–0.12109 ms | 0.789–0.801× | 0.775–0.780× |
| Configuration edit, 357 B source and 181 B input | Compile to result | 0.09387–0.09461 ms | 0.12365–0.12450 ms | 0.754–0.765× | 0.709–0.738× |
| Generated 48-helper report, 9,559 B source | Compile only | 0.61425–0.61560 ms | 0.93274–0.93825 ms | 0.655–0.660× | 0.663–0.666× |

Warm elapsed includes RAM-backed file I/O, verification and previous
result disposal. Compile to result adds compilation and compiled-owner
disposal. Compile only uses resident source and includes retiring the
previous compiled object. Process CPU also covers framing and fixture
reset where applicable. All lanes use prewarmed processes. Warm file
search elapsed remains higher for Lython, while its wider CPU ratio
spans parity. The external comparison measures the combined revision.

Both quiet checks and all 112 batches verify. The exact 32-file image
matches three independent CPython output and file goldens locally and
on the VM. All workers join and the RAM-backed fixtures are removed.
The persistent archive verifies before the lease ends. Staging reuses
31 verified donor files. Two completed images were compressed after
verifying their archives and exact decompressed core hashes. Keep the
first staging guard rejection in the procedure history.

[Timings](summary.json) · [Worker identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
