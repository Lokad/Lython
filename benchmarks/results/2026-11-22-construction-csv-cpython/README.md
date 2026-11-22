# B1356 Current factory and CSV invoices against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython image was built from
`9cbcb2437`, with the same runtime and tests as production
`2f9f818b9`. The external baseline is stock
CPython 3.13.16. The mixed factory creates 4,096 functions for 2,048
inputs and uses no files. The invoices read 4,096 records and produce
155 output bytes from 127,031 and 174,147 input bytes respectively.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Mixed literal-default factory | Warm | 1.0234–1.0755 ms | 0.5761–0.5815 ms | 1.760–1.867× | 5.09 / 0.928% | 0.91% |
| Mixed literal-default factory | Compilation | 0.02429–0.02432 ms | 0.04320–0.04325 ms | 0.562–0.563× | 0.13 / 0.111% | 0.67% |
| Mixed literal-default factory | Compile to result | 1.094–1.102 ms | 0.623–0.628 ms | 1.743–1.770× | 0.73 / 0.837% | 0.93% |
| Ordinary CSV invoice | Warm | 6.1631–6.2455 ms | 6.0917–6.2134 ms | 0.992–1.025× | 1.34 / 1.997% | 2.29% |
| Quoted policy invoice | Warm | 7.2237–7.2985 ms | 6.2628–6.3355 ms | 1.140–1.165× | 1.04 / 1.162% | 0.62% |

Factory compilation takes 0.562–0.563× CPython's time, and time to
result takes 1.743–1.770×. The prior B1351 checkpoint reported
1.933–1.955× time to result. The ordinary invoice spans parity and
the quoted policy invoice takes 1.140–1.165× CPython's time. The
factory's warm spread is wider than its time-to-result spread.

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

All 140 timed batches and both quiet gates passed in
265 seconds. Stdout and output files matched their CPython goldens. The 32-file Lython image and both CPython worker identities
verified. Every worker joined and the persistent archive was checked
before releasing the VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Previous CPython checkpoint](../2026-11-22-post-range-cpython/README.md) ·
[Capture-construction source pair](../2026-11-22-native-single-capture-construction/README.md) ·
[ASCII parsing source pair](../2026-11-22-complete-decimal-ascii/README.md) ·
[Earlier JSON checkpoint](../2026-11-21-cpython-expanded-builtin/README.md) ·
[Image verification and procedures](procedures.tar.xz).
