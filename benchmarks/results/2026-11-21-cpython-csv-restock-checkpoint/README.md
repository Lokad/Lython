# B1278 CSV restock exports against CPython

Measured November 21, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Lython image `9bf720efa` has the same
production source as `7cc150d96`. The external baseline is stock
CPython 3.13.16. Both engines read 3,072 records, filter and normalize
them, emit 2,958 rows and match the exact output file and totals.
[Measured Lython source pair](../2026-11-21-bulk-csv-field-output/README.md).

| Workload and boundary | Input | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: | ---: |
| Ordinary export, warm | 182,437 B | 3.759–3.907 ms | 5.102–5.129 ms | 0.733–0.766× |
| Ordinary export, compilation | Same script and fixture | 0.02747–0.02770 ms | 0.04536–0.04552 ms | 0.604–0.611× |
| Ordinary export, compile-to-result | 182,437 B | 3.961–3.967 ms | 5.221–5.225 ms | 0.758–0.760× |
| Escaped Unicode export, warm | 170,917 B | 4.507–4.525 ms | 5.714–5.798 ms | 0.777–0.792× |

Each phase uses two independent workers per engine, warmed for at least
eight seconds, with seven rotated batches per worker. Ratios cover all
pairings of process medians. Above one means Lython takes longer.
Ordinary warm medians differ by 3.92% within Lython and 0.52% within
CPython. The escaped export's spreads are 0.41% and 1.48%. The highest
sample IQR across all four lanes is 1.84%.

Warm execution includes mediated RAM-backed input and output, golden
observation and prior-result cleanup. Compile-to-result adds compilation
and compiled-owner cleanup. The compile-only boundary measures work
with a fresh code owner and its disposal. Startup and final process
teardown have separate boundaries. The escaped case was measured warm.

All 112 timed batches and both machine gates passed in 214 seconds.
Every worker joined and the persistent raw archive verified under the
VM lease. The collection uses frozen production images and records
diagnostic ratios for these workloads.

[Phase summary](summary.json) · [Raw observations](observations.tgz)
· [Image verification and procedures](procedures.tar.xz).
