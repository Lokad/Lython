# B1336 Retained numeric calls in CSV reports against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython image is
`b6dd01aee`, with production-equivalent source at
`e961df912`. The external baseline is stock
CPython 3.13.16. All three CSV reports process 4,096 data records.
The ASCII and explicit-float reports read 139,299 bytes each. The
Unicode report reads 223,267 bytes.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Typed ASCII CSV meter report | Warm | 3.3976–3.5515 ms | 2.4894–2.5151 ms | 1.351–1.427× | 4.53 / 1.032% | 1.54% |
| CSV meter report with explicit float | Warm | 4.4598–4.6640 ms | 2.9030–2.9075 ms | 1.534–1.607× | 4.58 / 0.152% | 2.73% |
| Typed Unicode CSV meter report | Warm | 3.9239–3.9715 ms | 3.0951–3.1571 ms | 1.243–1.283× | 1.22 / 2.002% | 0.99% |
| Typed ASCII CSV meter report | Compilation | 0.04166–0.04177 ms | 0.06754–0.06762 ms | 0.616–0.618× | 0.26 / 0.119% | 0.45% |
| Typed ASCII CSV meter report | Compile to result | 3.338–3.408 ms | 2.664–2.707 ms | 1.233–1.279× | 2.10 / 1.625% | 0.80% |

Ratios divide Lython elapsed by CPython elapsed and span all pairings
of two process medians per engine. Above one means Lython takes longer.
Two workers per engine and phase warmed for at least eight seconds
and supplied seven rotated timed batches each. Controls show the spread
of the two process medians for each engine.

Warm execution includes mediated RAM-backed input and output, captured
stdout, golden verification and prior-result disposal.
Compile-only timing includes code-owner cleanup. Time to result covers
both compilation and execution with their cleanup. Process startup and
final teardown have separate boundaries.

All 140 timed batches and both quiet gates passed in 268 seconds.
Stdout and output files matched their CPython goldens. The 32-file
Lython image and both CPython worker identities verified. Every worker
joined and the persistent archive verified before lease release.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Numeric call source pair](../2026-11-22-numeric-extrema-operands/README.md)
· [Prior meter checkpoint](../2026-11-22-retained-csv-cpython/README.md)
· [Image verification and procedures](procedures.tar.xz).
