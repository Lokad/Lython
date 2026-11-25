# B1678 Carry operand slots through short `map` callbacks

Each execution mode uses one ordered cursor pull loop. Short rows hold the
existing operand slots. Compiled callbacks enter the existing direct evaluator
and native workspace. Builtins use the existing operand binder. A callback that
needs the general borrowed packet copies the row once at that boundary. Wide
calls retain their admitted packet and its original lifetime.

The short callback returns a ready operand or hands the iterator the existing
pending operation to await. The first verified draft wrapped the pending
operation in another continuation. The timed source removes that wrapper.

The reference is `2b1e8a6f2`. Both 32-file images match five complete CPython
3.13.2 goldens and their 15 executable bodies and 514 instructions, spans and
retained compilation admission. Windows passes 274 focused public checks on
each Release target. Ubuntu .NET 11 passes 274 public and 21 white-box checks.
The new test interrupts a later source after the first row operand is supplied,
verifies file cleanup and allowance reuse, and checks aliases and overflow
promotion through nested maps.

| Complete script | Reference allocation | Candidate allocation | Admitted peak in both |
| --- | ---: | ---: | ---: |
| Nested scalar report, 4,096 readings | 406,824 B | 13,608 B | 2,720 B |
| Shipment callback report | 1,315,684 B | 1,315,584 B | 2,817,644 B |
| Shipment native `map(int, …)` | 1,139,376 B | 1,139,446 B | 2,662,235 B |
| Shipment with retained zip tuples | 1,223,840 B | 1,223,784 B | 2,892,539 B |
| Quote-heavy invoice | 3,281,540 B | 3,281,590 B | 9,040,266 B |

The scalar report saves 393,216 B, or 96.66%, in managed allocation. It passes
values from `range` through two machine-integer callbacks into `sum`. Every
allowance returns to baseline after owned cleanup.

## First run with a failed quiet check

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Four workers warmed for five seconds and ran seven rotated batches with a
0.72-second target. Warm includes applicable mediated RAM-backed I/O,
verification and previous-result disposal. Wider CPU also includes request
framing and fixture reset. The async lane uses `RunAsync`. All 168 batches
verify and each recorded job reports its workers joined and fixtures cleaned.
The post-run machine check records 9% idle CPU and rejects qualification.
The terminal receipt preserves that failure and the raw observations.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Nested scalar report | Warm | −29.52 to −17.27% | −29.63 to −17.55% | 16.81 / 0.49% |
| Shipment native map | Warm | +2.48 to +6.05% | +2.49 to +5.77% | 2.83 / 0.63% |
| Shipment callback report | Warm | −2.93 to −0.39% | −2.02 to −0.34% | 1.28 / 1.32% |
| Shipment with retained zip tuples | Warm | +2.23 to +5.89% | +3.45 to +6.61% | 1.45 / 2.10% |
| Quote-heavy invoice | Warm | −0.92 to +0.91% | +0.50 to +0.88% | 1.30 / 0.54% |
| Shipment callback report | `RunAsync` warm | −0.65 to −0.32% | −1.77 to +0.70% | 0.00 / 0.33% |

These are diagnostic worker-median pairing ranges. The scalar allocation
result stands separately. The retained-zip script has no `map` call. The
candidate also changes the shared executable builtin input shape, so the
initial comparison cannot locate the source of that adverse control. Keep
the source private. The refinement restores the ordinary input path and
uses a bounded positional view for the short builtin callback in the same
binder. It also skips the completed task wrapper on synchronous builtin calls.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocations](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal status](quiet-terminal-status.json) ·
[Procedures and source](procedures.tar.xz).
