# B1941 Use the Python iterator's own sequence position

The initial candidate at `405afff35` calls the existing exact sequence
reader directly from a list or tuple iterator. It removes the inner cursor
allocation and uses the Python iterator's position for pulls and length
hints. The iterator stays visible as `.0` inside generator expressions.
The regression checks shared position, live list growth, and exhaustion.
Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 163 public and 80
white-box focused Release cases. Both complete worker images verify four
CPython 3.13.16 output and file goldens and differ only in the core DLL.

On the ordinary 2,048-record reduction, the Windows .NET 11 witness finds
147,400 B less CLR allocation per invocation in each API, about 3.92%.
The range-based version of the same reduction allocates 32,768 B more in
both APIs because the extra sequence fields also enlarge generic iterators.
The iterable shipment allocation differs by at most 230 B and the file-fed
map matches. Compiled and peak execution charges match for all four scripts.
Every command returns the shared allowance to baseline.

The local Ubuntu WSL comparison on November 28, 2026 uses .NET 11 RC3
and one logical CPU. Two workers per image each warm for five seconds and
provide seven rotated batches. Ranges pair all worker medians. Elapsed
includes applicable RAM-backed I/O, verification and prior-result disposal.
Process CPU includes fixture reset and framing. Compilation and startup
are outside the clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary tuple reduction | `RunAsync` | −7.79% to −4.78% | −7.92% to −6.17% |
| Ordinary tuple reduction | `Run` | −4.44% to −0.36% | −4.62% to −1.98% |
| Iterable shipment | `RunAsync` | +2.52% to +4.21% | +1.24% to +2.77% |
| File-fed quantity map | `RunAsync` | +0.81% to +3.26% | −2.43% to +1.35% |
| Range-based reduction | `RunAsync` | −0.57% to +2.96% | −0.81% to +2.16% |

All 140 batches verify. Workers join and fixtures are removed before archive
verification. The initial staging attempt stopped on a catalog-version type
error before verification or timing. The successful attempt preserves the
protocol's integer version and verifies the copied worker images.

The initial design remains experimental. Separate concrete storage for
exact sequences and generic cursors can preserve the generic object's size
and remove its dispatch branch. The source explains the allocation cost.
The iterable CPU regression needs an independent check on the refined
source. [Refined result](refined/README.md).

[Timing](summary.json) · [Allocation](allocation.json) · [Image identity](identity.json) ·
[Archive receipt](archive-receipt.json) · [Observations](observations.tar.xz) ·
[Source patches and procedures](procedures.tar.xz).
