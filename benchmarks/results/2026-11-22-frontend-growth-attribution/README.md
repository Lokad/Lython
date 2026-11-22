# B1386 Compilation storage attribution on Windows

Profiled the frozen `d45f2ed21` core, with the retained Unicode decimal
change from `f17ba1c9f`, on Windows and .NET 11 RC3 on November 22,
2026 UTC. The exact 32-file image and three archived script goldens
verified with the unchanged benchmark worker and CPython 3.13.2.

Each worker warmed for roughly four seconds, then collected six seconds
of Windows EventPipe sampled thread time. Each request compiles and
disposes a fresh code owner. Guest execution stays outside the captured
batches. The table filters converted stacks to compiler ancestry,
which excludes most idle and protocol time. The counts describe sampled
thread time and overlapping ancestry. They cannot establish inclusive
CPU share on the quiet VM or predict the result of a code change.

| Complete source | Compiler intervals | Attributed thread time | Instruction list growth | Name-binding set copies |
| --- | ---: | ---: | ---: | ---: |
| Configuration manifest | 4,481 | 5,862 ms | 472 ms | 191 ms |
| Sparse generated validator | 3,281 | 5,991 ms | 840 ms | 515 ms |
| Postponed nested factories | 4,039 | 5,560 ms | 918 ms | 0 ms observed |

Instruction list growth appears in all three captures. Existing code
grows a list of instructions for each block and copies its reachable
instructions to a final tape after operand forwarding and layout.
Investigate eliminating growing per-block backing as an independent
construction stage. Compare admitted peak, including temporary and
final overlap, before promoting a new layout. The prior packed-block
and finalization variants remain relevant controls. The name-binding
copies appear mainly in branch and comprehension analysis. Preserve
branch isolation and deletion behavior before pursuing that boundary.
[Quiet compilation attribution](../2026-11-20-current-cold-compiler-attribution/README.md) ·
[Finalization experiment](../2026-11-22-fused-executable-finalization/README.md).

All 1,644 compile batches submitted during the attached sessions
completed. All workers closed and both verification passes per case
matched the catalog. The initial collector used a temporary directory
name the worker correctly refused. The successful run used the existing
supervisor naming convention. The archive includes that failure, the raw
traces and the exact procedures. No new timing ratios were collected.

[Attribution](attribution.json) · [Capture summary](run-summary.json) ·
[Raw traces and procedures](captures.tar.xz).
