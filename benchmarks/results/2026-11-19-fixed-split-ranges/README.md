# B1083 fixed split range reuse

Private `c24fcbd75` tests scanning an exact builtin split once when its
result feeds fixed unpacking. The scanner writes byte ranges to the
private result operands and replaces them with admitted fragments.
Escaping or mismatched lists keep the ordinary materialization path.
The pair is `896e0c40a` versus the isolated prototype.

Pinned local allocation is effectively equal and accounted peaks match.
Windows .NET 11 and Ubuntu .NET 10 each passed 423 focused Release cases.
These include refusal before target callbacks, storage of retained split
results and cancellation. Both 32-file images match four CPython goldens
and restore their shared allowance.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover four pairs of duplicate process medians. Negative changes
favor the candidate.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Expanded forwarding | Warm | +0.50…+2.61% | +1.68…+2.11% |
| Expanded forwarding | Compile-to-result | +0.07…+4.90% | +0.21…+5.07% |
| Helper text normalization | Warm | −4.04…+0.73% | −3.71…+0.68% |
| Large positional calls | Warm | −0.31…+0.43% | −0.82…−0.41% |
| CSV invoice | Warm | +0.54…+1.79% | +0.73…+2.00% |

The prototype stays private. Removing the rescan adds a per-fragment
scratch write and read relative to the existing bulk count. All four
forwarding warm pairs use more CPU, with both same-source elapsed spreads
near 1%. The CSV movement remains in the evidence. These measurements
do not isolate the cost of individual scanner operations.

Both quiet gates and all 100 timed batches passed in 231.80 seconds.
Workers joined, fixtures were removed and the archive verified under
the lease. Warm includes mediated I/O, goldens and previous-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process
CPU measures a wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
