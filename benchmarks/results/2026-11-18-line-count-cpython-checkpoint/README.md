# Helper text and normalization against CPython

B984 measured `4ff69a9af` / frozen Lython `74d19a10` on 2026-11-18 UTC.
The comparison uses .NET 11 RC3 and stock CPython 3.13.16 with PGO/LTO,
GIL enabled and no JIT. The quiet Azure F4as_v7 VM runs Ubuntu 24.04.4
with four EPYC 9V45 vCPUs and 16 GiB RAM. Both scripts process 52,224 bytes
and 4,096 lines. Both engines matched existing stdout and file goldens.

| Workload | Phase | Lython ms | CPython ms | Lython / CPython | Control spread L / C | Max IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Helper list | Compilation | 0.04561–0.04588 | 0.04372–0.04377 | 1.04–1.05× | 0.58% / 0.11% | 1.19% |
| Helper list | Warm | 1.052–1.064 | 0.880–0.900 | 1.17–1.21× | 1.20% / 2.30% | 0.86% |
| Helper list | Compile-to-result | 1.165–1.180 | 0.816–0.825 | 1.41–1.45× | 1.29% / 1.04% | 4.44% |
| Normalization | Warm | 0.377–0.382 | 0.676–0.679 | 0.56–0.57× | 1.36% / 0.53% | 1.19% |

Ratios divide Lython elapsed by CPython elapsed. Ranges cover all pairings
of two process medians per engine. Each worker warmed for eight seconds
of complete requests and ran five rotated measured batches. Compilation
includes compiled-owner disposal. Warm includes mediated RAM-backed I/O,
output checking and prior-result cleanup. Compile-to-result adds compilation
and disposal of its compiled owner. Startup, fixture reset, protocol and
final cache teardown are outside the elapsed clock. Process CPU is wider.
All four jobs and quiet checks completed in 184.53 seconds. Workers joined,
fixtures were removed and archived observations passed SHA-256 verification.
No working replicas were retired for this collection.

The CPython helper compile-to-result clock is below its separately warmed
execution clock. Both are directly observed phase medians. Their difference
cannot isolate compile or cleanup cost. This inversion also occurred in an
older collection and did not recur in its same-process confirmation. The
current magnitude is uncertain. CPython normalization elapsed again moved
between collections, from 0.733–0.737 ms in B982 to 0.676–0.679 ms here.
B983 establishes the Lython source change with fixed-revision controls.
[Earlier helper phase investigation](../2026-11-17-helper-phase-attribution/README.md) ·
[B982](../2026-11-18-normalization-cpython-checkpoint/README.md) ·
[B983](../2026-11-18-private-line-count/README.md)

[Timings](summary.json) · [Frozen identity](identity.json) ·
[Raw observations](observations.tgz) · [Procedures](procedures.tar.xz)
