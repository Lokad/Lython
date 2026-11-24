# B1651 Combine operand forwarding and layout

This prototype removes the full shape scan after operand forwarding. During
forwarding, each retained instruction receives its physical operands, result
slot and live extent. A forwarded load pushes one provisional operand that
its consumer removes. The admitted peak counts the depth after this removal.
Final retargeting still uses the completed block offsets. Unexecuted suffixes
retain their original form, including the structural jump after an assertion.

The reference is `4ae7dcc38`. The prototype's frozen image uses that exact
source plus the recorded compiler patch. Both 32-file images match three
complete benchmark goldens and four boundary programs under CPython 3.13.2.
Across 30 sources, 168 code objects and 7,031 instructions have identical
encoded operands, source spans, call sites, exception regions, and storage
capacities. Compilation allocation, retained admission and execution peaks
match in all three timed cases. Warm allocation matches in both generated
scripts. The manifest allocation samples vary by fewer than 200 B per run.

| Workload | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated classifier | Compilation | −2.61 to +0.55% | −1.93 to +0.98% | 2.16 / 1.06% |
| Sparse generated validator | Compilation | −1.37 to +0.47% | +0.48 to +1.65% | 0.50 / 1.36% |
| Configuration manifest | Compile to result | −0.30 to +0.67% | +0.70 to +1.05% | 0.65 / 0.33% |
| Generated classifier | Compile to result | −3.04 to −1.97% | −4.86 to −1.02% | 0.31 / 0.80% |
| Configuration manifest | Compilation | −0.49 to +1.59% | −2.22 to +2.63% | 0.22 / 1.87% |
| Sparse generated validator | Compile to result | −0.11 to +2.01% | −1.10 to +3.35% | 1.71 / 0.41% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Ranges span all four pairings of worker medians from seven
rotated batches per worker after warmup. Compilation includes compiled-owner
disposal. Compile to result adds execution, output verification, prior-result
disposal and applicable mediated RAM I/O. Process CPU also includes request
framing and fixture reset. Startup is outside these clocks.

The classifier reaches its result sooner with lower CPU. Compilation timing
crosses zero for all three cases. The sparse compilation and manifest
through-result lanes show small adverse CPU ranges. The refinement improves
manifest CPU and slows the classifier. Both forms remain experimental. The
next source gate is terminal-block cleanup, which could remove the need to
carry dead suffixes during layout. [Refinement](refined/README.md).

Windows and Ubuntu each pass 5,284 focused .NET 11 and 127 .NET 10 Release
checks. Both quiet gates, all 168 batches, worker joins, fixture removal and
archive verification pass. The procedures include the initial build failure
from overlapping local names.

[Images](identity.json) · [Compile allocation](compile-allocation-summary.json) ·
[Execution allocation](allocation-summary.json) · [Timings](quiet-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Source patch, tape comparison, tests and procedures](procedures.tar.xz).
