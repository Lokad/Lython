# B1211 Retest compact call sites with the prepared positional flag

Measures private `e887f7e4b` against production source `ed43ffe95` on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
2026-11-20 UTC. It uses the same reference image and workload catalog
as [B1210](../README.md). Two independent processes per revision warm
for at least eight seconds each. Seven rotated rounds target 0.8-second
batches. Ranges cross all four pairings of process medians.

| Workload | Boundary | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Configuration manifest | Compile | −0.44% to +0.45% | −2.00% to +1.51% | 0.12% / 0.77% |
| Eager annotated rules | Warm | −3.62% to +4.19% | −3.93% to +3.33% | 6.63% / 1.38% |
| Named shipment | Warm | +0.77% to +4.28% | 0.00% to +4.27% | 0.69% / 2.76% |

The change restores the existing prepared flag while retaining the
keyword suffix representation. The same four workloads allocate
1.19–1.95% less during local compilation. Accounted execution peaks
match, warm allocation is effectively flat and the borrowed allowance
returns to baseline. Phase boundaries match B1210.

Both variants stay private. The earlier manifest compile improvement
does not repeat. Eager rules vary across identical reference processes.
Named shipment elapsed moves adversely in both collections, and this
refinement does not establish a warm CPU gain. The original results,
including the compile-to-result lanes, remain available. A different
representation that preserves the keyword-bearing execution path could
justify a future comparison.

Windows .NET 11 and Ubuntu .NET 10 each passed 2,282 focused public
checks. Ubuntu also passed 1,438 white-box checks. Both source-matched
32-file images passed four workload goldens and seven CPython probes
in both execution modes. Both quiet gates and all 84 timed batches
passed. Workers joined, the persistent archive verified under the lease,
and the guest fixtures were removed.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and verification](source-and-checks.tar.xz).
