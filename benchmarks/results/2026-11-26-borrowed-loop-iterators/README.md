# B1782 Borrow managed iterator values in executable loops

The executable loop keeps a managed iterator value that already owns its
position. Guest iteration protocols still resolve through the existing
cursor, and CLR enumerators keep cursor-owned cleanup. The loop checks
the managed stack before a borrowed pull. Fixed CSV and zip unpack
operate on the iterator itself. On exhaustion the loop takes the live
operand, disposing it when it is a private cursor. The async continuation
needs only the pending operation and instruction. Text files keep the
direct line publishing introduced by B1781. [Patch](candidate.patch)

The separately instrumented shipment script borrows one CSV dictionary
reader and 16 zip iterators in either API. It takes 2,049 ordinary
borrowed pulls and 2,064 borrowed fixed-unpack advances, including
exhaustion. The three comparison scripts have no new borrowed pulls.
Shipment CLR allocation falls from 1,094,976 to 1,093,480 bytes per
invocation. Peak admission matches on all four scripts, and cleanup
restores the allowance. Both images match all four CPython output and
file goldens. [Routes](routes.json) · [Allocation](allocation-summary.json)

The November 26, 2026 UTC quiet comparison starts at `ca0fd89f4`
on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. The complete 32-file
images differ only in the core DLL. Two workers per source warm for
five seconds and supply seven rotated batches per lane. Change ranges
cover all four pairings of worker medians. Negative values mean less
time. Warm elapsed includes mediated RAM-backed I/O, output and file
checks and previous-result disposal. Process CPU also covers fixture
reset and framing. Startup, compilation and final teardown sit outside
these clocks.

| Complete script and lane | Initial elapsed change | Initial process CPU change |
| --- | ---: | ---: |
| File-fed CSV quantity map, async | −2.57% to +0.71% | −2.62% to +1.60% |
| Shipment callbacks, sync | −5.85% to +1.14% | −7.39% to +1.29% |
| Protected file search, sync | −0.57% to +0.44% | −0.26% to +0.98% |
| Shipment callbacks, async | −2.01% to +5.32% | −2.22% to +3.67% |
| Ordinary definitions, sync | −1.56% to +1.01% | −0.65% to +0.31% |

Each platform passes 551 white-box and 1,847 public focused Release
checks, on Windows .NET 11 and Ubuntu .NET 10. These include strict
zip exhaustion, partial unpack across suspension, cancellation,
generator cleanup, and text-file alias ownership. All 140 timed
batches complete, both quiet gates pass, workers join and the archive
verifies after fixture removal. Staging reclaims 12 redundant archive
copies after matching them to committed bytes under the existing lease.
[Initial timing](summary.json) · [Source and image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and tests](procedures.tar.xz)

Retain the ownership simplification and the confirmed sync shipment gain.
The reordered block shows higher async shipment CPU and higher CPU on the
file-fed control, which takes zero new borrowed pulls. The cause remains
open. Keep both adverse ranges when checking the next callable delivery.
[Confirmation](confirmation/README.md)
