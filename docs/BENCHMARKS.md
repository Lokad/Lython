# Performance against CPython

| Workload, warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Iterable-row shipment, Lython `RunAsync` | 2,048 records / 27,350 B | 2.14816–2.28583 ms | 1.93894–2.01075 ms | 1.068–1.179× | Quiet diagnostic, 6.41% Lython worker spread, B1963 |
| Ordinary helper and generator reduction | 2,048 records / 27,350 B | 2.30966–2.32132 ms | 1.96572–1.99783 ms | 1.156–1.181× | Quiet diagnostic, B1963 |
| Ordinary helper and generator reduction, Lython `RunAsync` | 2,048 records / 27,350 B | 2.44217–2.45655 ms | 1.98706–2.00274 ms | 1.219–1.236× | Quiet diagnostic, B1963 |
| Repeated explicit tuple loop, Lython `RunAsync` | 2,048 records / 27,350 B | 1.13926–1.14520 ms | 1.60000–1.61554 ms | 0.705–0.716× | Quiet diagnostic, B1963 |
| Markdown report from JSON, Lython `RunAsync` | 3,072 records / 189,441 B | 2.66007–2.78636 ms | 2.58856–2.61006 ms | 1.019–1.076× | Quiet diagnostic, 4.75% Lython worker spread, B1963 |
| Nested JSON document summary | 2,048 records / 103,331 B | 0.64000–0.65729 ms | 0.67334–0.68318 ms | 0.937–0.976× | Quiet historical, `bfde55b6b`. [B1921](../benchmarks/results/2026-11-28-current-cpython-reader-checkpoint/README.md). |
| Nested JSON document summary, Lython `RunAsync` | 2,048 records / 103,331 B | 0.65308–0.66108 ms | 0.67361–0.67902 ms | 0.962–0.981× | Quiet historical, B1932 |
| File-fed CSV quantity map, Lython `RunAsync` | 2,048 records / 27,350 B | 0.42442–0.42651 ms | 1.36275–1.37681 ms | 0.308–0.313× | Quiet diagnostic, B1963 |
| File search, match at line 1 | 42,010 B | 0.06676–0.06690 ms | 0.05110–0.05118 ms | 1.304–1.309× | Quiet historical, B1932 |
| File search, match at line 1, Lython `RunAsync` | 42,010 B | 0.06913–0.06951 ms | 0.04994–0.05086 ms | 1.359–1.392× | Quiet historical, `e036d6597`. [B1937](../benchmarks/results/2026-11-28-current-cpython-shipment/README.md). |
| Compiled-pattern regex log search | 3,072 lines / 86,954 B | 2.67783–2.71755 ms | 0.96541–0.97195 ms | 2.755–2.815× | Quiet historical, B1932 |
| Module-level regex log search | 3,072 lines / 86,954 B | 3.04883–3.09664 ms | 1.54657–1.56180 ms | 1.952–2.002× | Quiet historical, B1932 |

B1963 measures Lython `0b50e6fda` against stock CPython 3.13.16 on
November 28, 2026 UTC. The four-vCPU Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3. B1963 verifies five goldens and
196 timed batches. B1932 measures `2ef73bd71` and verifies seven goldens
and 224 batches. Ratios divide Lython elapsed by CPython elapsed.
Values above 1× mean Lython takes longer. Ranges span all pairings of
medians from two workers per engine. [B1963](../benchmarks/results/2026-11-28-ownership-cpython/README.md) ·
[B1932](../benchmarks/results/2026-11-28-current-cpython-regex-checkpoint/README.md).

Warm execution uses compiled code in a running process. Elapsed includes
applicable RAM-backed I/O, output and file verification, and previous-result
disposal. Process CPU also includes fixture reset and framing. Lython uses
a mediated host and shared admission. CPython uses its native file APIs
with `-I -S -X utf8`. The regex rows use the optional PythonRe 0.5.0
adapter. [Shipment and report CPU](../benchmarks/results/2026-11-28-ownership-cpython/README.md) ·
[JSON and regex CPU](../benchmarks/results/2026-11-28-current-cpython-regex-checkpoint/README.md).

## Compilation and compile-to-result

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Ordinary shipment, compile to result | 279 B source / 27,350 B input | 2.32491–2.34569 ms | 2.06318–2.06373 ms | 1.127–1.137× | Quiet diagnostic, B1963 |
| Generated ordinary definitions, compile only | 96 definitions / 8,641 B source | 0.50068–0.50556 ms | 0.94442–0.95310 ms | 0.525–0.535× | Quiet historical, B1932 |
| Configuration edit, compile to result | 357 B source / 181 B input | 0.09548–0.09575 ms | 0.12340–0.12556 ms | 0.760–0.776× | Quiet historical, B1932 |

Compile-only times resident source and retiring previous compiled storage.
Compile-to-result also runs the script and includes applicable I/O and result
cleanup. Both use an already-running process. [B1963](../benchmarks/results/2026-11-28-ownership-cpython/README.md) ·
[B1932](../benchmarks/results/2026-11-28-current-cpython-regex-checkpoint/README.md).

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
| Ordinary helper and generator reduction, Lython `RunAsync` | `09cecc9ce` | 2,048 records / 27,350 B | 1.323–1.355× | Quiet diagnostic. [B1958](../benchmarks/results/2026-11-28-post-generator-cpython/README.md). |
| Ordinary helper and generator reduction, Lython `RunAsync` | `2806d7d75` | 2,048 records / 27,350 B | 1.449–1.475× | Quiet diagnostic. [B1954](../benchmarks/results/2026-11-28-combined-loop-cpython/README.md). |
| Markdown report from JSON, Lython `RunAsync` | `2806d7d75` | 3,072 records / 189,441 B | 1.034–1.163× | Gap inconclusive at 11.23% CPython worker spread. [B1954](../benchmarks/results/2026-11-28-combined-loop-cpython/README.md). |
| Iterable-row shipment, Lython `Run` | `bf10eeaea` | 2,048 records / 27,350 B | 1.026–1.058× | Quiet diagnostic. [B1943](../benchmarks/results/2026-11-28-cpython-sequence-iterator-checkpoint/README.md). |
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
| Share frontend member facts and resolve each receiver once | First compilation allocates 17.48% less for configuration and 56.00% less for a UTF-16 codec script against the preceding Lython revision. A separate change reduces local compile CPU by 4.66–5.23% on a generated 48-helper script. Configuration compile-to-result CPU remains inconclusive. [Metadata](../benchmarks/results/2026-11-28-shared-builtin-member-facts/README.md) · [Receiver inference](../benchmarks/results/2026-11-28-one-member-return-receiver/README.md). |
| Bind fixed-width `sum` arguments on the stack | Ordinary tuple or range reductions each save about 64 KiB per script. Quiet async tuple CPU falls 2.16–3.65%. Range and file-fed CPU span zero. [B1959](../benchmarks/results/2026-11-28-fixed-sum-binding/README.md). |
| Keep the integer subtotal in its generator operand | Ordinary tuple or range reductions each save about 160 KiB and 2,048 allowance growth requests at matching peaks. Local async tuple CPU falls 2.46–3.80%. [B1960](../benchmarks/results/2026-11-28-borrowed-sum-subtotal/README.md). |
| Reduce private generator sums in admitted slots | Ordinary tuple and range reductions each save 480 KiB per script in both APIs. Local tuple CPU falls 8.17–14.88% sync and 16.18–20.12% async. [B1957](../benchmarks/results/2026-11-28-borrowed-generator-sum/README.md). |
| Select the `sum` pull before awaiting | Quiet warm file-fed async CPU falls 3.48–6.06% at matching admission. Ordinary reduction and sync timings remain inconclusive. [B1949](../benchmarks/results/2026-11-28-sum-source-selection/README.md). |
| Share frontend module member storage | First compilation allocates about 167 KB less for configuration and XML at matching admission. Quiet warm XML compile-to-result uses 1.51–2.19% less CPU. Fresh-command timing remains inconclusive. [B1947 and B1948](../benchmarks/results/2026-11-28-module-surface-storage/README.md). |
| Keep exact sequence positions in existing loop operands | Repeated tuple loops allocate 17.1% less at matching peak admission. Quiet sync CPU falls 6.13–9.57%, reversing the adverse WSL result. Quiet async CPU spans zero. [B1953](../benchmarks/results/2026-11-28-direct-loop-position/README.md). |
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
