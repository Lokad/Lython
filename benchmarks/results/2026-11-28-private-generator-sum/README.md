# B1940 Pass the private generator frame to builtin sum

The candidate at `b95e5d0a6` passes the already-admitted frame of an
adjacent generator expression to the evaluated builtin `sum`. The existing
operand owns the frame through reduction and unwind. This removes the Python
generator wrapper and sum's iteration cursor in that case. Argument evaluation
still acquires the outer iterator first. The ordinary generator path handles
other targets and results that escape the expression.

Both paths share the existing numeric accumulator and generator advancement.
Focused regressions cover target rebinding during `__iter__`, user-defined
numeric addition, suspended I/O, cancellation, refused admission and cleanup.
Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 711 public and 159
white-box Release cases. Both 32-file Linux worker images match their
recorded hashes and verify all four CPython 3.13.16 output and file goldens.
Only the core DLL differs between images.

## Allocation

The ordinary reduction reads 2,048 records from a 27,350 B CSV, runs a helper
and sums a generator expression for each row. In the separate Windows .NET 11
witness, CLR allocation falls from 4,052,878 B to 3,757,966 B with `Run` and
from 4,053,094 B to 3,758,182 B with `RunAsync`. Both save 294,912 B, about
7.28%. Allocation for each control differs by at most 56 B. Compiled and
peak execution admission match for all cases. Every command returns the
allowance to baseline.

## Warm execution

The November 28, 2026 local Ubuntu WSL comparison uses .NET 11 RC3 and
one logical CPU. Two workers per image warm for at least five seconds each
then rotate through seven timed batches. Ranges pair all worker medians.
Elapsed includes applicable RAM-backed I/O, output and file verification,
and prior-result cleanup. Process CPU also includes fixture reset and request
framing. Compilation and startup are outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary helper and generator reduction | `RunAsync` | −5.77% to −2.57% | −3.86% to −1.67% |
| Ordinary helper and generator reduction | `Run` | −1.75% to +1.18% | −1.37% to +0.72% |
| Iterable shipment | `RunAsync` | −1.77% to +1.25% | −1.88% to +0.39% |
| File-fed quantity map | `RunAsync` | +2.77% to +8.32% | −1.92% to +9.19% |
| First-line file search | `RunAsync` | −1.47% to +0.04% | −2.61% to −1.71% |

All 140 batches verify. Workers join and guest fixtures are removed before
the archive is verified. Retain the change for the allocation reduction and
favorable local async reduction timing. Sync reduction remains inconclusive.
The file-fed map's elapsed result is adverse, with within-image CPU spread of
4.18% reference and 6.87% candidate. Recheck that control at the next quiet
checkpoint. The first-line control also uses slightly less CPU. The
[B1937 external timings](../2026-11-28-current-cpython-shipment/README.md)
describe their measured revision.

[Timing](summary.json) · [Allocation](allocation.json) · [Image identity](identity.json) ·
[Archive receipt](archive-receipt.json) · [Observations](observations.tar.xz) ·
[Source patches and procedures](procedures.tar.xz).
