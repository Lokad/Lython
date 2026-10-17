# Counted string replacement: allocation diagnostic

The immediate three-positional-argument call now reuses the existing direct
receiver operation. This capture measures Lython against earlier Lython; it has
no CPython allocation baseline and establishes no elapsed-time or RSS benefit.
The primary timing comparisons remain in [BENCHMARKS.md](../../../BENCHMARKS.md).

| Warm 128-call loop | Baseline allocated bytes | Candidate allocated bytes |
| --- | ---: | ---: |
| Immediate call, synchronous API | 73,664 | 40,896 |
| Immediate call, RunAsync | 80,808 | 40,872 |
| Exposed method, synchronous control | 69,576 | 69,576 |
| Exposed method, RunAsync control | 80,816 | 80,816 |

Measured 2026-10-17 UTC on an Intel Core i7-14700KF, Windows 10.0.26200 x64,
Release net10.0. Baseline is `61c96200`; the candidate is the four-file direct-call
patch applied to that revision before commit. Exact source hashes, every sample
and the recipe hash are in [allocation.json](allocation.json). These are historical
source identities, not measurements of an arbitrary later HEAD.

The [temporary diagnostic recipe](CountedReplaceAllocationProbe.cs.txt) compiles
once, prepares the context and 128 distinct UTF-8 receivers outside measurement,
warms four times and records eight samples with
`GC.GetAllocatedBytesForCurrentThread`. It measures the executable loop only.
Both APIs must complete inline; there is no actual I/O suspension. Exposed-method
controls use the same replacement/count and keep general binding. No forced GC,
custom tiering or protection switches are used. Outputs, frame unwind, reservations
and final shared-budget refunds are checked.

To reproduce, temporarily copy the recipe into the white-box test project, build
Release and run only `CountedReplaceAllocationProbe`. Archive the result and remove
the temporary test afterward. It is deliberately absent from the permanent suite.
This single short capture does not justify a new timing campaign or a CPython
speed ratio. Semantic qualification separately covers Python outputs, actual
argument/guest I/O, refusal, cancellation, retained results and existing fuel cuts.
