# B1945 Prepare private generator call shape during instruction layout

The compiler records the evaluated target's operand slot for a generator
expression adjacent to a single-positional-argument call. It uses the
existing instruction operand field. Runtime checks whether that slot
contains builtin `sum` before acquiring the outer iterator. This removes
the runtime neighbor and call-site inspection.

Only eligible generator-expression operands extend the cleanup bound.
The iterable shipment module's last potentially owning slot returns
from 11 to 8. The tuple and range reductions retain their private handoffs.
All four scripts retain the same opcode sequences. Their compiled and
peak execution admission match, and every command refunds to baseline.
CLR allocation differs by at most 56 B.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 738 public and
193 white-box focused Release cases. The existing checks cover evaluated
callable identity, `__iter__` rebinding, closure retention, genuinely
suspended work, cancellation, stack admission and cleanup. Both frozen
32-file images verify all four CPython 3.13.16 output and file goldens.
Only the core DLL differs from the reference at `bf10eeaea`.

## Warm execution

The November 28, 2026 local Ubuntu WSL run uses .NET 11 RC3 and one logical
CPU. Two workers per image warm for five seconds and supply seven rotated
batches for each lane. Ranges pair all worker medians. Warm elapsed
includes applicable RAM-backed I/O, verification and previous-result
cleanup. Process CPU also includes fixture reset and framing. Compilation
and startup are outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | `Run` | −0.42% to +2.14% | −0.54% to +1.66% |
| Iterable shipment | `RunAsync` | −0.42% to +4.40% | +0.48% to +1.60% |
| Ordinary tuple reduction | `Run` | −5.28% to −2.27% | −6.15% to −0.52% |
| Ordinary tuple reduction | `RunAsync` | −0.89% to +2.81% | −2.56% to +3.23% |
| File-fed quantity map | `RunAsync` | −2.92% to +0.39% | +0.36% to +2.16% |
| Range reduction | `RunAsync` | −2.46% to +3.00% | −0.67% to +3.18% |

All 168 batches verify and all workers join. The first collection stopped
before timing because its copied driver still expected five lanes. The
corrected run kept the same jobs, images, goldens and measurement boundary.

Retain for the simpler handoff and narrower cleanup bound. The ordinary
sync CPU result is favorable with 5.21% spread in reference worker medians.
Async iterable and file-fed CPU rise slightly, and ordinary async timing
is inconclusive. B1944's file-fed regression remains open. [B1944](../2026-11-28-cpython-sequence-iterator-checkpoint/matched-control/README.md).

[Timing](summary.json) · [Allocation](allocation.json) · [Operand layout](operand-layout.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) · [Procedures](procedures.tar.xz).
