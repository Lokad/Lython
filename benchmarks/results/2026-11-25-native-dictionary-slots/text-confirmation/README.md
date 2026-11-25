# B1712d Text timing at two warmup depths

The reference `6934aafb9`, layout-only variant and prepared-consumer
variant use the same complete nonlocal word report and exact frozen
images as the text profiles. The Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3 on November 25, 2026 UTC. All six workers used the ordinary
runtime and verified the CPython output and written files.

Two workers per image each contribute seven rotated batches at two
points in the same process lifetime. Each point starts after at least
the named number of accumulated full-request seconds. The second
point includes the first sample block in its warmup depth. Warm elapsed
includes mediated RAM-backed I/O, verification and result cleanup.
Wider CPU includes fixture reset and request framing. Comparisons span
all four pairings of the two worker medians per image.

| Minimum warmup | Layout vs reference CPU | Prepared vs reference CPU | Prepared vs layout CPU | Prepared elapsed worker spread |
| --- | ---: | ---: | ---: | ---: |
| 5 seconds | +0.14 to +6.09% | −7.10 to +11.16% | −10.16 to +8.20% | 19.20% |
| 16 seconds | +0.42 to +5.60% | −8.78 to +13.53% | −12.37 to +11.44% | 19.55% |

The reference elapsed worker spread stays below 2.63%, and the layout
spread stays below 3.20%. The two prepared workers continue to differ
after deeper warmup, with the faster worker also faster than the
reference. The earlier coherent prepared slowdown remains a valid
observation. This confirmation leaves its magnitude and cause unresolved.
The layout alone shows a smaller adverse comparison at both depths.
The profiles do not identify a divergent hot path. Keep both prototypes
private and resume the function and operand roadmap. Revisit this source
pair when there is a concrete hypothesis that can resolve the process
split or a smaller storage migration worth measuring.

Both quiet gates and all 84 timed batches passed. All workers joined,
guest fixtures were removed, and the archive verified under the lease.

[Image identity](identity.json) · [Timings and worker spreads](summary.json) ·
[Observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz).
