# B1783 reordered fixed-row local binding confirmation

The [initial block](../README.md) supplies the identical 32-file
images, workload catalog, timing boundaries and sample counts.
This block reorders the three affected lanes and a file-search control.
Change ranges span all four pairings of worker medians.

| Complete script and lane | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Protected file search, sync | +0.17% to +0.46% | −0.26% to +0.65% |
| Shipment callbacks, sync | −6.97% to −0.56% | −7.23% to −0.41% |
| Explicit-float CSV meter, sync | −4.33% to −1.79% | −4.43% to −1.94% |
| Shipment callbacks, async | −2.17% to −0.35% | −2.97% to −0.43% |

File search has under 0.27% within-source elapsed spread and mixed
CPU. The initial 16.17% candidate elapsed spread does not recur.
The CSV meter reverses the first block's adverse CPU direction.
Sync shipment elapsed falls in both blocks, with up to 4.48%
elapsed spread in the candidate on this repeat. Async CPU falls in
this block and spans zero in the initial block.

All 112 batches pass. Both quiet checks, worker joins and persistent
archive verification complete with fixtures removed.
[Timing](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz)
