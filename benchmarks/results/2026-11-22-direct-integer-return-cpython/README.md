# B1373 Direct integer returns against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython image from
`7d323d40f` matches the production runtime at
`e152d1ceb`.
The external baseline is stock CPython 3.13.16. The nested-helper
invoice reads 4,096 records from 127,031 input bytes. The
function-scoped helper has 2,048 calls. The mixed factory creates
4,096 functions for 2,048 inputs.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Nested-helper CSV invoice | Warm | 5.9260–5.9414 ms | 5.5584–5.5594 ms | 1.066–1.069× | 0.26 / 0.018% | 1.02% |
| Function-scoped arithmetic helper | Warm | 0.2344–0.2668 ms | 0.1319–0.1333 ms | 1.759–2.023× | 13.82 / 1.066% | 1.01% |
| Mixed literal-default factory | Compilation | 0.02439–0.02517 ms | 0.04307–0.04325 ms | 0.564–0.584× | 3.17 / 0.426% | 0.74% |
| Mixed literal-default factory | Compile to result | 1.046–1.060 ms | 0.623–0.625 ms | 1.674–1.702× | 1.39 / 0.247% | 0.64% |

Ratios divide Lython elapsed by CPython elapsed and span all pairings
of two process medians per engine. Above one means Lython takes longer.
Two workers per engine and phase warmed for at least eight seconds
and supplied seven rotated timed batches each. Controls show the spread
of the two process medians for each engine.

Warm execution includes RAM-backed input and output where applicable,
captured stdout, golden verification and prior-result disposal. Lython
uses host mediation for file operations. Time to result combines
compilation, execution and their cleanup. Process startup and final
teardown have separate boundaries.

All 112 timed batches and both quiet gates passed in
213 seconds. Stdout and output files matched
their CPython goldens. The 32-file Lython image and both CPython worker
identities verified. Every worker joined and the persistent archive
was checked before releasing the VM lease.

The mixed factory has no eligible direct return. Its wider CPU debt
in the paired source comparison stays open. This checkpoint measures
both compilation and time to result against CPython on the retained
runtime. It leaves the earlier source-pair comparison independent.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Previous call checkpoint](../2026-11-22-retained-call-transport-cpython/README.md) ·
[Direct return source pair](../2026-11-22-native-direct-integer-return-refined/README.md) ·
[Image verification and procedures](procedures.tar.xz).
