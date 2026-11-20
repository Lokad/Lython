# B1221 Manifest comparison with longer batches

Uses the exact B1220 images and catalog on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Two processes per
revision warm for at least 12 seconds. Seven rotated rounds target
0.8 seconds per batch. Timing boundaries and four-way median pairings
match [B1220](../README.md).

| Configuration manifest | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | ---: | ---: | ---: |
| Compile to result | −2.83% to +0.32% | −2.78% to 0.00% | 2.47% / 0.75% |
| Warm | −1.47% to +0.32% | −1.44% to +0.24% | 1.10% / 0.71% |

The initial manifest compile-to-result increase does not repeat.
Both elapsed intervals span zero. Keep the original adverse
observations and retain the simplified compiler for the measured
dataclass gains. Dataclass warm elapsed and eager warm controls
remain inconclusive.

All 56 timed batches and both machine checks passed. Workers joined,
the persistent archive verified under the lease, and the guest
fixtures were removed.

[Timings](summary.json) · [Raw observations](observations.tgz) ·
[Source-matched procedure](procedures.tar.xz).
