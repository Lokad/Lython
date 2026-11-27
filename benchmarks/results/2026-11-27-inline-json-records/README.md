# B1908 Embed the starting values of shared JSON records

The dense fixture contains 2,048 two-field objects and 2,048 one-field
children. The trial embeds the four starting value slots in the dictionary
shell once the decoder knows a shared layout. Wider rows still rise to the
layout's final array capacity. Shape promotion admits private backing before
copying values and dropping the old representation. Every occupied slot
retains its existing conservative value charge.

The baseline is `8c05ed067`. Only the core DLL differs between the
verified 32-file worker images. Three independent CPython 3.13.16 goldens
and all 112 timed batches pass. The candidate passes 1,706 public and
332 white-box focused Release checks on Windows .NET 11.
CPython also verifies the copy, keyword forwarding, deletion, reinsertion
and clear regression.

| Complete workload | Managed allocation change | Peak admitted bytes change |
| --- | ---: | ---: |
| Dense nested JSON | −8.70% to −8.71% | −9.18% |
| Sparse nested JSON Lines | −8.32% to −8.33% | −8.63% |
| Quoted CSV invoice control | +4.34% to +4.35% | 0% |

The allocation witnesses cover both APIs. The ordinary dictionary shell
grows by 32 bytes, which adds about 131 KB to the CSV script per execution.
All witnesses restore the shared allowance to baseline.

## Matched local timing

The November 27, 2026 UTC WSL comparison uses one logical CPU. Two
workers per image warm for at least five seconds and supply seven rotated
batches per lane. Intervals span pairings of worker medians. Warm elapsed
includes mediated I/O, validation and previous-result disposal. Process
CPU includes fixture reset and request framing.

| Workload | Mode | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Dense nested JSON | Run | −6.43% to +4.00% | −6.86% to +0.29% |
| Quoted CSV invoice control | Run | −0.09% to +1.44% | −0.44% to +1.25% |
| Sparse nested JSON Lines | RunAsync | −3.52% to +0.73% | −2.05% to +1.46% |
| Dense nested JSON | RunAsync | −5.18% to −2.79% | −7.74% to −4.36% |

The dense async lane supports the hypothesis. The ordinary dictionary
allocation increase prevents retaining this version. The refinement
limits the extra shell slots to shared JSON records before testing the
same complete workloads again.

[Image identity](identity.json) · [Allocation and admission](allocation-summary.json) ·
[Timing summary](summary.json) · [Raw observations](observations.tar.xz) ·
[Source patches and focused checks](procedures.tar.xz).
