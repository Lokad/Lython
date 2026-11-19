# B1104 one map for abstract branch facts

R672 stores a name's abstract value and length bounds together. Branch
clones and joins carry one map. Each fact can still exist independently.
Cached expression resolutions, callback invalidation and cleanup keep
their prior lifetimes. The single entry is wider.

The private candidate is `414b5f150`. The reference image `bc65698dd`
has the production and test sources of `80cbf4988`. Windows .NET 11 and
Ubuntu .NET 10 each passed 370 focused Release checks, including the join
of independently present facts. The two complete worker images passed
five CPython goldens. Local compilation allocation was effectively flat
for the validator. It rose 0.86% for the manifest and fell 0.80% for
annotated rules. Borrowed owners returned to baseline.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026. Ranges span all four combinations of two independent worker medians
per image. Compilation includes compiled-owner disposal. Warm includes
mediated I/O, goldens and previous-result cleanup. Compile-to-result
combines both. Process CPU covers the wider request.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generated validator | Compilation | +0.31…+1.47% | −0.50…+3.06% |
| Configuration manifest | Compilation | −2.20…−0.08% | −4.19…−0.89% |
| Annotated rules | Compilation | −0.03…+2.59% | +0.20…+2.31% |
| Generated validator | Compile-to-result | −0.65…+1.23% | −1.08…+1.62% |
| Configuration manifest | Compile-to-result | +0.40…+1.52% | −2.42…+2.32% |
| Shipment report control | Warm | −0.48…+0.74% | −1.16…0.00% |

Keep the prototype private. Manifest compilation improves with lower CPU.
Its compile-to-result elapsed rises. Validator compilation was the main
target and slows with effectively flat allocation. The annotated compilation
control also uses more CPU.

All 120 timed batches completed in 325.00 seconds. The quiet gates passed,
workers joined, guest fixtures were removed and the observation archive
was verified under the VM lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
