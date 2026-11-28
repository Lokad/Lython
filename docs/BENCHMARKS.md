# Performance against CPython

| Workload, warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Iterable-row shipment | 2,048 records / 27,350 B | 1.98382–1.98854 ms | 1.94462–1.96143 ms | 1.011–1.023× | Quiet diagnostic |
| Iterable-row shipment, Lython `RunAsync` | 2,048 records / 27,350 B | 2.19451–2.24686 ms | 1.95501–1.96270 ms | 1.118–1.149× | Quiet diagnostic |
| Nested JSON document summary | 2,048 records / 103,331 B | 0.64000–0.65729 ms | 0.67334–0.68318 ms | 0.937–0.976× | Quiet historical, `bfde55b6b`. [B1921](../benchmarks/results/2026-11-28-current-cpython-reader-checkpoint/README.md). |
| Nested JSON document summary, Lython `RunAsync` | 2,048 records / 103,331 B | 0.64055–0.64199 ms | 0.67123–0.67802 ms | 0.945–0.956× | Quiet diagnostic |
| File-fed CSV quantity map, Lython `RunAsync` | 2,048 records / 27,350 B | 0.40809–0.43424 ms | 1.37398–1.37725 ms | 0.296–0.316× | Quiet historical, `bfde55b6b`, 6.41% Lython elapsed spread. [B1921](../benchmarks/results/2026-11-28-current-cpython-reader-checkpoint/README.md). |
| File search, match at line 1 | 42,010 B | 0.06664–0.06685 ms | 0.05021–0.05086 ms | 1.310–1.331× | Quiet diagnostic |
| Compiled-pattern regex log search | 3,072 lines / 86,954 B | 2.78428–2.85304 ms | 0.95047–0.99712 ms | 2.792–3.002× | Quiet historical, `24218276c`. [B1914](../benchmarks/results/2026-11-28-current-cpython-24218276/README.md). |
| Module-level regex log search | 3,072 lines / 86,954 B | 3.08377–3.11461 ms | 1.59613–1.63767 ms | 1.883–1.951× | Quiet historical, `24218276c`. [B1914](../benchmarks/results/2026-11-28-current-cpython-24218276/README.md). |

B1926 measures Lython `486b6bb60` against stock CPython 3.13.16 on
November 28, 2026 UTC. The four-vCPU Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3. Ratios divide Lython elapsed by CPython elapsed.
Values above 1× mean Lython takes longer. Ranges span all pairings of
medians from two workers per engine. All five workload goldens and
168 timed batches verify. [B1926](../benchmarks/results/2026-11-28-current-cpython-compiler-checkpoint/README.md).

Warm execution uses compiled code in a running process. Elapsed includes
applicable RAM-backed I/O, output and file verification, and previous-result
disposal. Process CPU also includes fixture reset and framing. Lython uses
a mediated host and shared admission. CPython uses its native file APIs
with `-I -S -X utf8`. The historical regex rows use the optional PythonRe
0.5.0 adapter. [CPU and workload details](../benchmarks/results/2026-11-28-current-cpython-compiler-checkpoint/README.md).

## Compilation and compile-to-result

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Generated ordinary definitions, compile only | 96 definitions / 8,641 B source | 0.49597–0.50051 ms | 1.09063–1.09859 ms | 0.451–0.459× | Quiet diagnostic |
| Configuration edit, compile to result | 357 B source / 181 B input | 0.09525–0.09653 ms | 0.12228–0.12304 ms | 0.774–0.789× | Quiet diagnostic |

Compile-only times resident source and retiring previous compiled storage.
Compile-to-result also runs the script and includes applicable I/O and result
cleanup. Both use an already-running process. [B1926](../benchmarks/results/2026-11-28-current-cpython-compiler-checkpoint/README.md).

## Fresh process

| Workload | Input | Lython median | CPython median | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | 181 B input | 259.0 ms | 32.06 ms | 8.08× | `a159f24f2`, seven launch pairs, diagnostic |

The fresh worker clock spans launch through process exit and pipe drain,
including runtime initialization, compilation, one run, I/O and
in-process cleanup. OS caches stay warm. Lython runs first in every pair.
The supervisor validates each decoded reply after the timer stops.
Embedded use in an already-running process is covered by the warm and
compile-to-result rows. [B1906](../benchmarks/results/2026-11-27-current-cpython-a159f24/README.md).
A local phase diagnostic separates first compilation, execution and
benchmark reply work. [B1927](../benchmarks/results/2026-11-28-first-command-phases/README.md).

## Earlier measurements

| Workload, warm execution | Measured Lython revision | Input | Lython / CPython | Measurement status |
| --- | --- | --- | ---: | --- |
| File-fed CSV quantity map, Lython `Run` | `24218276c` | 2,048 records / 27,350 B | 0.293–0.299× | Quiet diagnostic. [B1914](../benchmarks/results/2026-11-28-current-cpython-24218276/README.md). |
| CSV shipment re-export with `DictWriter` | `6e59cd41e` | 2,048 records / 27,350 B | 0.635–0.642× | Quiet diagnostic. [B1869](../benchmarks/results/2026-11-27-csv-dictwriter-cpython/README.md). |
| Default JSON Lines export | `ab883268b` | 768 records / 47,361 B | 0.776–0.883× | Local WSL diagnostic. [B1880](../benchmarks/results/2026-11-27-wsl-current-cpython/README.md). |
| CSV summary via line generator, Lython `RunAsync` | `a1b3a3325` | 4,096 records / 57,361 B | 0.966–1.001× | Elapsed spans parity. [B1844](../benchmarks/results/2026-11-27-post-csv-json-cpython/README.md). |
| Compiled-pattern log search, PythonRe 0.5.0 | `4ab65dcc6` | 3,072 lines / 86,954 B | 2.576–2.629× | Local WSL diagnostic. [B1885](../benchmarks/results/2026-11-27-wsl-log-cpython/README.md). |
| Module-level log search, PythonRe 0.5.0 | `4ab65dcc6` | 3,072 lines / 86,954 B | 1.900–1.961× | Local WSL diagnostic. [B1885](../benchmarks/results/2026-11-27-wsl-log-cpython/README.md). |

B1906 includes the later-line file search, warm configuration edit and
generated-definition compile-to-result at `a159f24f2`. The previous guide
retains their absolute timings and the first-line comparison at that revision.
[Previous guide](../benchmarks/results/2026-11-28-current-cpython-24218276/previous-guide.md) ·
[B1906 checkpoint](../benchmarks/results/2026-11-27-current-cpython-a159f24/README.md).

## Improvements within Lython

| Change | Measured effect |
| --- | --- |
| Keep native slots across generator row callbacks | Iterable shipment allocates 131,128 B less per run. Local warm async CPU falls 3.33–5.76% on the same-image repeat. [B1910](../benchmarks/results/2026-11-27-private-generator-row-slots/README.md). |
| Drain generator rows into callback storage | Iterable shipment allocates 6.8% less in both APIs. Quiet sync elapsed falls 0.67–3.18%. Quiet async elapsed and CPU span zero. [B1905](../benchmarks/results/2026-11-27-generator-row-workspace/README.md). |
| Share the generator lookup table across function calls | Iterable shipment allocates 9.28–9.29% less at matching admission. Quiet CPU falls 6.28–10.99% sync and 5.32–7.96% async. The first bound async increase spans zero on repeat. [B1897](../benchmarks/results/2026-11-27-generator-definition-cache/README.md) · [B1898](../benchmarks/results/2026-11-27-bound-cache-owner-repeat/README.md). |
| Reuse shared JSON layouts across missing fields | Nested JSON Lines with alternating optional fields allocate 41.5% less and use 31.4% less peak admission. Dense document and file-fed CSV CPU span zero. [B1889](../benchmarks/results/2026-11-27-json-shared-prefix/README.md). |
| Collect lexical bindings directly into slots | Ordinary and escaping definitions each allocate 36,672 B less during compilation. Local ordinary compile CPU falls 2.65–4.54%. Function-free control CPU rises 0.63–2.11%. [B1925](../benchmarks/results/2026-11-28-direct-lexical-binding-slots/README.md). |
| Reuse lexical slots in local-name diagnostics | Ordinary and escaping definition compilation allocates 29,584 B less. Quiet ordinary compile CPU falls 2.82–4.14%. [B1900](../benchmarks/results/2026-11-27-local-assignment-slots/README.md). |

The [benchmark commands](../benchmarks/README.md) cover reproduction.
The [comparison protocol](../benchmarks/COMPARISON.md) defines the
qualification boundaries. The [experimental history](../benchmarks/EXPERIMENTS.md)
preserves adverse results and rejected trials.
