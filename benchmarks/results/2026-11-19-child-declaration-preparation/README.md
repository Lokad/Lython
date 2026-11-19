# B1097 consolidated declaration preparation

Private R667 (`1dc7a95b3`) removes the separate function inventory and
later child-capture scan. It prepares closure locations and lexical ownership
flags together using the parent's existing slot map for identity. The exact
reference is `482b45a89`. The candidate passed 1,073 focused Windows .NET 11
and Ubuntu .NET 10 Release checks. The two complete worker images matched
six CPython workload goldens. Local compilation allocation falls 3.44% for
ordinary, 3.41% for annotated and 4.02% for captured rules. The borrowed
compile owners return to baseline.

A separate scope probe found a correctness regression. When a lambda
precedes a named function, the candidate reverses their transitively
forwarded names in `locals()`. The reference and CPython both produce
`(('read_b', 'read_a', 'a', 'b'), 1, 2)` and the candidate produces
`(('read_b', 'read_a', 'b', 'a'), 1, 2)`. The regression test at `5778e3a65`
passes through `Run` and `RunAsync` on both targets. The allocation and
timing observations below apply to the private version with this regression.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026. Each range covers all combinations of two independent process
medians per image. Compilation includes compiled-owner disposal.
Compile-to-result includes execution, mediated I/O, goldens and result
cleanup. Warm excludes compilation. Process CPU has a wider boundary.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rules | Compilation | −5.03…−3.51% | −5.83…−3.65% |
| Annotated rules | Compilation | −4.16…−3.08% | −4.54…−1.94% |
| Captured rules | Compilation | −3.87…−3.44% | −5.20…−3.80% |
| Manifest | Compilation | −2.21…−0.64% | −2.37…+0.51% |
| Generated validator | Compilation | −1.91…+0.68% | −1.46…+1.02% |
| Ordinary rules | Compile-to-result | −5.31…+1.87% | −3.99…+0.94% |
| Annotated rules | Compile-to-result | +0.24…+3.51% | +1.43…+2.63% |
| Captured rules | Compile-to-result | −4.62…−1.70% | −5.20…+1.31% |
| Shipment report | Warm | −0.50…−0.05% | −1.90…+1.17% |

Keep R667 private. The compiler-only gains justify testing an order-preserving
revision. Annotated compile-to-result remains a control for any such revision.
The collection completed 180 verified batches in 410.71 seconds. Both quiet
gates, worker joins, fixture cleanup and persistent archive verification passed.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks, scope probe and procedures](checks-and-procedures.tar.xz) ·
[Lease receipt](archive-receipt.json).
