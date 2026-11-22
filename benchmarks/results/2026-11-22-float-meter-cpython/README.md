# B1313 CSV meter reports against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython image is
`7d57b2f26`, with production-equivalent source at
`ac2728ce7`. The external baseline is stock
CPython 3.13.16. Each meter script reads 4,096 readings and writes a
summary for three sites. The ASCII input is 139,299 bytes. The
Unicode input is 223,267 bytes. The invoice control reads 4,096 records
from 127,031 bytes and writes the existing grouped report.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Typed ASCII CSV meter report | Warm | 4.4599–4.6552 ms | 2.5139–2.5535 ms | 1.747–1.852× | 4.38 / 1.577% | 0.92% |
| Typed ASCII CSV meter report | Compilation | 0.04265–0.04268 ms | 0.06745–0.06748 ms | 0.632–0.633× | 0.07 / 0.056% | 0.43% |
| Typed ASCII CSV meter report | Compile to result | 4.688–4.741 ms | 2.678–2.698 ms | 1.738–1.770× | 1.13 / 0.732% | 0.76% |
| CSV meter report with explicit float | Warm | 5.7828–5.9922 ms | 2.8706–2.9930 ms | 1.932–2.087× | 3.62 / 4.265% | 1.19% |
| Typed Unicode CSV meter report | Warm | 5.0186–5.2075 ms | 3.1340–3.1962 ms | 1.570–1.662× | 3.76 / 1.984% | 0.77% |
| Integer CSV invoice | Warm | 6.1850–6.1933 ms | 6.0798–6.0879 ms | 1.016–1.019× | 0.13 / 0.133% | 0.77% |

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
321 seconds. Stdout and the CSV report matched their CPython goldens. The 32-file Lython image and both CPython worker identities
verified. Every worker joined and the persistent archive was checked
before releasing the VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Internal source-pair comparison](../2026-11-22-direct-float-text/README.md)
· [Image verification and procedures](procedures.tar.xz).
