# B1985 Refresh the regex log comparison against CPython

Lython `a1a9ee4a4` and stock CPython 3.13.16 ran on November 28, 2026
UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. The frozen Lython
worker uses the exact `f231ce76a` core and signed PythonRe 0.5.0 adapter. Both
scripts read 3,072 log lines in 86,954 bytes and check their complete
output. Lython uses `Run` and the mediated file host. CPython uses
its native file APIs with `-I -S -X utf8`.

| Warm workload | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | ---: | ---: | ---: | ---: |
| Compiled-pattern search | 2.67503–2.80340 ms | 0.96782–1.02097 ms | 2.620–2.897× | 2.528–2.732× |
| Module-level search | 2.94343–3.14617 ms | 1.58999–1.61443 ms | 1.823–1.979× | 1.810–1.922× |

Ratios divide Lython by CPython and span pairings of medians from two
workers per engine. Both engines warm for at least five seconds and
contribute seven rotated batches per worker. Elapsed includes mediated
RAM-backed I/O, checking results and files, and prior-result disposal.
Process CPU also includes fixture reset and framing. Both clocks
exclude compilation and launch. Module-level Lython elapsed has 6.89%
worker spread. Compiled-search CPython elapsed has 5.49% spread. The
results establish the remaining gaps with uncertainty in their size.

Both goldens pass locally and on the VM. All 56 timed batches and both
quiet gates verify. Staging reuses the 32 verified files from B1980.
The workers join, RAM fixtures are removed and the persistent archive
verifies. The earlier external comparisons keep their measured
revisions. [B1932](../2026-11-28-current-cpython-regex-checkpoint/README.md) ·
[Current managed profile](../2026-11-28-current-regex-profile/README.md).

[Timing summary](summary.json) · [Worker identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
