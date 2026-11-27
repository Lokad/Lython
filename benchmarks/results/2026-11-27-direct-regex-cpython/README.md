# B1860 CPython comparison after direct regex ranges

Lython `7305efabf` uses the optional signed PythonRe 0.5.0 adapter. It
runs against stock CPython 3.13.16 on November 27, 2026 UTC. The comparison
host is `05c1059de`. The Azure F4as_v7 runs Ubuntu 24.04.4 with .NET 11 RC3.
All 32 frozen worker files are verified against the B1859 candidate.

Both scripts search a 3,072-line, 86,954-byte fixture and consume one
capture per match. The compiled-pattern script saves a pattern and the
module script calls `re.search`. Both engines have two warmed workers
with seven rotated batches apiece. The ranges use worker medians.
Lython / CPython divides their elapsed times, with values above 1×
indicating longer Lython execution.

| Warm workload | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Compiled-pattern log search | 2.88760–2.90887 ms | 0.99232–1.00082 ms | 2.885–2.931× |
| Module-level log search | 3.27088–3.30482 ms | 1.58144–1.58565 ms | 2.063–2.090× |

Warm elapsed covers applicable RAM-backed I/O, verification and disposal
of the previous result. Lython uses the mediated host with shared
accounting active. CPython uses its native file APIs. Wider process CPU
also covers fixture reset and request framing. Compilation and startup
are excluded. The prior compile-to-result rows keep their earlier
revision. [B1858](../2026-11-27-compiler-regex-cpython/README.md).

Both independent CPython output and file goldens pass. The repeat completes
56 measured batches in 82.71 seconds with both quietness gates passing.
Within-engine elapsed spread is 0.27–1.04%. The workers join, the guest
fixtures are removed and the archive verifies under the lease. These are
diagnostic external ratios. The matched source comparison isolates the
effects of the direct ranges. [B1859](../2026-11-27-signed-regex-small-match/README.md).

The first run completed both lanes and failed the post-run quietness check
at 7.54% busy CPU against a 5% gate. The repeat uses the original image,
catalog, clocks and gate. The first run is excluded from the table.
[Initial gate](initial/gate-failure.json) ·
[Initial observations](initial/observations.tar.xz) ·
[Initial terminal status](initial/terminal-status.json).

[Timing summary](summary.json) · [Fixture sizes](fixture-census.json) ·
[Image identity](identity.json) · [Qualified observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Verification and procedures](procedures.tar.xz).
