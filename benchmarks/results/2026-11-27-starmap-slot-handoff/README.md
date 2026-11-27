# B1819 Transfer evaluated generator rows into callback slots

The trial on 36f7b175b lets a starmap pull request an exact list display
that immediately feeds a same-command generator yield. The generator
leaves its frame at the normal yield boundary. Its existing slots remain
funded until the caller publishes canonical aliases directly into the
actual callback slots, including the bound receiver, and retires the
source operands. A pending callback uses the ordinary admitted continuation.
Direct pulls, other arities and dynamic callbacks keep ordinary yields.

The first frozen trial recognized bare functions only. Both shipment
scripts use bound methods, so allocation and admission matched the starting
runtime. The corrected trial takes the receiver through the existing call
binder. Both final images verify the four existing independent CPython
output and file goldens. The candidate passes 1,281 focused Release public
checks on both Windows .NET 11 and Ubuntu WSL .NET 10. Three completed
regression cases match CPython 3.13.2. They cover a bound callback resuming
its source and obtaining an ordinary list, and an async source or callback
that can suspend. Cancellation waits for close and returns the allowance
to baseline in both suspension cases.

## Allocation and admission

Windows .NET 11 runs both complete APIs on frozen full images. Shared
allowance returns to baseline on each run.

| Workload | API | CLR bytes, reference → trial | Peak admitted bytes, reference → trial |
| --- | --- | ---: | ---: |
| Three-field shipment | sync | 1,325,656 → 1,112,664 | 3,467,109 → 2,845,113 |
| Five-field shipment | sync | 1,358,992 → 1,113,176 | 3,795,557 → 2,845,833 |
| Bound zip/starmap control | sync | 1,099,736 → 1,099,736 | 2,822,785 → 2,822,785 |
| File-fed map control | sync | 808,528 → 808,528 | 2,270,071 → 2,270,071 |
| Three-field shipment | async | 1,325,840 → 1,112,848 | 3,467,109 → 2,845,113 |
| Five-field shipment | async | 1,359,120 → 1,113,360 | 3,795,557 → 2,845,833 |
| Bound zip/starmap control | async | 1,099,920 → 1,099,920 | 2,822,785 → 2,822,785 |
| File-fed map control | async | 808,744 → 808,744 | 2,270,071 → 2,270,071 |

## Local warm comparison

Ubuntu WSL runs two workers per source with three seconds of warmup and
seven rotated timed batches per worker. Intervals span the four pairings of
worker medians. Negative values mean less time. Warm execution includes
mediated RAM-backed I/O, output and file verification and previous-result
disposal. Process CPU also includes fixture reset and request framing.
Compilation and startup are outside these lanes.

| Workload | Warm API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Three-field shipment | async | −19.49% to +0.85% | −32.15% to +9.51% |
| Bound zip/starmap control | async | −6.26% to −1.13% | −7.34% to +2.41% |
| Five-field shipment | async | −10.22% to −1.11% | −9.78% to +0.83% |
| File-fed map control | sync | −2.27% to +1.64% | −3.30% to −0.94% |
| Five-field shipment | sync | −8.88% to −4.64% | −6.21% to −3.45% |

Keep the direct transfer. The two shipment scripts allocate 16–18% less
and use 18–25% less peak admitted memory in both APIs. Both controls
match allocation and admission. Five-field sync uses 4.64–8.88% less
elapsed time and 3.45–6.21% less process CPU locally. The file-fed sync
control also uses 0.94–3.30% less CPU. Async CPU spans zero in both
affected cases. The bound control also improves in async elapsed, and
the three-field reference workers differ substantially. Treat async CPU
and the size of any async elapsed gain as unresolved.

[Allocation](allocation-summary.json) · [Timing](summary.json) ·
[Image hashes](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Patches and checks](procedures.tar.xz).
