# Normalization against CPython

B982 measured `6f494109f` / frozen Lython `e4905aa5` on 2026-11-18 UTC.
The comparison uses .NET 11 RC3 and stock CPython 3.13.16 with PGO/LTO,
GIL enabled and no JIT. The quiet Azure F4as_v7 VM runs Ubuntu 24.04.4
with four EPYC 9V45 vCPUs and 16 GiB RAM. Both normalization scripts
process 52,224 bytes and 4,096 lines. Complete stdout and output files
match the existing goldens on both engines.

| Workload | Phase | Lython ms | CPython ms | Lython / CPython | Control spread L / C | Max IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Normalization | Compilation | 0.03568–0.03616 | 0.03765–0.03796 | 0.94–0.96× | 1.36% / 0.81% | 2.00% |
| Normalization | Warm | 0.4450–0.4542 | 0.7328–0.7368 | 0.60–0.62× | 2.06% / 0.55% | 1.35% |
| Normalization | Compile-to-result | 0.5100–0.5148 | 0.8451–0.8495 | 0.60–0.61× | 0.95% / 0.53% | 0.88% |
| Helper list | Warm | 1.1298–1.1534 | 0.8479–0.9024 | 1.25–1.36× | 2.09% / 6.42% | 1.77% |

Ratios divide Lython elapsed by CPython elapsed. Ranges cover all pairings
of two process medians per engine. Each worker warmed for eight seconds
of complete requests and ran five rotated measured batches. Compilation
includes compiled-owner disposal. Warm includes mediated RAM-backed I/O,
output checking and previous-result cleanup. Compile-to-result adds
compilation and disposal of its compiled owner. Startup, fixture reset,
protocol and final cache teardown stay outside elapsed. Process CPU is a
wider boundary. All four jobs and quiet checks completed in 184.36 seconds.
Workers joined, guest fixtures were removed and archived data was verified.
No working replicas were retired for staging.

CPython warm normalization previously measured 0.501–0.503 ms,
0.725–0.737 ms and 0.508–0.511 ms in separate collections. The cause
remains unresolved. The CPython helper-list control varies 6.42% inside
B982 and limits that ratio's precision. Use the fixed-revision B981 source
pair to attribute the Lython optimization. The B982 ratios describe only
this collection and give no estimate of a cross-collection speedup.

[Current timings](summary.json) · [Frozen identity](identity.json) ·
[Raw observations](observations.tgz) · [Procedures](procedures.tar.xz) ·
[Paired Lython comparison](../2026-11-18-trimmed-split-join/README.md) ·
[Earlier text control](../2026-11-18-csv-cpython-checkpoint/README.md)
