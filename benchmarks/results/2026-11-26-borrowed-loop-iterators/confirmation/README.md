# B1782 reordered shipment confirmation

The [initial images and catalog](../README.md) ran again on the same quiet
VM. The four lanes change placement and keep five-second warmups, seven
rotated batches per worker and the same execution boundaries. The ranges
span all four pairings of worker medians.

| Complete script and lane | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Shipment callbacks, sync | −7.82% to −5.08% | −6.53% to −4.71% |
| Protected file search, sync | +0.46% to +1.11% | +0.01% to +1.95% |
| Shipment callbacks, async | +0.53% to +2.56% | +1.36% to +3.19% |
| File-fed CSV quantity map, async | +2.65% to +4.60% | +2.38% to +5.69% |

Sync shipment elapsed worker spread falls below 2% in both builds.
Async shipment uses more CPU with stable workers in this block. The
file-fed map also uses more CPU, with zero calls through the new
borrowed-loop route. The candidate is retained for its sync improvement
and shared loop ownership, with the async timing increase open.
The first block is retained alongside this one.

All 112 batches verify on the same frozen images. Both quiet gates pass,
workers join, and the persistent archive verifies after fixture removal.
[Timing](summary.json) · [Images](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz)
