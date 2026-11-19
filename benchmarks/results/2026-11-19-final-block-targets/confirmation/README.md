# B1088 branch-target compilation confirmation

The identical B1086 reference and candidate images ran with 18 seconds of
full-request warmup per process. Two independent validator blocks reversed
the starting worker order. A manifest block repeated the original workload.
Five measured rounds per block retained both same-source controls.

| Compilation workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Generated validator, first block | −1.15 to +0.52% | −1.01 to +1.01% | 1.32 / 0.36% |
| Generated validator, reverse order | −2.01 to +1.15% | +0.02 to +2.61% | 0.96 / 2.25% |
| Configuration manifest | −0.02 to +2.56% | −1.22 to +1.96% | 0.83 / 1.74% |

The 26.22% validator candidate split from B1086 did not recur in either
block. The original manifest improvement also did not repeat. Validator
elapsed spans zero in both blocks and the reverse-order CPU result is
adverse. The original split remains unexplained.

Retain the compiler simplification as `c3873194c`. It removes a complete
instruction rewrite pass while producing the same instruction and protected
region data in six probes. Compilation allocations match in the original
pair. The mixed timing leaves a bounded possible compile-only cost for
these workloads. Treat it as part of the next frontend delivery comparison.

Both complete images and the catalog hashes match B1086. All 60 measured
batches passed in 261.52 seconds on the same quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Workers joined, fixtures were removed and the
persistent archive verified under the lease.

[Original B1086](../README.md) · [Elapsed and CPU results](summary.json) ·
[Raw observations](observations.tgz) · [Procedures](checks-and-procedures.tar.xz).
