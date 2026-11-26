# B1785 Local confirmation of JSON record sizing

The [initial block](../README.md) supplies the same frozen images,
source and fixture goldens. The repeat runs three lanes with reordered
placement and pins one logical CPU on local Ubuntu WSL with .NET 11 RC3.
Two workers per source warm for five seconds and contribute seven rotated
batches targeting 460 ms. Change ranges span all four pairs of medians.
Negative changes mean less time. [Medians and spreads](summary.json)

| Complete script and lane | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| JSON Lines helper report, async | +0.52% to +7.93% | +4.83% to +8.06% |
| Unicode text control, sync | −15.72% to −7.15% | −8.94% to +5.32% |
| JSON service totals, sync | −3.45% to −0.26% | +0.73% to +2.81% |

The text control takes less elapsed time with 9.84% candidate elapsed
spread and mixed CPU. Async JSON Lines has under 2.32% worker CPU spread
per source. The cause of the adverse CPU result remains unresolved.
All 84 batches verify. Workers join and the persistent archive verifies
after fixture cleanup. The quiet VM storage preview made no changes.
Available space and verified duplicate copies fell short of the existing
staging floor, so no VM run was launched.

[Frozen images](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Procedures and VM preview](procedures.tar.xz)
