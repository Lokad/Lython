# B1369 Retained call transport against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython image from
`7a8df051a` matches the production runtime at `880c6d958`.
The external baseline is stock CPython 3.13.16. The two invoices
each read 4,096 records, from 127,031 and 174,147 input bytes
respectively. The function-scoped helper has 2,048 calls. The
mixed factory creates 4,096 functions for 2,048 inputs.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Nested-helper CSV invoice | Warm | 6.0378–6.0900 ms | 5.5313–5.5374 ms | 1.090–1.101× | 0.86 / 0.110% | 2.08% |
| Function-scoped arithmetic helper | Warm | 0.2916–0.3046 ms | 0.1289–0.1292 ms | 2.257–2.363× | 4.46 / 0.213% | 1.59% |
| Quoted policy invoice | Warm | 7.1974–7.4475 ms | 6.2554–6.2886 ms | 1.145–1.191× | 3.48 / 0.530% | 0.42% |
| Mixed literal-default factory | Compilation | 0.02415–0.02444 ms | 0.04320–0.04342 ms | 0.556–0.566× | 1.18 / 0.510% | 0.76% |
| Mixed literal-default factory | Compile to result | 1.068–1.097 ms | 0.617–0.624 ms | 1.712–1.780× | 2.70 / 1.201% | 1.02% |

Ratios divide Lython elapsed by CPython elapsed and span all pairings
of two process medians per engine. Above one means Lython takes longer.
Two workers per engine and phase warmed for at least eight seconds
and supplied seven rotated timed batches each. Controls show the spread
of the two process medians for each engine.

Warm execution includes RAM-backed input and output where applicable,
captured stdout, golden verification and prior-result disposal. Lython
uses host mediation for reads and writes. Time to result combines
compilation, execution and their cleanup. Process startup and final
teardown have separate boundaries.

All 140 timed batches and both quiet gates passed in 266 seconds.
Stdout and output files matched their CPython goldens. The 32-file
Lython image and both CPython worker identities verified. Every
worker joined and the persistent archive was checked before releasing
the VM lease.

Mixed first-use debt from the source comparison remains open. The
mixed tape has no unpack or dictionary store. The native function
bodies contain no call opcode. The new fixed-return and dictionary
branches do no additional guest work on that script. Its allocations
and peak allowance match in B1368. That leaves the elapsed and CPU
difference unexplained.

The first attempt stopped at the existing disk floor before staging
or sampling. The retry retired working copies of one older DLL only
after verifying every alias, the canonical archived core and all five
affected records. The inputs, toolchains and floor stayed the same.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Previous factory and CSV comparison](../2026-11-22-construction-csv-cpython/README.md) ·
[Native transport source pair](../2026-11-22-native-fixed-returns-refined/README.md) ·
[Image verification and procedures](procedures.tar.xz).
