# B1351 Current factory, CSV and JSON Lines scripts against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython image was built from
`72e1131b9`. The external baseline is stock
CPython 3.13.16. The typed ASCII meter reads 4,096 records from
139,299 bytes of CSV. The mixed factory creates 4,096 functions for
2,048 inputs and uses no files. The JSON Lines export normalizes
3,072 records from a 189,441-byte input and writes 189,440 bytes.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Typed ASCII CSV meter report | Warm | 2.9913–3.0669 ms | 2.4906–2.5124 ms | 1.191–1.231× | 2.53 / 0.874% | 0.72% |
| Mixed literal-default factory | Warm | 1.1698–1.1900 ms | 0.5752–0.5769 ms | 2.028–2.069× | 1.73 / 0.301% | 0.59% |
| JSON Lines export with expanded options | Warm | 6.2787–6.4731 ms | 6.7348–6.8164 ms | 0.921–0.961× | 3.10 / 1.210% | 3.61% |
| Mixed literal-default factory | Compilation | 0.02445–0.02469 ms | 0.04317–0.04319 ms | 0.566–0.572× | 1.00 / 0.043% | 0.67% |
| Typed ASCII CSV meter report | Compile to result | 3.102–3.286 ms | 2.655–2.677 ms | 1.159–1.237× | 5.93 / 0.811% | 0.52% |
| Mixed literal-default factory | Compile to result | 1.224–1.227 ms | 0.628–0.633 ms | 1.933–1.955× | 0.25 / 0.923% | 1.19% |

Factory compilation takes 0.566–0.572× CPython's time. The same
factory takes 1.933–1.955× as long from compilation to result and
2.028–2.069× as long warm. CSV time to result varies by 5.93% between
Lython workers. Both engines' absolute medians moved since B1345.
The source-pair observations give the effect of direct range iteration.

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

All 168 timed batches and both quiet gates passed in
318 seconds. Stdout and output files matched their CPython goldens. The 32-file Lython image and both CPython worker identities
verified. Every worker joined and the persistent archive was checked
before releasing the VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Previous CPython checkpoint](../2026-11-22-post-staging-cpython/README.md) ·
[Direct range source pair](../2026-11-22-direct-range-cursors/README.md) ·
[ASCII parsing source pair](../2026-11-22-complete-decimal-ascii/README.md) ·
[Earlier JSON checkpoint](../2026-11-21-cpython-expanded-builtin/README.md) ·
[Image verification and procedures](procedures.tar.xz).
