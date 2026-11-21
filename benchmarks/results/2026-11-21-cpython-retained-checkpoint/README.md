# B1307 retained factories and CSV invoice against CPython

Measured November 21, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The measured Lython revision is
`4337b7765`. The external baseline is stock CPython
3.13.16. The factory scripts create 4,096 definitions for 2,048
inputs without guest file I/O. The invoice processes 4,096 data
records (127,031 B), applies two policy methods per row and writes
a grouped report.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Postponed nested factories | Warm | 1.1941–1.1974 ms | 0.5357–0.5513 ms | 2.166–2.235× | 0.27 / 2.906% | 0.71% |
| Postponed nested factories | Compilation | 0.02269–0.02273 ms | 0.03937–0.03983 ms | 0.570–0.577× | 0.16 / 1.179% | 1.52% |
| Postponed nested factories | Compile to result | 1.183–1.295 ms | 0.576–0.588 ms | 2.014–2.247× | 9.44 / 1.940% | 0.93% |
| Nested annotated factories | Warm | 1.3076–1.3543 ms | 0.5716–0.5751 ms | 2.274–2.369× | 3.57 / 0.617% | 0.72% |
| CSV invoice with policy methods | Warm | 7.2135–7.2859 ms | 6.0753–6.1221 ms | 1.178–1.199× | 1.00 / 0.770% | 1.57% |

Ratios divide Lython elapsed by CPython elapsed and span all pairings
of two process medians per engine. Above one means Lython takes longer.
Two workers per engine and phase warmed for at least eight seconds
and supplied seven rotated timed batches each. Controls show the spread
of the two process medians for each engine.

The postponed time-to-result lane has a 9.44% Lython control spread.
Its approximate magnitude remains uncertain. The earlier postponed
warm ratio was 2.096–2.137× in a separate collection on a different
revision. The difference from the latest 2.166–2.235× remains
unexplained. The controlled B1305 comparison isolates the keyword
storage change on its recorded Lython source pair.
[Earlier CPython comparison](../2026-11-21-cpython-current-factories/README.md) ·
[Keyword storage comparison](../2026-11-21-inline-keyword-default/README.md).

Warm execution includes mediated RAM-backed input and output, captured
stdout, golden verification and prior-result disposal.
Compile-only timing includes code-owner cleanup. Time to result covers
both compilation and execution with their cleanup. Process startup and
final teardown have separate boundaries.

All 140 timed batches and both quiet gates passed in
265 seconds. Stdout and the CSV report matched their CPython goldens. The 32-file Lython image and both CPython worker identities
verified. Every worker joined and the persistent archive was checked
before releasing the VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz)
· [Image verification and procedures](procedures.tar.xz) ·
[Separate factory admission diagnostic](factory-admission/README.md).
