# Bounded startswith: allocation diagnostic

Immediate positional startswith calls with start/end bounds now borrow the
operand frame and reuse the existing affix helper. This compares Lython with
earlier Lython. It has no CPython allocation baseline and establishes no elapsed
time, RSS or embedding-latency benefit. Primary external timing comparisons stay
in [BENCHMARKS.md](../../../BENCHMARKS.md).

| Warm 128-call loop | Baseline allocated bytes | Candidate allocated bytes |
| --- | ---: | ---: |
| Immediate call, synchronous API | 48,640 | 7,680 |
| Immediate call, RunAsync | 55,784 | 7,656 |
| Exposed method, synchronous control | 44,552 | 44,552 |
| Exposed method, RunAsync control | 55,792 | 55,792 |

Measured 2026-10-17 UTC on an Intel Core i7-14700KF, Windows 10.0.26200 x64,
Release net10.0. Baseline is `093bba6b`; candidate is the five-file borrowed-operand
patch on that revision before commit. Exact candidate source hashes, all samples
and recipe hash are in [allocation.json](allocation.json). These identities do
not describe an arbitrary later HEAD.

The [temporary recipe](BoundedStartsWithAllocationProbe.cs.txt) compiles once,
prepares a context and 128 distinct Unicode strings outside measurement, warms
four times and records eight samples using GC.GetAllocatedBytesForCurrentThread.
Both APIs must complete inline. The loop sums Boolean results from
text.startswith('😀',1,3); the exposed control first materializes the method and
uses identical bounds. No actual I/O, forced collection, custom tiering or
protection switches. Outputs, unwind, reservations and shared-budget refunds
are asserted. This isolates argument/method transport; it does not time a file job.

To reproduce, temporarily copy the recipe into the white-box project, build
Release, run only BoundedStartsWithAllocationProbe, archive evidence and remove
the temporary test. It is absent from the permanent suite. Independent semantic
coverage includes complete early-exit file search/report bytes, bound/argument
I/O, cancellation, terminal refusal, retained results and exact fuel traces.
No new benchmark framework or long lane is needed for this capture.
