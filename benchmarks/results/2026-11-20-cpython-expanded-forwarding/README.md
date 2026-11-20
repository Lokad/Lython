# B1180 expanded forwarding after record-layout reuse

Measured `c3684d4d1` with the source-identical frozen `289380214` worker
image against stock CPython 3.13.16. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 20, 2026 UTC. Both workloads use the
same sources, files and goldens as B1173.

| Workload | Boundary | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| Expanded forwarding, 1,024 records / 26,112 B | Warm | 1.533–1.543 ms | 1.173–1.189 ms | 1.29–1.32× |
| Expanded forwarding | Compile-to-result | 1.611–1.638 ms | 1.318 ms | 1.22–1.24× |
| Expanded forwarding | Compilation | 0.04385–0.04448 ms | 0.06131–0.06261 ms | 0.70–0.73× |
| CSV invoice, 4,096 records / 127,031 B | Warm | 6.685–6.790 ms | 6.285–6.302 ms | 1.06–1.08× |

Ratios cross both independent Lython worker medians with both CPython
worker medians. Above one means Lython takes longer. The warm forwarding
controls differ by 0.62% for Lython and 1.38% for CPython. Its warm
within-worker IQR reaches 4.03% of the median. This is a workload-specific
diagnostic. Use the B1179 source pair to attribute the Lython change.

Warm includes mediated RAM-backed file I/O, golden checks and prior-result
disposal. Compile-to-result adds compilation and disposal of its owner.
Compilation alone includes compiled-owner disposal. Fixture reset, startup
and final teardown are outside the guest clock.

Both machine gates and all 80 timed batches passed. The first launch
stopped before timing because the collection manifest lacked auxiliary
image metadata. Its empty fixture mount was released under the idle lease.
The retry completed all batches and joined users, then exited at the
archive's disk-floor check. The original archive was verified under the
idle lease against both machine checks, the worker image, the catalog and
the stored observations. Both failures are preserved with the procedures.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Archive verification](archive-recovery.json) · [Procedures](procedures.tar.xz) ·
[B1179 source pair](../2026-11-20-dynamic-record-layout/README.md) ·
[Earlier B1173](../2026-11-20-cpython-reports-validator/README.md).
