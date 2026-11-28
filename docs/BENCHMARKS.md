# Performance against CPython

| Workload, warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Iterable-row shipment | 2,048 records / 27,350 B | 2.01506–2.07370 ms | 1.97285–1.97362 ms | 1.021–1.051× | Quiet diagnostic, B1937 |
| Iterable-row shipment, Lython `RunAsync` | 2,048 records / 27,350 B | 2.20086–2.20510 ms | 1.93885–1.94706 ms | 1.130–1.137× | Quiet diagnostic, B1937 |
| Ordinary helper and generator reduction, Lython `RunAsync` | 2,048 records / 27,350 B | 3.13237–3.19689 ms | 1.97443–1.97768 ms | 1.584–1.619× | Quiet diagnostic, B1937 |
| Nested JSON document summary | 2,048 records / 103,331 B | 0.64000–0.65729 ms | 0.67334–0.68318 ms | 0.937–0.976× | Quiet historical, `bfde55b6b`. [B1921](../benchmarks/results/2026-11-28-current-cpython-reader-checkpoint/README.md). |
| Nested JSON document summary, Lython `RunAsync` | 2,048 records / 103,331 B | 0.65308–0.66108 ms | 0.67361–0.67902 ms | 0.962–0.981× | Quiet diagnostic, B1932 |
| File-fed CSV quantity map, Lython `RunAsync` | 2,048 records / 27,350 B | 0.42560–0.43160 ms | 1.35824–1.36216 ms | 0.312–0.318× | Quiet diagnostic, B1937 |
| File search, match at line 1 | 42,010 B | 0.06676–0.06690 ms | 0.05110–0.05118 ms | 1.304–1.309× | Quiet diagnostic, B1932 |
| File search, match at line 1, Lython `RunAsync` | 42,010 B | 0.06913–0.06951 ms | 0.04994–0.05086 ms | 1.359–1.392× | Quiet diagnostic, B1937 |
| Compiled-pattern regex log search | 3,072 lines / 86,954 B | 2.67783–2.71755 ms | 0.96541–0.97195 ms | 2.755–2.815× | Quiet diagnostic, B1932 |
| Module-level regex log search | 3,072 lines / 86,954 B | 3.04883–3.09664 ms | 1.54657–1.56180 ms | 1.952–2.002× | Quiet diagnostic, B1932 |

B1937 measures Lython `e036d6597` against stock CPython 3.13.16 on
November 28, 2026 UTC. The four-vCPU Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 for both checkpoints. B1937 verifies four goldens and
140 timed batches. B1932 measures `2ef73bd71` and verifies seven goldens
and 224 batches. Ratios divide Lython elapsed by CPython elapsed.
Values above 1× mean Lython takes longer. Ranges span all pairings of
medians from two workers per engine. [B1937](../benchmarks/results/2026-11-28-current-cpython-shipment/README.md) ·
[B1932](../benchmarks/results/2026-11-28-current-cpython-regex-checkpoint/README.md).

Warm execution uses compiled code in a running process. Elapsed includes
applicable RAM-backed I/O, output and file verification, and previous-result
disposal. Process CPU also includes fixture reset and framing. Lython uses
a mediated host and shared admission. CPython uses its native file APIs
with `-I -S -X utf8`. The regex rows use the optional PythonRe 0.5.0
adapter. [Shipment CPU and workload details](../benchmarks/results/2026-11-28-current-cpython-shipment/README.md) ·
[JSON and regex CPU](../benchmarks/results/2026-11-28-current-cpython-regex-checkpoint/README.md).

## Compilation and compile-to-result

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Generated ordinary definitions, compile only | 96 definitions / 8,641 B source | 0.50068–0.50556 ms | 0.94442–0.95310 ms | 0.525–0.535× | Quiet diagnostic |
| Configuration edit, compile to result | 357 B source / 181 B input | 0.09548–0.09575 ms | 0.12340–0.12556 ms | 0.760–0.776× | Quiet diagnostic |

Compile-only times resident source and retiring previous compiled storage.
Compile-to-result also runs the script and includes applicable I/O and result
cleanup. Both use an already-running process. [B1932](../benchmarks/results/2026-11-28-current-cpython-regex-checkpoint/README.md).

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

The previous checkpoint records absolute timing and CPU at `486b6bb60`. [B1926](../benchmarks/results/2026-11-28-current-cpython-compiler-checkpoint/README.md).

## Improvements within Lython

| Change | Measured effect |
| --- | --- |
| Remove the cursor inside exact list and tuple iterator values | Ordinary helper and generator reduction allocates about 164 KB less in both APIs at matching admission. Local warm CPU falls 3.80–5.45% async and 2.09–3.81% sync. File-fed map elapsed rises and its CPU spans zero. [B1942](../benchmarks/results/2026-11-28-direct-sequence-iterator/refined/README.md). |
| Pass the private generator frame to builtin `sum` | Ordinary helper and generator reduction allocates 7.28% less in both APIs at matching admission. Local warm async CPU falls 1.67–3.86%. Sync reduction is inconclusive, and the file-fed map control takes longer elapsed. [B1940](../benchmarks/results/2026-11-28-private-generator-sum/README.md). |
| Remove the private regex allowance monitor | The compiled log scan allocates 73,728 B less in each API. Local warm CPU falls 6.04–7.51% for the compiled log and 2.27–6.06% for the module log at matching admission. [B1938](../benchmarks/results/2026-11-28-private-regex-match-allowance/README.md). |
| Transfer the private generator frame to starmap | Iterable shipment allocates 6.2% less in both APIs at matching admission. Local warm CPU falls 2.68–9.27% async and rises 0.81–1.47% sync. The first-line control is inconclusive. [B1936](../benchmarks/results/2026-11-28-staged-generator-activation/README.md). |
| Keep native slots across generator row callbacks | Iterable shipment allocates 131,128 B less per run. Local warm async CPU falls 3.33–5.76% on the same-image repeat. [B1910](../benchmarks/results/2026-11-27-private-generator-row-slots/README.md). |
| Resolve scalar regex group selectors directly | Compiled log scans allocate 7.2% less and Unicode named-group scans allocate 22.6% less at the same peak admission. CPU gains remain inconclusive. [B1931](../benchmarks/results/2026-11-28-regex-group-resolution/README.md). |
| Drain generator rows into callback storage | Iterable shipment allocates 6.8% less in both APIs. Quiet sync elapsed falls 0.67–3.18%. Quiet async elapsed and CPU span zero. [B1905](../benchmarks/results/2026-11-27-generator-row-workspace/README.md). |
| Share the generator lookup table across function calls | Iterable shipment allocates 9.28–9.29% less at matching admission. Quiet CPU falls 6.28–10.99% sync and 5.32–7.96% async. The first bound async increase spans zero on repeat. [B1897](../benchmarks/results/2026-11-27-generator-definition-cache/README.md) · [B1898](../benchmarks/results/2026-11-27-bound-cache-owner-repeat/README.md). |
| Reuse shared JSON layouts across missing fields | Nested JSON Lines with alternating optional fields allocate 41.5% less and use 31.4% less peak admission. Dense document and file-fed CSV CPU span zero. [B1889](../benchmarks/results/2026-11-27-json-shared-prefix/README.md). |
| Share numeric call contracts | The cold numeric-method workload uses 5.94% less median process CPU locally. Configuration startup and warm numeric timing are inconclusive. [B1928](../benchmarks/results/2026-11-28-numeric-callable-catalog/README.md). |
| Collect lexical bindings directly into slots | Ordinary and escaping definitions each allocate 36,672 B less during compilation. Local ordinary compile CPU falls 2.65–4.54%. Function-free control CPU rises 0.63–2.11%. [B1925](../benchmarks/results/2026-11-28-direct-lexical-binding-slots/README.md). |
| Reuse lexical slots in local-name diagnostics | Ordinary and escaping definition compilation allocates 29,584 B less. Quiet ordinary compile CPU falls 2.82–4.14%. [B1900](../benchmarks/results/2026-11-27-local-assignment-slots/README.md). |

The [benchmark commands](../benchmarks/README.md) cover reproduction.
The [comparison protocol](../benchmarks/COMPARISON.md) defines the
qualification boundaries. The [experimental history](../benchmarks/EXPERIMENTS.md)
preserves adverse results and rejected trials.
