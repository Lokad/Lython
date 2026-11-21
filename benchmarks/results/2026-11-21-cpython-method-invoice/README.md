# B1298 CSV policy invoice against CPython

Measured November 21, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The measured Lython image is
`8f210d967`, production-equivalent to
`d51891009`. The external baseline is stock CPython
3.13.16. The script processes 4,096 data records from a 127,031-byte CSV
file, applies two policy method calls per row and writes a grouped CSV
report.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| CSV invoice with policy methods | Warm | 7.4025–7.4344 ms | 6.0537–6.1443 ms | 1.205–1.228× | 0.43 / 1.497% | 0.84% |
| CSV invoice with policy methods | Compilation | 0.05971–0.05991 ms | 0.09793–0.09849 ms | 0.606–0.612× | 0.34 / 0.572% | 0.55% |
| CSV invoice with policy methods | Compile to result | 7.469–7.556 ms | 6.416–6.480 ms | 1.153–1.178× | 1.17 / 0.997% | 0.72% |

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

All 84 timed batches and both quiet gates passed in 162 seconds.
Stdout and the report matched their CPython goldens. The 32-file Lython
image and both CPython worker identities verified. Every worker joined
and the persistent archive was checked before releasing the VM lease.
Input size counts logical CSV records, including those that span physical
lines. The procedures preserve the original physical-line assertion.

[Phase summary](summary.json) · [Raw observations](observations.tgz)
· [Image verification and procedures](procedures.tar.xz).
