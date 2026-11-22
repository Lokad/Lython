# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Typed ASCII CSV meter report | 4,096 records / 139,299 B | 2.991–3.067 ms | 2.491–2.512 ms | 1.191–1.231× | Diagnostic. Lython worker spread 2.53%. [B1351]. |
| CSV meter report with explicit `float()` | 4,096 records / 139,299 B | 4.460–4.664 ms | 2.903–2.907 ms | 1.534–1.607× | Diagnostic. Lython worker spread 4.58%. [B1336]. |
| Typed Unicode CSV meter report | 4,096 records / 223,267 B | 3.924–3.972 ms | 3.095–3.157 ms | 1.243–1.283× | Diagnostic. Lython worker spread 1.22%. [B1336]. |
| Quote-heavy CSV policy invoice | 4,096 records / 174,147 B | 7.227–7.301 ms | 6.267–6.280 ms | 1.151–1.165× | Diagnostic. Lython worker spread 1.02%. [B1327]. |
| CSV restock export | 3,072 records / 182,437 B | 3.759–3.907 ms | 5.102–5.129 ms | 0.733–0.766× | Historical diagnostic. [B1278]. |
| Map-based text report | 3,072 lines / 35,840 B | 1.440–1.495 ms | 1.217–1.220 ms | 1.181–1.228× | Historical diagnostic. [B1274]. |
| JSON Lines export with expanded options | 3,072 records / 189,441 B | 6.279–6.473 ms | 6.735–6.816 ms | 0.921–0.961× | Diagnostic. Lython worker spread 3.10%. [B1351]. |
| Mixed literal-default factory | 2,048 inputs / 4,096 definitions | 1.170–1.190 ms | 0.575–0.577 ms | 2.028–2.069× | Diagnostic. Lython worker spread 1.73%. [B1351]. |
| Generated validator with a generator | 96 guards / 1,714 B JSON | 0.07462–0.07479 ms | 0.03998–0.04031 ms | 1.851–1.871× | Diagnostic. [B1316]. |

Ratios divide Lython elapsed time by CPython elapsed time. Above 1× means
Lython takes longer. Ranges span the four pairings of process medians from
two workers per engine. The rows cover several source revisions. Each
linked checkpoint gives worker spread, batch variation and exact image IDs.

The quiet comparison machine is an Azure F4as_v7 running Ubuntu 24.04.4
with .NET 11 RC3. The external baseline is stock CPython 3.13.16. Warm
execution uses a compiled script in a running process. Timings include
applicable RAM-backed reads and writes, verification of output and files,
and disposal of the previous result. Lython uses a mediated host with
efficient ranged reads and appends and a shared allowance with accounting
active. CPython uses its native file APIs. Fixture reset and request
framing sit outside the elapsed execution clock. The raw process CPU
measurements cover the wider request.

## Compilation and compile-to-result

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Typed ASCII CSV meter report | Compilation only | 0.04166–0.04177 ms | 0.06754–0.06762 ms | 0.616–0.618× | Diagnostic. [B1336]. |
| Typed ASCII CSV meter report | Compile-to-result | 3.102–3.286 ms | 2.655–2.677 ms | 1.159–1.237× | Diagnostic. Lython worker spread 5.93%. [B1351]. |
| Mixed literal-default factory | Compilation only | 0.02445–0.02469 ms | 0.04317–0.04319 ms | 0.566–0.572× | Diagnostic. Lython worker spread 1.00%. [B1351]. |
| Mixed literal-default factory | Compile-to-result | 1.224–1.227 ms | 0.628–0.633 ms | 1.933–1.955× | Diagnostic. Lython worker spread 0.25%. [B1351]. |
| Generated validator with a generator | Compilation only | 1.519–1.534 ms | 1.813–1.825 ms | 0.832–0.846× | Diagnostic. [B1316]. |
| Generated validator with a generator | Compile-to-result | 1.575–1.597 ms | 1.965–1.967 ms | 0.801–0.813× | Diagnostic. [B1316]. |
| Postponed nested factories | Compilation only | 0.02269–0.02273 ms | 0.03937–0.03983 ms | 0.570–0.577× | Diagnostic. [B1307]. |
| Postponed nested factories | Compile-to-result | 1.183–1.295 ms | 0.576–0.588 ms | 2.014–2.247× | Uncertain magnitude. First-use Lython worker spread is 9.44%. [B1307]. |
| JSON normalization | Compilation only | 0.03536–0.03571 ms | 0.05254–0.05284 ms | 0.669–0.680× | Diagnostic. [B1252]. |
| JSON normalization | Compile-to-result | 2.771–2.801 ms | 2.813–3.007 ms | 0.922–0.996× | Inconclusive. Initial run spans parity and the repeat varies more. [B1253]. |

Compilation includes disposal of compiled code owners. Time to result
combines compilation, one execution and cleanup in an already-running
host. Workers measure the phases independently. Use time to result for
new scripts. Process startup and final teardown are excluded from these
clocks. The archived tables include the earlier startup evidence.

## Measured revisions

| Checkpoint | Lython source | Date |
| --- | --- | --- |
| [B1351] | `72e1131b9` | 2026-11-22 UTC |
| [B1345] | `6b5ae7c26` | 2026-11-22 UTC |
| [B1336] | `e961df912` | 2026-11-22 UTC |
| [B1327] | `318bf0e0a` | 2026-11-22 UTC |
| [B1316] | `8c81fb8d2` | 2026-11-22 UTC |
| [B1278] | `7cc150d96` | 2026-11-21 UTC |
| [B1274] | `b42c4d508` | 2026-11-21 UTC |
| [B1253] | `5901c06bd` | 2026-11-20 UTC |
| [B1307] | `4337b7765` | 2026-11-21 UTC |

## Improvements within Lython

| Change | Measured effect | Limits |
| --- | --- | --- |
| Use range enumerators directly in executable loops | A basic arithmetic loop takes 13.97–14.17% less warm time. In confirmation, the mixed factory takes 2.20–7.12% less time to result. | The initial factory warm timings and range materialization span zero. One unchanged warm control varies by 6.26% in confirmation. [B1350](../benchmarks/results/2026-11-22-direct-range-cursors/README.md). |
| Fuse regex range initialization and scalar projection | The ASCII group-text report uses 1.45–4.35% less warm CPU with the same adapter. | All Unicode timings span zero. Local allocation and admitted peaks match. [B1349](../benchmarks/results/2026-11-22-fused-regex-range-projection/README.md). |
| Transfer computed integers into captured bindings | The computed text report allocates 18.05% less locally and takes 2.51–4.57% less warm time on the quiet VM. | Text time to result and the fully supplied helper span zero. [B1348](../benchmarks/results/2026-11-22-computed-capture-stores/README.md). |
| Resolve executable jump targets on demand | Removes the eager block traversal and separate target array. Dataclass compilation uses 0.53–1.90% less CPU. | Validator time to result and dead-heavy elapsed span zero. [B1346](../benchmarks/results/2026-11-22-demanded-jump-targets/README.md). |
| Stage function defaults directly in executable instructions | The default observer uses 1.91–5.02% less warm CPU. Local compiler allocation falls by 304–424 bytes for affected fixtures. | Mixed time to result is 2.32–5.26% slower in the initial collection. A reversed-order confirmation spans zero with a moving unchanged control. [B1343](../benchmarks/results/2026-11-22-direct-default-instructions/README.md). |
| Parse complete short ASCII decimal tokens | Typed ASCII warm is 2.76–10.98% faster, explicit float 5.23–9.28% faster and typed ASCII time to result 5.33–8.38% faster. CPU falls in all three lanes. | Typed ASCII candidate worker spread is 7.86%. Unicode effect is uncertain, the integer invoice spans zero, and allocation is flat. [B1339](../benchmarks/results/2026-11-22-complete-decimal-ascii/README.md). |
| Read exact positional numeric extrema directly | Three meter reports run 9.08–15.74% faster warm and allocate 30.64–43.68% less locally. Typed ASCII time to result falls 13.81–16.43%. | The sync invoice takes 0.16–2.47% longer elapsed and its CPU ranges span zero. [B1335](../benchmarks/results/2026-11-22-numeric-extrema-operands/README.md). |
| Share compiled literal positional default tuples | Mixed factory time to result falls 10.71–14.42% and warm allocation falls 18.09%. | Dynamic-default and fully supplied timings span zero after refinement. The initial dynamic control was adverse. [B1326](../benchmarks/results/2026-11-22-compiled-literal-defaults-keywords/README.md). |
| Fill fixed CSV loop targets directly | Typed ASCII and explicit-float CSV reports take 4.95–7.04% and 2.63–5.81% less time warm, with lower process CPU. Local allocation falls 17.92% and 13.28%. | The sync invoice spans zero. The earlier async invoice regression does not recur. [B1325](../benchmarks/results/2026-11-22-csv-compiled-unpack/README.md). |
| Native helper returns | Warm allocation falls 19.92% for CSV helpers and 19.31% for JSON helpers. | Release, stress and installed consumers qualified at `8f07764a`. The original elapsed improvements and slower text control remain diagnostic. [Source pair and qualification](../benchmarks/results/2026-11-02-native-function-returns/README.md). |
| Direct UTF-8 formatting/storage | Allocation falls 8.67%. The controlled report uses 5–8% less elapsed time and CPU. | The initial adverse elapsed result of 15.125 ms remains unexplained. [Initial result and confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md). |
| Bulk CSV/shared-key/final-row construction | Selected refinements retain final row backing and bulk construction. The original prototype measured gains of roughly 6–8%. | Earlier source pairs and tradeoffs are in the [previous guide](../benchmarks/results/2026-11-17-current-cpython-checkpoint/prior-BENCHMARKS.md) and [history](../benchmarks/EXPERIMENTS.md). |

Earlier workloads, startup results and uncertainty remain available in
the [historical comparison tables](https://github.com/Lokad/Lython/blob/96253ff6799a2d2eaceb6ccd6439ee63a72f71d9/docs/BENCHMARKS.md).
Review individual designs and adverse observations in
the [experiment index](../benchmarks/EXPERIMENTS.md).
Reproduce the comparisons with the [benchmark tools](../benchmarks/README.md).

[B1351]: ../benchmarks/results/2026-11-22-post-range-cpython/README.md
[B1345]: ../benchmarks/results/2026-11-22-post-staging-cpython/README.md
[B1336]: ../benchmarks/results/2026-11-22-numeric-extrema-cpython/README.md
[B1327]: ../benchmarks/results/2026-11-22-retained-csv-cpython/README.md
[B1316]: ../benchmarks/results/2026-11-22-compiler-csv-cpython/README.md
[B1278]: ../benchmarks/results/2026-11-21-cpython-csv-restock-checkpoint/README.md
[B1274]: ../benchmarks/results/2026-11-21-cpython-casing-checkpoint/README.md
[B1252]: ../benchmarks/results/2026-11-20-cpython-json-normalization/README.md
[B1253]: ../benchmarks/results/2026-11-20-cpython-json-normalization/README.md
[B1307]: ../benchmarks/results/2026-11-21-cpython-retained-checkpoint/README.md
