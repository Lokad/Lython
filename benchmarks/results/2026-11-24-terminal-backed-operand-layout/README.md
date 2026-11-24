# B1653 Assign operand slots during forwarding

The terminal-block invariant in `132d066c1` allows forwarding and operand
layout to share one traversal of a reachable block. Plain loads receive
provisional positions, and the next consumer accounts for the loads retained
in the tape. The compiler writes each consumer's final slots together. This
replaces the shape scan over compacted instructions and removes the suffix
state used in [B1651–B1652](../2026-11-24-single-pass-operand-layout/README.md).
Final retargeting uses completed block offsets.

The reference is `132d066c1`. The candidate's frozen source differs by the
recorded compiler patch. Both 32-file images match three complete benchmark
goldens and four untimed boundary programs under CPython 3.13.2. Across 31
sources, all 170 code objects and 7,084 instructions retain identical slot
encodings, call sites, source spans, exception regions and storage bounds.
Compilation and warm execution allocations, retained compilation admission
and peak execution admission match in all three workloads.

| Workload | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated classifier | Compilation | −5.30 to −2.34% | −4.44 to −1.21% | 0.81 / 2.29% |
| Sparse generated validator | Compile to result | −3.30 to −2.95% | −3.76 to +0.25% | 0.26 / 0.11% |
| Configuration manifest | Compile to result | −1.71 to −0.81% | −2.11 to −0.33% | 0.53 / 0.38% |
| Generated classifier | Compile to result | −2.67 to −0.13% | −4.72 to +1.56% | 0.13 / 2.47% |
| Configuration manifest | Compilation | −4.68 to −3.12% | −5.06 to −3.63% | 0.05 / 1.59% |
| Sparse generated validator | Compilation | −3.22 to −2.04% | −5.16 to −1.01% | 0.06 / 1.16% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Ranges include all four pairings of worker medians from seven
rotated batches per worker after five seconds of warmup. Compilation includes
compiled-owner disposal. Compile to result adds execution, output verification,
previous-result disposal and applicable mediated RAM I/O. Process CPU also
includes fixture reset and request framing. Startup is outside these clocks.

Compilation elapsed and wider CPU fall in all three workloads. Compile-to-result
elapsed falls in all three. Through-result CPU crosses zero for the classifier
and validator. The classifier also has 2.47% candidate-worker elapsed spread at
that boundary. Windows and Ubuntu each pass 2,653 focused .NET 11 and 128 .NET 10
Release checks. An additional 44 Windows Debug checks exercise the terminal-block
assertion. Both quiet gates and all 168 batches pass. Workers join and the fixture
mount is removed before verified archival.

The VM stage retired two closed binary replicas after byte-for-byte validation
against the retained Windows images. It also retired a manifest profile copy
that matches its verified archive. The procedures contain the custody receipt,
both patches and the initial tape driver's stale-reference failure.

[Images](identity.json) · [Compile allocation](compile-allocation-summary.json) ·
[Execution allocation](allocation-summary.json) · [Timings](quiet-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Source patches, tape comparison, tests and procedures](procedures.tar.xz).
