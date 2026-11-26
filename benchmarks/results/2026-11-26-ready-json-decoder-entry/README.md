# B1768 Ready JSON decoder entry

The prototype removes the outer decoder state machine for a completed
option preparation and parse. The existing construction loop still builds
the values. If either operation suspends, its continuation resumes the
same parse with the current cancellation state and releases the managed
frame when decoding settles. [Source attribution](../2026-11-26-current-json-attribution/README.md).

| Workload and lane | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| JSON Lines helper report, warm sync | +0.55 to +3.86% | −1.87 to +8.00% |
| Unchanged Unicode text, warm sync | −6.78 to +7.84% | −8.16 to +5.00% |
| JSON Lines helper report, warm RunAsync | −3.73 to +1.87% | −3.45 to −1.56% |
| JSON document totals, warm sync | −2.39 to +0.79% | −6.54 to +2.80% |

The November 26, 2026 diagnostic compares the df71fc295 runtime with the
archived patch on local WSL Ubuntu and .NET 11 RC3, pinned to one logical
processor. Both 32-file images match three independent CPython output and
file goldens. Two workers per image warm for four seconds and provide
seven rotated batches per workload. Ranges span all pairings of worker
medians. Negative values mean less time. Warm elapsed includes RAM-backed
I/O, verification and result disposal. Wider process CPU covers reset and
framing. The unchanged text reference workers differ by 9.83% on elapsed.
There is no quiet-VM result for this patch.

Peak admission matches in all three workload diagnostics and median CLR
allocation changes by at most 174 B per run. Focused JSON Release checks
pass on Windows .NET 11 and Ubuntu .NET 10. The suspended option test
observes the active cancellation token, verifies option truth runs before
scalar conversion and checks the allowance returns to baseline after
success or cancellation. Existing suspended hook and refusal checks pass.
All 112 local batches completed. Workers joined, fixtures were removed and
the archive hash was verified.

Keep the patch private. The local async CPU direction favors the candidate,
sync elapsed moves adversely and the text control remains variable. These
results leave the overall CPU effect uncertain. A further trial needs a
design that removes the ready-entry work with less continuation structure,
or independent attribution resolving the sync cost.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](archive-receipt.json) · [Patch and procedures](procedures.tar.xz).
