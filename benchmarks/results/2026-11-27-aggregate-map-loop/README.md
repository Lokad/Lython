# B1818 Accumulate completed mapped rows in the producer loop

The trial lets asynchronous `sum` drive a same-command short `map` until
an iterator or callback suspends, a value requires Python addition, or the
map ends. The producer applies completed numeric results to the existing
sum state before returning through the aggregate await loop. Completed
rows update the subtotal immediately. A pending operation follows
the existing map continuation with its funded operands. General additions
receive the original result and accumulated prefix. The numeric phase
retains overflow promotion and Python 3.13 floating compensation.

The file-fed CSV fixture converts a single field per row and sums it.
Ordinary and bound zip shipment and the protected file search keep their
existing consumers. Both frozen images match four independent CPython
output and file goldens. Windows .NET 11 and Ubuntu .NET 10 each pass 831
focused Release checks. Three new completed scripts match CPython 3.13.2.
The interruption checks cover a source that suspends after another operand
was read and a callback that suspends with the full row bound. Reentrant
addition consumes the same map without a repeated pull.

## Allocation and timing

Windows .NET 11 measures CLR allocation and peak admitted bytes. All
shared allowances return to baseline after disposal.

| Workload | API | CLR bytes, reference → candidate | Peak admitted bytes, reference → candidate |
| --- | --- | ---: | ---: |
| Protected file-search control | sync | 56,160 → 56,160 | 36,512 → 36,512 |
| File-fed CSV map and sum | sync | 808,528 → 808,528 | 2,270,071 → 2,270,071 |
| Ordinary shipment control | sync | 1,092,936 → 1,092,936 | 2,817,644 → 2,817,644 |
| Bound zip/starmap control | sync | 1,099,736 → 1,099,736 | 2,822,785 → 2,822,785 |
| Protected file-search control | async | 56,408 → 56,408 | 36,512 → 36,512 |
| File-fed CSV map and sum | async | 808,744 → 808,744 | 2,270,071 → 2,270,071 |
| Ordinary shipment control | async | 1,093,120 → 1,093,176 | 2,817,644 → 2,817,644 |
| Bound zip/starmap control | async | 1,099,920 → 1,100,150 | 2,822,785 → 2,822,785 |

Ubuntu WSL uses two workers per source, three seconds of warmup and seven
rotated batches per worker. The intervals span all worker median pairings.
Negative values mean less time. Elapsed includes RAM-backed file I/O,
output verification and prior-result disposal. The wider process clock
includes fixture reset and framing. Compilation and startup are separate.

| Workload | Warm API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed CSV map and sum | async | −2.76% to +11.54% | −6.78% to +15.16% |
| Ordinary shipment control | async | −15.86% to −7.58% | −22.22% to −15.57% |
| File-fed CSV map and sum | sync | −2.14% to +38.83% | −2.67% to +39.39% |
| Bound zip/starmap control | async | −3.14% to +0.80% | −4.68% to −0.15% |
| Protected file-search control | sync | −4.54% to +9.72% | −10.86% to +14.30% |

## Pinned repeat

The same binaries and goldens repeat the file-fed case and bound shipment
control on one logical CPU in Ubuntu WSL. The bound control runs first and
workers warm for six seconds. Each pairing uses the previous block's full
execution and process CPU clocks. This is a local diagnostic.

| Workload | Warm API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Bound zip/starmap control | async | +20.90% to +69.27% | +12.50% to +62.90% |
| File-fed CSV map and sum | async | −2.90% to +4.60% | −7.60% to +0.94% |

Keep the runtime trial private. The file-fed scripts match on CLR bytes
and peak admission in both APIs. The initial affected async timing spans
−2.76% to +11.54% elapsed and −6.78% to +15.16% CPU. On the pinned repeat
it spans −2.90% to +4.60% elapsed and −7.60% to +0.94% CPU. The unchanged
bound control is 20.90–69.27% slower in repeat elapsed and has wide
same-source spread. The ordinary shipment control moves favorably in the
first block. The complete handoff is absent from the affected loop, and
its CPU effect remains unresolved. Preserve both blocks and revisit only
when the design can also remove a materialization or when tighter
conditions make the isolated effect decision-relevant.

The six new regression cases also pass with the delivered runtime on
Windows .NET 11 and Ubuntu .NET 10. A separate census on that core
confirms a three-value and a five-value `MakeList` immediately before
`Yield` in the two shipment generator expressions. The bound shipment
and file-fed map have neither list yield. [Delivered tape census](delivered-yield-census.json).

[Allocations](allocation-summary.json) · [Timing](summary.json) ·
[Image hashes](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Patches and checks](procedures.tar.xz).

[Repeat timing](confirmation/summary.json) · [Repeat observations](confirmation/observations.tar.xz) · [Repeat receipt](confirmation/archive-receipt.json).
