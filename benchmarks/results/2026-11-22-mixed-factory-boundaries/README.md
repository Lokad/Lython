# B1374 Mixed factory phase separation

Reused the two frozen B1372 images on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 22, 2026 UTC.
The images match production `880c6d958` and `e152d1ceb`. The
script creates 4,096 functions from 2,048 inputs and uses no guest
files. Each phase uses independent workers. Two workers per
revision warmed for at least eight seconds and ran seven rotated
timed batches each.

| Phase | Elapsed change | Wider CPU change | Spread old/new |
| --- | ---: | ---: | ---: |
| Compile to result | -2.26 to +7.32% | -2.19 to +7.78% | 0.46 / 9.29% |
| Compilation only | +0.87 to +1.45% | -1.44 to +1.56% | 0.01 / 0.57% |
| Warm execution | -0.69 to +2.79% | -1.11 to +4.65% | 3.33 / 0.18% |

Negative changes favor the retained runtime. Ranges span pairings
of process medians. Wider CPU includes worker-side request handling.
Warm includes running the compiled code, verifying output and
disposing the prior result. Compilation includes compiled-owner
disposal. Compile to result includes execution and its cleanup.
Startup stays outside these clocks.

The compilation elapsed increase is small and repeatable across
these workers. Its CPU range spans zero, as does warm execution.
The 9.29% same-image spread in compile to result leaves both
directions plausible. Keep the adverse B1371 and B1372 results.
The phase split leaves the cause open and provides no specific
call or allocation stage to change. Advance the independent
record and text work.

All 84 timed batches verified in 167
seconds. Both complete image manifests and the original catalog
matched. All workers joined and the persistent archive verified
under the lease. The VM reused immutable staged files.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Original source comparison](../2026-11-22-native-direct-integer-return-refined/README.md).
