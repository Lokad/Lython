# Ordinary byte-sum call diagnostic

Immutable `b2c91ba6` lets ordinary one-positional `sum(bytes)` use existing operand
storage and the shared native sum loop under both execution APIs. It removes
argument repackaging for this form and repeated immutable sum-signature creation.
The native algorithm, cancellation, integer admission and managed frame policy
remain unchanged; other argument forms and custom iterables keep general binding.

One 8.066-second capture on 2026-10-19 compares frozen `13375e82` and `b2c91ba6`
with isolated CPython 3.13.2 on shared Windows workstation LOKAD-0399. The existing
`scripts.file-manifest-helpers.large` job copies two files (4,107 bytes total),
computes checksums through an annotated helper, and writes a JSON manifest.

| Engine | Fresh compile and complete job | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `13375e82` | 4.294 ms | 21.43% | Before; diagnostic |
| Lython `b2c91ba6` | 4.616 ms | 16.63% | After; inconclusive change |
| CPython 3.13.2 | 4.661 ms | 10.65% | External context on this machine only |

The candidate median is 7.51% higher with substantial spread in all workers;
there is **no demonstrated speedup or qualified regression**. Its candidate/
CPython quotient, 0.990, is an unqualified point estimate. No allocation, warm-only
or embedding-latency claim follows. Keep the dedicated Linux VM snapshots in
[BENCHMARKS.md](../../../BENCHMARKS.md) as separate representative history.

Each invocation compiles afresh in a running host. Timing includes disk staging,
reset, complete file work, exact stdout/output-file checks and compilation/result
cleanup. Worker startup and protocol are excluded. Compilation alone is not
separately measured. Seven alternating rounds follow warmup, on x64 Windows build
26200, .NET 10.0.13, workstation GC, 28 logical processors. Output hashes match
before and after timing in every engine. The owned parent and workers exited.

Evidence: [raw samples and identities](micro.json), [owned process receipt](process.receipt.json),
[scope and hashes](scope.json), and the unchanged [shared catalog with sources,
fixtures and independent goldens](../2026-10-19-json-scalar-completion/catalog.json).
No new framework, full lane or repeated timing campaign was added.
