# B1195 Shared compiler buffers for executable class suites

Measures `2dad15d55` against `cf059b2a2` on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Both roles use two
independent processes with at least 10 seconds of warmup per process.
Five rotated rounds target 0.5-second requests. The table spans four
pairings of process medians. Negative values indicate less time in the
candidate.

| Workload | Phase | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Inherited dataclass configuration | Compilation | +9.86% to +17.09% | +9.99% to +19.06% | 0.65% / 5.89% |
| Inherited dataclass configuration | Compile to result | +5.53% to +10.26% | +4.70% to +11.44% | 0.98% / 3.48% |
| Inherited dataclass configuration | Warm | +0.88% to +1.75% | +1.56% to +2.32% | 0.52% / 0.34% |
| Configuration manifest | Compile to result | −1.15% to −0.43% | −1.42% to +1.06% | 0.00% / 0.73% |

Dataclass medians span 0.420–0.424 ms before and 0.447–0.463 ms after
from compile to result. Compilation includes disposal of the compiled owner.
Compile to result adds execution, mediated I/O, output checks and
prior-result cleanup. Warm includes execution and the same result boundary.
Process CPU covers the wider request loop. This comparison measures two
Lython revisions. The CPython runs here verified semantics and goldens.

Sibling scopes now reuse name, constant and entry-block buffers within
one admitted compilation. Capture maps and final arrays remain code-owned.
The class instruction owns its metadata directly. Dataclass compile
allocation falls from the initial prototype's 770,496 bytes to 688,720 bytes
and remains 8.03% above the 637,504-byte reference. Manifest, eager rules
and invoice compile allocation fall 5.98%, 1.46% and 1.28%. The validator is
effectively flat. Warm allocation stays flat except for a 0.27% increase on
dataclasses. All accounted execution peaks match, and cleanup restores the
allowance to baseline.

Retain the class engine consolidation for removing both lowered statement
interpreters and more than 2,100 net production lines. The dataclass compile
and compile-to-result regressions are delivery debt. Compared with B1194,
the refined cold ranges are lower, and the warm dataclass lane now leans
adverse on both clocks. Manifest compile-to-result CPU spans zero.
No general performance gain is inferred.

Windows .NET 11 and Ubuntu .NET 10 each passed 4,889 ordinary white-box and
1,369 focused public Release checks. Both 32-file images verified five
workload goldens and eight CPython semantic probes in both execution modes,
including class body cleanup and sibling closures. Both quiet gates and all
80 timed batches passed. Workers joined and the persistent archive verified
under the lease. The VM started with 267 MiB free after staging and kept
266 MiB after archiving.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz) ·
[Measured core](measured-core.dll.gz) · [Original result](../README.md).
