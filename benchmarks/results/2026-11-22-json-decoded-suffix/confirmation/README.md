# B1318 Confirm JSON suffix decoding

Reused B1317's verified source pair, catalog and complete worker images
on November 22, 2026 UTC. Warm-up rose from eight to sixteen seconds
per process. The escaped and plain report lanes reversed role order.
Both reports and the unchanged text control used seven rotated batches
from two independently warmed processes per revision on the quiet Azure
F4as_v7. The runner includes mediated RAM-backed I/O and prior-result
disposal. Process CPU includes the wider worker request. Ranges span
process-median pairings.

| Whole warm script | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Escaped-Unicode formatted report | −1.13 to +2.55% | −0.45 to +0.78% | 1.65 / 2.04% |
| Plain formatted report | −1.83 to +1.31% | −1.03 to +0.87% | 1.68 / 1.49% |
| Unchanged mixed-Unicode text report | +0.20 to +1.08% | +0.41 to +1.95% | 0.25 / 0.63% |

Keep the candidate private. Both report clocks cross zero. The plain
report's initial CPU benefit did not repeat with the longer warm-up
and tighter control spread. Escaped-report CPU still spans zero.
The candidate removes a second decode for short suffixes while
retaining the wide replay path. Allocation and accounted memory are
effectively flat. Those results do not justify the extra scan and
scratch paths in this form. The text control moves slightly against
the candidate without exercising the decoder.

All 84 timed batches matched output hashes. The complete image pair
and catalog matched B1317. Workers joined and the persistent archive
verified under the lease after fixture cleanup in 263 seconds.
[B1317 initial comparison](../README.md) · [Timing](summary.json) ·
[Raw observations](observations.tgz) · [Procedures](procedures.tar.xz).
