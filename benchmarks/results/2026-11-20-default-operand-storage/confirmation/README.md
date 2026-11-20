# B1217 Longer warm repeat for eager nested factories

Reuses both B1216 images and the exact catalog on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Two processes per
revision warm for at least 12 seconds each. Seven rotated rounds target
0.8 seconds per batch. Timing boundaries and process-median pairings match
[B1216](../README.md).

| Warm workload | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | ---: | ---: | ---: |
| Eager nested factories | −5.53% to +0.93% | −4.82% to +1.79% | 0.41% / 6.39% |
| CSV invoice | −1.67% to +1.14% | −2.52% to +0.81% | 2.34% / 0.50% |

The original roughly 9% adverse nested process does not recur. The two
candidate medians still differ by 6.39%. Both clocks span zero, so the
nested warm result remains inconclusive. The invoice control also spans
zero. The original repeat and both directions remain in B1216.

Retain for removal of the default wrapper, lower factory allocation,
and supporting elapsed and CPU results on eager rules and postponed
nested factories in B1216. Keep the eager nested uncertainty and
compile-only CPU increase as explicit limits. All four workloads have
unchanged inspected native entry eligibility.

All 56 timed batches, image and workload checks, and both machine
checks passed. All workers joined, the persistent archive verified
under lease and the guest fixtures were removed.

[Timings](summary.json) · [Raw observations](observations.tgz) ·
[Source-matched procedure](procedures.tar.xz).
