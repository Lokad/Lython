# B1787 Quiet integer continuation confirmation

The [initial comparison](../README.md) supplies the same worker images,
source and fixture goldens. This run reorders five lanes and uses the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two workers per
source warm for five seconds and supply seven rotated timed batches
targeting 440 ms. Ranges span all four pairings of worker medians.

| Complete script and lane | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| CSV invoice, sync | +0.13% to +1.32% | 0.00% to +2.27% |
| Ordinary definitions, async | −1.00% to +0.75% | −4.83% to +1.86% |
| Protected file search, sync | −1.78% to −0.07% | −1.62% to −1.34% |
| Escaping rules, sync | −2.63% to +1.43% | −4.85% to +2.63% |
| Ordinary definitions, sync | −2.01% to +0.50% | −1.88% to +1.49% |

The invoice elapsed spread is below 0.68% per source. The first block's
invoice improvement and async ordinary-definition slowdown do not
reproduce. The unchanged file-search control uses less CPU. Both quiet
checks and all 140 timed batches pass. Workers join, guest fixtures are
removed and the persistent observations archive verifies.

Staging recovers 11.65 MB from two inactive historical VM worker copies.
Their complete images were matched against frozen local copies and each
retired DLL was also preserved and verified in a local compressed backup.
The published observations for both older runs remain byte-identical.
The original launch stopped at a missing CPython digest. The correction
adds that digest to the staged manifest, verifies the unchanged images and
inputs and matches the four goldens before sampling.

[Medians and spreads](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures and staging evidence](procedures.tar.xz)
