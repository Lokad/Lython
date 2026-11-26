# B1780 confirmation with reordered warm lanes

The same two complete images and CPython-verified workload catalog
from the [first block](../README.md) run on the same quiet VM with
reordered placement. The four selected lanes use the original
warmup, batch size target and seven observations per worker. The
timing boundaries and four pairings of process medians are identical.

| Workload and lane | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| JSON Lines helper report, sync | −7.77% to −1.92% | −6.77% to −1.52% |
| Unicode report, sync | +2.53% to +6.97% | +3.77% to +5.59% |
| JSON Lines helper report, async | −5.65% to +3.99% | −2.15% to +4.35% |
| Document service totals, sync | −0.82% to +0.81% | −1.19% to +0.22% |

The sync JSON Lines reference workers differ by 5.33% in elapsed,
and async candidate elapsed spread reaches 8.06%. Both sync JSON
Lines CPU ranges favor the candidate. Both blocks show higher
Unicode control CPU, with under 1% same-source CPU spread on the
repeat. The separate route trace shows zero direct decoder entries
for this report. The cause of the difference remains unresolved.

The runtime keeps its existing entry. All 112 batches verify,
both quiet checks pass, all workers join and the observations are
verified in persistent storage. [Timing](summary.json) ·
[Observations](observations.tar.xz) · [Image identity](identity.json) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz)
