# B1092 keyword copy loop selection

The private `9a67c4b2a` prototype moves the choice between snapshotting
and transferring a dictionary out of the entry loop. Both loops use the
same key validation, error order, cancellation and ownership checks.
The reference `06b40a712` already transfers private inline dictionaries.

| Workload | Guest elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Named forwarding, warm | +0.61 to +1.98% | +0.41 to +1.23% | 0.40 / 0.96% |
| Named forwarding, compile-to-result | −0.66 to +1.27% | −1.11 to +0.87% | 0.23 / 1.71% |
| Inline dynamic keys, warm | −1.23 to −0.43% | −0.39 to +1.61% | 0.61 / 0.20% |
| Inline literal keys, warm | −1.90 to +1.49% | −0.65 to +3.45% | 1.18 / 2.25% |
| Mutable bound-method forwarding, compile-to-result | +1.72 to +2.34% | +0.27 to +3.03% | 0.50 / 0.12% |
| CSV invoice, warm | −2.88 to +2.33% | −2.67 to +1.37% | 2.18 / 3.11% |

Keep this prototype private. Named warm and mutable-bound compile-to-result
are adverse with supporting process CPU. Inline dynamic-key CPU spans
zero. Allocation and accounted peak effectively match, and every local
owner returned to baseline. Leave the original B1090 regression open for a
design that removes a larger copy stage with the same alias semantics.

Windows .NET 11 and Ubuntu .NET 10 each passed 397 focused Release cases,
including reentrant snapshot admission, terminal refusal and suspended
inline dictionary mutation. Both full 32-file images matched the five
CPython catalog goldens.

The pair ran on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3
on 2026-11-19 UTC. Ranges span all four combinations of process medians.
All 120 timed batches passed, workers joined, guest fixtures were removed
and the archive verified under the lease. Warm includes mediated RAM I/O,
goldens and previous-result cleanup. Compile-to-result adds compilation
and its owner disposal. Process CPU covers the wider request.

[Elapsed, CPU and allocation](summary.json) · [Raw observations](observations.tgz) ·
[Exact sources, checks and procedures](checks-and-procedures.tar.xz) ·
[Original transfer](../2026-11-19-private-keyword-mapping/README.md).
