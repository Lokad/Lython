# B1730c Computed scalar arguments with existing update fusion

This refinement declines the direct argument path when the call is followed
by an augmented assignment or reaches the end of its block. Both drivers
keep their existing call-plus-update path. Each generated factory still
marks 160 instructions. A completed exact call bypasses publication of the
target and argument and publishes its result through the existing consumer.

| Case | Lane | Elapsed difference | Process CPU difference | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Complete ordinary definitions | Warm sync | −15.30% to −10.63% | −16.83% to −11.33% | 0.41% / 5.08% |
| Complete ordinary definitions | Warm async | −17.76% to −13.91% | −17.75% to −14.86% | 2.94% / 1.69% |
| Complete ordinary definitions | Compilation only | +0.20% to +0.78% | +0.84% to +1.32% | 0.13% / 0.45% |
| Complete ordinary definitions | Compile-to-result | −6.62% to −4.68% | −9.46% to −5.71% | 1.73% / 0.34% |
| Default effects diagnostic | Warm sync | −21.55% to −18.23% | −20.78% to −19.01% | 2.41% / 1.77% |
| Escaping defaulted rules | Warm sync | −3.17% to +0.02% | −1.96% to +1.38% | 2.98% / 0.30% |

On November 25, 2026 UTC, the quiet Azure F4as_v7 used Ubuntu 24.04.4 and
.NET 11 RC3. Reference source is `857cc8001`. The candidate identity
records the exact patch and both verified 32-file images. Each workload and
phase uses two processes per source, seven seconds of warmup per process and
seven rotated batches per process. The ranges span all four cross-source
process-median pairings. Negative percentages mean the candidate uses less
time. The elapsed lanes separate warm execution, compilation and one
compile-and-run. Execution includes verification and result cleanup.
Compilation includes compiled-owner disposal. Process CPU covers the wider
request with framing. There is no timed guest file I/O for these scripts.

The warm gains recur in both hosting modes. Compilation costs increase on
the measured complete script and the combined clock still falls. The
escaping-rule control spans zero in both comparisons. Local allocation and
admitted capacity match for all three scripts, including allocation during
compilation and the compiled owner's retained admission. Windows .NET 11
and Ubuntu .NET 10 each pass 282 focused Release checks. Both frozen images
pass all three independent output goldens. All VM workers join and the
evidence is verified under the lease.

[First block](../README.md) · [Identity](identity.json) ·
[Shape inspection](shape-summary.json) · [Execution allocation](allocation-summary.json) ·
[Compilation allocation](compile-allocation-summary.json) ·
[Timing summary](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
