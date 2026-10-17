# Direct float text: allocation diagnostic

One-positional native text/bytes conversions reuse operand storage and the
existing admitted float parser. This compares Lython with earlier Lython. It
has no CPython allocation baseline and establishes no elapsed-time, RSS or host
latency benefit. Primary timing comparisons stay in [BENCHMARKS.md](../../../BENCHMARKS.md).

| Warm128-conversion loop | Baseline allocated bytes | Candidate allocated bytes |
| --- | ---: | ---: |
| Native text, synchronous API | 18,912 | 14,816 |
| Native text, RunAsync | 30,152 | 14,792 |
| Expanded-call synchronous control | 95,712 | 95,712 |
| Expanded-call RunAsync control | 104,904 | 104,904 |

Measured2026-10-17 UTC, Intel Core i7-14700KF, Windows10.0.26200 x64, Release
net10.0. Baseline is f0d3b700; candidate is the three-file shared-parser/direct
operand patch on that revision before commit. [allocation.json](allocation.json)
pins candidate source identities, every sample and the recipe hash. These are
historical patch identities, not measurements of arbitrary later HEAD.

The [temporary recipe](DirectFloatTextAllocationProbe.cs.txt) compiles once and
prepares128 distinct native strings (0.5 through127.5) outside measurement. Four
warmups precede eight current-thread allocated-byte samples. Each loop converts
and sums all values; both APIs must complete inline. The general control uses
float(*(text,)), including its tuple expansion. Its additional packaging is not
subtracted from native results. No actual I/O, GC.Collect, custom tiering or
protection switches. Assertions check sum8192.0, unwind, temporary reservations
and final shared-budget refunds. This witness does not measure bytes inputs or
complete CSV elapsed time.

To reproduce, copy the recipe temporarily into the white-box project, build
Release and run only DirectFloatTextAllocationProbe. Archive and remove it before
final qualification; it is absent from the permanent suite. Separate semantic
coverage includes native strings/bytes, signed zero/nonfinite values, errors,
expanded/keyword calls, shadowing/aliases, guest protocols, exact fuel, actual
I/O/cancellation/refusal, retained input and complete CSV-to-JSON output bytes.
