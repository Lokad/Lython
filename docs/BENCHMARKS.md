# Performance against CPython

| Workload, warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Iterable-row shipment | 2,048 records / 27,350 B | 2.11862–2.16291 ms | 1.97177–1.97204 ms | 1.074–1.097× | Quiet diagnostic |
| Iterable-row shipment, Lython `RunAsync` | 2,048 records / 27,350 B | 2.40065–2.40647 ms | 1.96262–1.97993 ms | 1.212–1.226× | Quiet diagnostic |
| Nested JSON document summary | 2,048 records / 103,331 B | 0.77133–0.78416 ms | 0.67826–0.68660 ms | 1.123–1.156× | Quiet diagnostic |
| File-fed CSV quantity map | 2,048 records / 27,350 B | 0.39554–0.40183 ms | 1.36802–1.37413 ms | 0.288–0.294× | Quiet diagnostic |
| File search, match at line 1 | 42,010 B | 0.06670–0.06676 ms | 0.05033–0.05209 ms | 1.280–1.326× | Quiet diagnostic, CPU spans parity |
| File search, match at line 1,025 | 42,010 B | 0.07624–0.07629 ms | 0.13581–0.13637 ms | 0.559–0.562× | Quiet diagnostic |
| Configuration edit | 181 B input | 0.06962–0.07004 ms | 0.06173–0.06242 ms | 1.115–1.135× | Quiet diagnostic |

B1906 measures Lython `a159f24f2` against stock CPython 3.13.16 on
November 27, 2026 UTC. The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3.
Ratios divide Lython elapsed by CPython elapsed. Values above 1× mean
Lython takes longer. Ranges span the pairings of medians from two workers
per engine. All seven workload goldens and 280 warmed or compile batches
verify. [B1906](../benchmarks/results/2026-11-27-current-cpython-a159f24/README.md).

Warm execution starts with compiled code in a running process. It
includes applicable RAM-backed I/O, output and file verification, and
previous-result disposal. Lython uses a mediated host and active shared
memory admission. CPython uses its native file APIs with `-I -S -X utf8`.
Fixture reset and framing are outside elapsed and included in the
process CPU figures in the checkpoint. The first-line match takes about
15–16 µs longer in elapsed, with process CPU spanning parity. [B1906](../benchmarks/results/2026-11-27-current-cpython-a159f24/README.md).

## Compilation and compile-to-result

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Generated ordinary definitions, compile only | 96 definitions / 8,641 B source | 0.50878–0.51710 ms | 0.94729–0.95570 ms | 0.532–0.546× | Quiet diagnostic |
| Generated ordinary definitions, compile to result | 96 definitions / 8,641 B source | 0.70738–0.71531 ms | 1.12081–1.12491 ms | 0.629–0.638× | Quiet diagnostic |
| Configuration edit, compile to result | 357 B source / 181 B input | 0.09658–0.09868 ms | 0.12451–0.12738 ms | 0.758–0.793× | Quiet diagnostic |

Compile-only times resident source and retiring previous compiled
storage. Compile-to-result also runs the script and includes applicable
I/O and result cleanup. Both use an already-running process. [B1906](../benchmarks/results/2026-11-27-current-cpython-a159f24/README.md).

## Fresh process

| Workload | Input | Lython median | CPython median | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | 181 B input | 259.0 ms | 32.06 ms | 8.08× | Seven launch pairs, diagnostic |

The fresh worker clock spans launch through process exit and pipe drain,
including runtime initialization, compilation, one run, I/O and
in-process cleanup. OS caches stay warm. Lython runs first in every pair.
The supervisor validates each decoded reply after the timer stops.
Embedded use in an already-running process is covered by the warm and
compile-to-result rows. [B1906](../benchmarks/results/2026-11-27-current-cpython-a159f24/README.md).

## Earlier measurements

| Workload, warm execution | Measured Lython revision | Input | Lython / CPython | Measurement status |
| --- | --- | --- | ---: | --- |
| CSV shipment re-export with `DictWriter` | `6e59cd41e` | 2,048 records / 27,350 B | 0.635–0.642× | Quiet diagnostic. [B1869](../benchmarks/results/2026-11-27-csv-dictwriter-cpython/README.md). |
| Default JSON Lines export | `ab883268b` | 768 records / 47,361 B | 0.776–0.883× | Local WSL diagnostic. [B1880](../benchmarks/results/2026-11-27-wsl-current-cpython/README.md). |
| CSV summary via line generator, Lython `RunAsync` | `a1b3a3325` | 4,096 records / 57,361 B | 0.966–1.001× | Elapsed spans parity. [B1844](../benchmarks/results/2026-11-27-post-csv-json-cpython/README.md). |
| Compiled-pattern log search, PythonRe 0.5.0 | `4ab65dcc6` | 3,072 lines / 86,954 B | 2.576–2.629× | Local WSL diagnostic. [B1885](../benchmarks/results/2026-11-27-wsl-log-cpython/README.md). |
| Module-level log search, PythonRe 0.5.0 | `4ab65dcc6` | 3,072 lines / 86,954 B | 1.900–1.961× | Local WSL diagnostic. [B1885](../benchmarks/results/2026-11-27-wsl-log-cpython/README.md). |

The [archived guide](../benchmarks/results/2026-11-27-current-cpython-a159f24/previous-guide.md)
retains the earlier absolute timings, revisions and inconclusive results.

## Improvements within Lython

| Change | Measured effect |
| --- | --- |
| Drain generator rows into callback storage | Iterable shipment allocates 6.8% less in both APIs. Quiet sync elapsed falls 0.67–3.18%. Quiet async elapsed and CPU span zero. [B1905](../benchmarks/results/2026-11-27-generator-row-workspace/README.md). |
| Share the generator lookup table across function calls | Iterable shipment allocates 9.28–9.29% less at matching admission. Quiet CPU falls 6.28–10.99% sync and 5.32–7.96% async. The first bound async increase spans zero on repeat. [B1897](../benchmarks/results/2026-11-27-generator-definition-cache/README.md) · [B1898](../benchmarks/results/2026-11-27-bound-cache-owner-repeat/README.md). |
| Reuse shared JSON layouts across missing fields | Nested JSON Lines with alternating optional fields allocate 41.5% less and use 31.4% less peak admission. Dense document and file-fed CSV CPU span zero. [B1889](../benchmarks/results/2026-11-27-json-shared-prefix/README.md). |
| Reuse lexical slots in local-name diagnostics | Ordinary and escaping definition compilation allocates 29,584 B less. Quiet ordinary compile CPU falls 2.82–4.14%. [B1900](../benchmarks/results/2026-11-27-local-assignment-slots/README.md). |

The [benchmark commands](../benchmarks/README.md) cover reproduction.
The [comparison protocol](../benchmarks/COMPARISON.md) defines the
qualification boundaries. The [experimental history](../benchmarks/EXPERIMENTS.md)
preserves adverse results and rejected trials.
