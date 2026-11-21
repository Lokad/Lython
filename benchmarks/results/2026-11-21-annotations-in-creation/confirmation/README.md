# B1292 callable-integrated annotation confirmation

Repeated the eager rule and unchanged helper warm lanes from [B1291](../README.md)
and added eager time to result. Both runs compare the same complete
frozen images, `256c5e651` and `595798ffa`, on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC. Two
independently warmed processes per image supply seven rotated batches
each. Ranges span all four pairings of process medians.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Fully supplied helper | Warm | −2.37% to +2.76% | −2.61% to +3.06% | 4.59% / 0.63% |
| Eager rules | Warm | −4.81% to −0.39% | −5.22% to +1.23% | 4.19% / 0.43% |
| Eager rules | Compile to result | −7.65% to −1.74% | −8.15% to −0.88% | 1.95% / 4.37% |

The warm clock includes execution, golden observation and prior-result
disposal. Time to result adds compilation and code-owner disposal.
These scripts have no guest filesystem I/O. Startup and final teardown
are outside these clocks. Process CPU includes the wider request.

Retain the callable-integrated reads. Both runs favor warm eager rules,
with reference spread obscuring the size of the effect. Warm CPU is
mixed in this repeat. Eager time to result improves in both clocks.
The earlier 15.05% candidate spread for the unchanged helper does not
recur, and its comparison spans zero. The nested and observe-all warm
elapsed results remain inconclusive. Allocation and accounted peaks are
effectively flat in the original local diagnostic. These timings apply
to the two measured images. No separate compile-only lane or refreshed
CPython timing was collected for this change.

Both quiet gates and all 84 batches pass. Workers join and the archive
verifies under the lease after removing guest fixtures. The original
warm comparison and its adverse control are preserved alongside this
confirmation.

[Timing summary](summary.json) · [Raw observations](observations.tgz) ·
[Image verification and procedures](procedures.tar.xz).
