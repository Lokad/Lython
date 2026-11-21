# B1285 current factories against CPython

Measured November 21, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The measured Lython revision is `256c5e651`.
The external baseline is stock CPython 3.13.16. Each complete script
processes 2,048 inputs and creates 4,096 nested function definitions.
The factories bind live defaults and preserve annotations in their output.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Postponed nested factories | Warm | 1.1434–1.1628 ms | 0.5442–0.5456 ms | 2.096–2.137× | 1.69 / 0.259% | 1.13% |
| Postponed nested factories | Compile to result | 1.212–1.225 ms | 0.578–0.603 ms | 2.011–2.120× | 1.11 / 4.296% | 0.79% |
| Postponed nested factories | Compilation | 0.02261–0.02284 ms | 0.03970–0.03996 ms | 0.566–0.575× | 1.02 / 0.636% | 0.50% |
| Eager nested factories | Warm | 1.3336–1.3822 ms | 0.5772 ms | 2.310–2.395× | 3.64 / 0.004% | 1.59% |

Ratios divide Lython elapsed by CPython elapsed and span all pairings
of two process medians per engine. Above one means Lython takes longer.
Two workers per engine and phase warmed for at least eight seconds
and supplied seven rotated timed batches each. Controls show the spread
of the two process medians for each engine.

Warm execution includes captured stdout, golden verification and
prior-result disposal. These two scripts perform no guest filesystem I/O.
Compile-only timing includes code-owner cleanup. Time to result covers
both compilation and execution with their cleanup. Process startup and
final teardown have separate boundaries.

All 112 timed batches and both quiet gates passed in
211 seconds. Both outputs matched the CPython
goldens. The 32-file Lython image and both CPython worker identities
verified. Every worker joined and the persistent archive was checked
before releasing the VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz)
· [Image verification and procedures](procedures.tar.xz).
