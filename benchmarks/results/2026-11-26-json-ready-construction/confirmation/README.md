# B1776 reordered confirmation

The second quiet block uses the same reference, candidate, complete input
catalog and per-lane settings as the first. The document sync lane moves to the
front and the unchanged Unicode control runs at two positions. Each row spans
all four pairings of two process medians per image. Negative changes mean
lower time. [First block and measurement boundaries](../README.md) ·
[Medians and control spreads](summary.json)

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| JSON document totals | Warm sync | −2.12% to −1.38% | −3.98% to −1.18% |
| Unicode text control, first placement | Warm sync | −5.73% to +4.11% | −5.26% to +0.82% |
| JSON Lines report | Warm async | −2.32% to +0.97% | −3.77% to +0.80% |
| JSON document totals | Warm async | −2.39% to +0.18% | −1.60% to +1.67% |
| Unicode text control, second placement | Warm sync | −0.51% to +6.94% | +0.42% to +8.91% |

The document sync elapsed and CPU ranges are favorable in both blocks. In
confirmation its same-image elapsed spreads stay below 0.48%. Async timings
remain inconclusive. The unchanged text control shifts between placements,
with same-source CPU spread up to 7.38%. The original adverse observation
remains part of the evidence. The two blocks support keeping the simpler
document construction and matching peak admission. They leave the effect on
unrelated text and the magnitude of the async change unresolved.

Both blocks complete 140 batches, pass their quiet gates, join their workers
and archive the verified observations after fixture cleanup. The confirmation
borrows the exact staged images after verifying all 32 files in each.
[Image identities](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](../procedures.tar.xz)
