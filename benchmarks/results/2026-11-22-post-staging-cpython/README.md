# B1345 Current factory, CSV and JSON Lines scripts against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython image is
`2aa1b17e2`, with production-equivalent source at
`6b5ae7c26`. The external baseline is stock
CPython 3.13.16. The typed ASCII meter reads 4,096 records from
139,299 bytes of CSV. The mixed factory creates 4,096 functions for
2,048 inputs and uses no files. The JSON Lines export normalizes
3,072 records from a 189,441-byte input and writes 189,440 bytes.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Typed ASCII CSV meter report | Warm | 3.2472–3.2639 ms | 2.6383–2.6867 ms | 1.209–1.237× | 0.51 / 1.835% | 1.02% |
| Mixed literal-default factory | Warm | 1.2399–1.2699 ms | 0.6053–0.6170 ms | 2.010–2.098× | 2.42 / 1.935% | 0.97% |
| JSON Lines export with expanded options | Warm | 6.5376–6.5604 ms | 7.0140–7.0747 ms | 0.924–0.935× | 0.35 / 0.865% | 3.08% |
| Mixed literal-default factory | Compilation | 0.02523–0.02559 ms | 0.04490–0.04530 ms | 0.557–0.570× | 1.43 / 0.888% | 2.60% |
| Typed ASCII CSV meter report | Compile to result | 3.177–3.203 ms | 2.703–2.713 ms | 1.171–1.185× | 0.83 / 0.371% | 2.14% |
| Mixed literal-default factory | Compile to result | 1.229–1.245 ms | 0.621–0.622 ms | 1.977–2.004× | 1.32 / 0.031% | 1.29% |

The factory's compilation is faster than CPython's, while reaching
the result takes about twice as long. The remaining gap is primarily
in execution. Compare matched source pairs to assess the effect of
an individual change. CPython's meter and JSON medians changed
between this run and the earlier checkpoints. The JSON report's
Lython medians overlap the earlier range.

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

All 168 timed batches and both quiet gates passed in 317 seconds.
Stdout and files matched CPython goldens. The 32-file Lython image
and both CPython worker identities verified. Workers joined and the
persistent archive verified before VM lease release.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Default staging source pair](../2026-11-22-direct-default-instructions/README.md) ·
[ASCII parsing source pair](../2026-11-22-complete-decimal-ascii/README.md) ·
[Earlier JSON checkpoint](../2026-11-21-cpython-expanded-builtin/README.md) ·
[Image verification and procedures](procedures.tar.xz).
