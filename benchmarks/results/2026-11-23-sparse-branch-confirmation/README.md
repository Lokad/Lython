# B1444 Sparse validator confirmation

Repeated the exact B1442 `e8fd8b73f` and `2e5e316c1` images on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November
23, 2026 UTC. The second image matches retained `30d956735`.
Worker ordering was reversed. The catalog, inputs, output checks,
eight-second warmup and seven rotated batches were reused.

| Workload | Boundary | Elapsed change | Wider process CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Sparse generated validator | Compile to result | −0.50 to +0.54% | −2.00 to +1.29% | 0.62 / 0.42% |
| Small configuration edit | Compile to result | −1.36 to +0.82% | −1.24 to +1.14% | 1.60 / 0.61% |
| Sparse generated validator | Compile | −1.06 to +3.01% | −1.52 to +2.08% | 1.44 / 2.63% |

Ranges span the four pairings of two process medians per source.
Compilation includes code-owner cleanup. Compile to result adds one
execution, applicable mediated RAM-backed I/O, output and file checks,
and disposal of the previous result. Wider process CPU includes
request processing and resets. Startup and final teardown are separate.

The sparse first-result slowdown at one reference worker in B1442 did
not recur. The new range spans zero with 0.62% reference-worker spread,
down from 5.94%. The two compilation comparisons also span zero. The
compiler still copies more sparse value entries, and the original
adverse pairing and higher first-result elapsed in B1443 remain recorded.
There is no demonstrated reason to change the retained snapshot design.

All 84 timed batches verify within 166 seconds. Both quiet gates pass,
workers join, guest fixtures are removed and the archive verifies under
the lease. Source and semantic qualification are unchanged from B1442.

[Timings](summary.json) · [Source and images](source.json) ·
[Observations](observations.tgz) · [Procedures](procedures.tar.xz) ·
[B1442](../2026-11-23-abstract-state-snapshots/README.md) ·
[B1443](../2026-11-23-branch-cpython-checkpoint/README.md).
