# B1095 integer-prefix timing confirmation

This confirmation reused the exact B1094 source pair and verified worker
images (`06b40a712` and `954f2acfd`) on the same quiet Azure F4as_v7 VM with
.NET 11 RC3 and Ubuntu 24.04.4. Each process accumulated 12 seconds of
warmup instead of 8. The ordinary warm worker launch order was reversed.
The [original comparison](../README.md) preserves its adverse worker, all
checks and the first setup failure.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary rule closures | Warm | −3.14…+2.98% | −2.55…+2.62% |
| Annotated rule closures | Warm | −3.66…+3.12% | −5.47…+3.37% |
| Captured rule closures | Compile-to-result | −1.48…+1.66% | −2.09…+4.74% |
| Ordinary rule closures | Compile-to-result | −3.38…+2.84% | −2.48…0.00% |
| Expanded forwarding control | Warm | −0.00…+1.39% | −1.52…+1.23% |

The large ordinary warm divergence did not recur. Candidate medians still
spread 3.47% for ordinary warm and 5.10% for annotated warm. The original
annotated warm gain did not repeat. Three of four captured compile-to-result
pairs still consume more process CPU, and the elapsed ranges span zero.
Keep the prototype private. The simplification saves work at partial chains,
but current representative timing and unchanged allocation give no delivery
benefit to offset the remaining CPU uncertainty. Revisit with a workload
that demonstrates material machine-chain replay or with a larger dispatch
redesign that removes additional work.

All 100 golden sample batches, quiet gates, worker joins and persistent
archive verification passed. The same command-v2 execution and cleanup
boundaries apply. [Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
