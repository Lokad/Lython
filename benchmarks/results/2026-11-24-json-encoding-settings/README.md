# B1552 Encoder settings stored by value

Private `3d52767fe` embeds resolved settings in the JSON iterator,
reuses immutable Boolean boxes, and shares the indent emission entry.
The matched runtime reference is `b3c447420`. A managed constructor
diagnostic measured 160 → 216 CLR bytes for the iterator at the same
224-byte admission. Both images match all four stock CPython 3.13.16
goldens. Each platform passed 909 public and 221 white-box Release
checks, including cancellation and refusal in a suspended callback
from expanded options.

| Warm script | CLR allocation change | Elapsed change | Wider process CPU change |
| --- | ---: | ---: | ---: |
| Expanded JSON Lines export | −10.88% | −1.76% to +0.53% | −2.24% to 0.00% |
| Default-options JSON Lines export | +3.48% | +1.23% to +3.02% | 0.00% to +2.68% |
| Configuration edit | −0.34% | −0.81% to +0.60% | −0.80% to +1.29% |
| Unchanged positional calls | 0.00% | +0.38% to +3.36% | +0.20% to +3.62% |

Admitted peaks match and the budget returns to baseline for every
invocation. The local allocation figures are medians of six complete
Windows invocations. Quiet timing on the Azure F4as_v7, Ubuntu 24.04.4
and .NET 11 RC3 uses two independently warmed processes per image,
eight warm seconds and seven batches per worker. Ranges span all
pairings of worker medians. Elapsed includes mediated RAM I/O,
golden verification and prior-result cleanup. Process CPU includes
reset and protocol handling. All 112 samples and both machine gates
pass, then the workers join and the archive verifies under lease.

Keep the embedded layout private. The allocation reduction on expanded
options coincides with larger default iterators, slower default encoding
and adverse unchanged call timing. The positional lane uses no JSON
encoder, so its slowdown gives no cause. The separate refinement
keeps the original iterator layout to isolate the savings from flag
boxing and indent routing. Staging initially stopped on the disk
margin. The successful collection retired only two exact archived
working copies and preserved the original condition.

[Allocations](allocation-summary.json) · [Timing](summary.json) ·
[Image identity](identity.json) · [Terminal status](terminal-status.json) ·
[Raw observations](observations.tar.xz) · [Procedures and patch](procedures.tar.xz).
