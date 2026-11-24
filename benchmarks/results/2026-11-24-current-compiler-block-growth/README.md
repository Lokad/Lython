# Current compiler instruction storage attribution

The frozen `4eb8c9559` core matches production `5313f9e71`. All 32 files
in the measured worker image match B1591. The existing worker compiled
and disposed fresh code owners on Windows and .NET 11 RC3 on November 24,
2026 UTC. Three CPython 3.13.2 goldens pass before profiling. Each worker
warms for about four seconds and records six seconds of EventPipe sampled
thread time. Counts filter to compiler ancestry and can overlap. Guest
execution stays outside the captured batches.

| Complete source | Compiler thread time | Instruction-list growth ancestry | Emitted / recopied slots | Reopened blocks |
| --- | ---: | ---: | ---: | ---: |
| Configuration manifest | 5,863 ms | 511 ms (8.7%) | 294 / 264 | 0 |
| Sparse generated validator | 5,967 ms | 1,030 ms (17.3%) | 3,900 / 3,884 | 192 |
| Postponed annotated factories | 5,175 ms | 880 ms (17.0%) | 60 / 60 | 0 |

The storage columns come from one additional compile per source with a
separate diagnostic core. Its output matches the reference executable
instructions, regions, source metadata and storage layout. Copy counts sum
the old capacities at actual list expansions. They include only instruction
emission. The 192 sparse reopens are equally divided between blocks with
four and eight existing instructions. Its lists allocate 487 first arrays,
389 expansions from four and 291 expansions from eight. The two smaller
scripts have no reopens and still copy roughly one slot for each emitted slot.

The evidence supports replacing the growing per-block backing. B1333
already tested a single growing array with relocated ranges. That trial
increased admitted peak and slowed the complete sparse validator. A design
should avoid global replacement overlap, reduce reopening copies and keep
the existing final tape. First test equivalence and compare temporary and
published storage on the sparse guard, both smaller scripts and a dead-heavy
control. Current samples support a hypothesis. They do not estimate a speedup.

All 1,545 trace batches completed. Workers closed and files match their
recorded hashes. The archive includes the instrumented source change,
worker identity, verified raw traces and paired tape inspections. No new
elapsed or external CPU comparison was collected.

[Attribution](attribution.json) · [Growth counts](block-growth.json) ·
[Capture summary](run-summary.json) · [Observations and procedures](observations.tar.xz) ·
[Profiled image](../2026-11-24-decoded-closed-defaults/identity.json) ·
[Packed-buffer comparison](../2026-11-22-packed-block-layout/README.md).
