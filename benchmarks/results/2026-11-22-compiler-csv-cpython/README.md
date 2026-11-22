# B1316 Compiler and CSV checkpoint against CPython

Measured November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The frozen Lython image is
`d5abc29d4`, with production-equivalent source at
`8c81fb8d2`. The external baseline is stock
CPython 3.13.16. The validator reads 96 fields from a 1,714-byte
configuration and counts errors with a generator. The dataclass
script is 3,978 source bytes and needs no input file. Both CSV
reports process 4,096 data records. The ASCII meter input is
139,299 bytes and the quote-heavy invoice input is 174,147 bytes.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Controls L/C | Maximum IQR |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Generated validator with an error-counting generator | Compilation | 1.51938–1.53381 ms | 1.81280–1.82512 ms | 0.832–0.846× | 0.95 / 0.679% | 1.25% |
| Generated validator with an error-counting generator | Warm | 0.0746–0.0748 ms | 0.0400–0.0403 ms | 1.851–1.871× | 0.23 / 0.821% | 0.80% |
| Generated validator with an error-counting generator | Compile to result | 1.575–1.597 ms | 1.965–1.967 ms | 0.801–0.813× | 1.37 / 0.100% | 0.74% |
| Inherited dataclass configuration | Compilation | 0.17064–0.17213 ms | 0.43804–0.43880 ms | 0.389–0.393× | 0.87 / 0.173% | 0.92% |
| Typed ASCII CSV meter report | Warm | 4.1028–4.2314 ms | 2.5150–2.5183 ms | 1.629–1.682× | 3.14 / 0.129% | 0.55% |
| Quote-heavy CSV policy invoice | Warm | 7.2605–7.3663 ms | 6.2988–6.3573 ms | 1.142–1.169× | 1.46 / 0.929% | 1.03% |

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
321 seconds. Stdout and output files matched their CPython goldens. The 32-file Lython image and both CPython worker identities
verified. Every worker joined and the persistent archive was checked
before releasing the VM lease.

[Phase summary](summary.json) · [Raw observations](observations.tgz) ·
[Compiler source pair](../2026-11-22-sparse-lexical-preparation/README.md) ·
[CSV source pair](../2026-11-22-csv-quoted-ranges/README.md)
· [Image verification and procedures](procedures.tar.xz).
