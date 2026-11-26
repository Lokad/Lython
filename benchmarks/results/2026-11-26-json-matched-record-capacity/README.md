# B1785 Trial sizing JSON record values from the existing key layout

The decoder already writes each property into the final record. A new
object with a known one- or two-key layout starts with two value
slots. The first unknown shape keeps its four-slot starting capacity.
If later input differs from the known shape, the existing dictionary
path admits private backing before replacing the shared layout. A
divergence can require an additional growth step with the smaller start.
The patch adds no cache or storage owner. [Source patch](candidate.patch)

| Complete warm sync workload | Managed bytes allocated per run, before → after | Peak admitted bytes, before → after |
| --- | ---: | ---: |
| JSON service totals, 2,048 records | 860,496 → 827,660 | 1,909,869 → 1,615,101 |
| JSON Lines helper report, 4,096 records | 1,600,030 → 1,534,280 | 3,822,014 → 3,232,334 |
| Unicode text control | 322,768 → 322,768 | 603,795 → 603,795 |

Peak admission falls by 144 B for each record after the first. Managed
allocation falls 3.82% and 4.11%, respectively. This diagnostic includes
result disposal and verifies that each owner returns to baseline.
Both 32-file images match three independent CPython output and file
goldens. Windows .NET 11 and Ubuntu WSL .NET 10 each pass 238 white-box
and 923 public JSON and record-ownership Release checks. The changed-shape
regression checks wider replacement, duplicate-key insertion order, values
and retained result lifetimes in both APIs. [Allocation](allocation-summary.json)

## Local paired timing

The November 26, 2026 UTC comparison uses the `76de4ac7e` runtime as the
reference. Both images differ only in the core DLL. WSL Ubuntu and .NET 11
RC3 ran pinned to one logical CPU, with two workers per source. Three
seconds of warmup per worker and seven rotated batches targeting 300 ms
give 140 observations. Ranges span all four pairings of worker medians.
Warm elapsed includes mediated RAM-backed I/O, output and file checks and
previous-result disposal. Wider process CPU includes reset and framing.

| Script and lane | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| JSON service totals, sync | −23.07% to +24.02% | −18.18% to +3.46% |
| Unicode text control, sync | −5.25% to +2.93% | −4.55% to +1.40% |
| JSON Lines helper report, async | +1.85% to +15.50% | −8.57% to +13.71% |
| JSON service totals, async | −28.98% to +36.42% | −0.80% to +30.00% |
| JSON Lines helper report, sync | −5.59% to +28.72% | −6.67% to +20.00% |

The sync document candidate worker elapsed spread reaches 49.78%.
Every CPU range spans zero. The async JSON Lines elapsed difference is
adverse in this block. All workers join and the archive verifies after
fixture cleanup. [Timing and spreads](summary.json) · [Frozen images](identity.json) ·
[Observations](observations.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Procedures and focused test logs](procedures.tar.xz)

Keep the existing starting capacity. The reordered local repeat again
takes longer on async JSON Lines and uses 4.83–8.06% more process CPU.
Sync document elapsed improves on repeat while CPU rises. The separate
allocation decrease remains established. This prototype trades less
retained capacity for adverse timing on an affected complete workload.
All 14 record-shape checks pass on each target with the original
capacity restored.
[Confirmation](confirmation/README.md)
