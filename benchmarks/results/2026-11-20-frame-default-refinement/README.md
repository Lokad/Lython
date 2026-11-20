# B1165 frame default cleanup refinement

Compared `a5214a158` with private `cf44cfc31`. The refinement restores
the native prefix's existing retirement and removes an unreachable
default-result branch there. Owned default slot cleanup remains in the
ordinary frame, which receives control before default construction.

| Workload, warm | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Ordinary rule closures | −0.80…+1.43% | −2.14…+1.31% |
| Eager annotated rules | −0.34…+5.14% | −0.24…+4.68% |
| Computed defaults | −0.99…+1.93% | −0.26…+3.51% |
| Positional CSV invoice | −2.66…+0.53% | −2.62…+1.32% |

Two processes per revision ran five rotated batches on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 20, 2026 UTC.
Ranges use all duplicate-process median pairings. Warm elapsed includes
mediated I/O, goldens and prior-result disposal. Process CPU covers the
wider batch. Eager reference medians differ by 3.88%. Both 32-file images
matched four CPython goldens and returned the allowance to baseline.
Ordinary and eager rule allocations fall 11.22% and 9.23%, respectively.
The ordinary peak matches and the eager peak increases by 24 bytes.

The frame-slot design stays private. Ordinary warm CPU now spans zero.
Eager and computed defaults still show adverse CPU pairings, and the
short comparison establishes no whole-script warm gain. The original
[B1164 comparison](../2026-11-20-frame-default-storage/README.md) preserves
the initial ordinary warm CPU increase and compile-to-result improvement.
`0f2cb5e8c` removes the unreachable branch independently in production.
That deletion passed 212 focused checks per platform. Its performance
was outside this comparison.

Windows .NET 11 and Ubuntu .NET 10 each passed 131 focused Release checks
on the refinement, following the broader B1164 checks. Both quiet gates
and all 80 timed batches passed. Workers joined and the persistent archive
verified under the lease after guest fixture cleanup.

[Summary](summary.json) · [Observations](observations.tgz) · [Archive receipt](archive-receipt.json) ·
[Source and checks](checks-and-procedures.tar.xz) · [Measured candidate core](measured-core.dll.gz) ·
[Measured reference core](../2026-11-20-owned-annotation-values/measured-core.dll.gz).
