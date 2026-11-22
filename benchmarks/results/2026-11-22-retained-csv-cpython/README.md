# B1327 Retained CSV and literal-default checkpoint against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython revision is `318bf0e0a`
and the external baseline is stock CPython 3.13.16. All three CSV
reports process 4,096 data records.
Both meter reports read 139,299 bytes. The quote-heavy invoice reads
174,147 bytes. The mixed factory creates 4,096 functions for 2,048 inputs
and uses no files.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Typed ASCII CSV meter report | Warm | 3.7987–3.8521 ms | 2.5255–2.5479 ms | 1.491–1.525× | 1.41 / 0.886% | 1.00% |
| CSV meter report with explicit float | Warm | 5.0411–5.0576 ms | 2.8424–2.8602 ms | 1.763–1.779× | 0.33 / 0.626% | 0.73% |
| Quote-heavy CSV policy invoice | Warm | 7.2270–7.3011 ms | 6.2669–6.2804 ms | 1.151–1.165× | 1.02 / 0.215% | 0.78% |
| Typed ASCII CSV meter report | Compilation | 0.04200–0.04221 ms | 0.06766–0.06773 ms | 0.620–0.624× | 0.49 / 0.101% | 1.05% |
| Typed ASCII CSV meter report | Compile to result | 3.940–4.021 ms | 2.689–2.703 ms | 1.458–1.496× | 2.05 / 0.531% | 0.58% |
| Mixed literal-default factory | Warm | 1.1788–1.2066 ms | 0.5757–0.5797 ms | 2.034–2.096× | 2.36 / 0.704% | 0.91% |
| Mixed literal-default factory | Compile to result | 1.245–1.271 ms | 0.622–0.626 ms | 1.988–2.042× | 2.12 / 0.586% | 0.89% |

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

All 196 timed batches and both quiet gates passed in
371 seconds. Stdout and output files matched their CPython goldens.
The 32-file Lython image and both CPython worker identities verified.
Every worker joined and the persistent archive was checked before
releasing the VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Literal defaults source pair](../2026-11-22-compiled-literal-defaults-keywords/README.md) ·
[CSV source pair](../2026-11-22-csv-compiled-unpack/README.md)
· [Image verification and procedures](procedures.tar.xz).
