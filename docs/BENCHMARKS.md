# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Typed ASCII CSV meter report | 4,096 records / 139,299 B | 3.034–3.049 ms | 2.488–2.522 ms | 1.203–1.226× | Diagnostic. Lython worker spread 0.49%. [B1361]. |
| CSV meter report with explicit `float()` | 4,096 records / 139,299 B | 4.460–4.664 ms | 2.903–2.907 ms | 1.534–1.607× | Diagnostic. Lython worker spread 4.58%. [B1336]. |
| Typed Unicode CSV meter report | 4,096 records / 223,267 B | 3.712–3.713 ms | 3.139–3.152 ms | 1.178–1.183× | Diagnostic. CPython worker spread 0.40%. [B1388]. |
| Ordinary CSV invoice | 4,096 records / 127,031 B | 6.163–6.246 ms | 6.092–6.213 ms | 0.992–1.025× | Diagnostic. Spans parity. [B1356]. |
| Nested-helper CSV invoice | 4,096 records / 127,031 B | 5.926–5.941 ms | 5.558–5.559 ms | 1.066–1.069× | Diagnostic. Lython worker spread 0.26%. [B1373]. |
| Quote-heavy CSV policy invoice | 4,096 records / 174,147 B | 7.155–7.287 ms | 6.268–6.277 ms | 1.140–1.162× | Diagnostic. Lython worker spread 1.84%. [B1388]. |
| CSV restock export | 3,072 records / 182,437 B | 3.759–3.907 ms | 5.102–5.129 ms | 0.733–0.766× | Historical diagnostic. [B1278]. |
| Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | 1.573–1.585 ms | 0.931–0.953 ms | 1.652–1.702× | Diagnostic. CPython worker spread 2.29%. [B1388]. |
| Map-based text report | 3,072 lines / 35,840 B | 1.440–1.495 ms | 1.217–1.220 ms | 1.181–1.228× | Historical diagnostic. [B1274]. |
| JSON Lines export with expanded options | 3,072 records / 189,441 B | 5.438–5.455 ms | 6.515–6.623 ms | 0.821–0.837× | Diagnostic. CPython worker spread 1.66%. [B1378]. |
| JSON Lines export with explicit options | 3,072 records / 189,441 B | 5.432–5.470 ms | 6.340–6.367 ms | 0.853–0.863× | Diagnostic. Lython worker spread 0.71%. [B1378]. |
| Configuration edit | One JSON file / 181 B | 0.077 ms | 0.062 ms | 1.236–1.251× | Diagnostic. CPython worker spread 0.94%. [B1378]. |
| Mixed literal-default factory | 2,048 inputs / 4,096 definitions | 1.023–1.075 ms | 0.576–0.581 ms | 1.760–1.867× | Diagnostic. Lython worker spread 5.09%. [B1356]. |
| Fully supplied arithmetic helper, module scope | 2,048 calls | 0.3546–0.3587 ms | 0.2008–0.2117 ms | 1.675–1.787× | Diagnostic. CPython worker spread 5.46%. [B1361]. |
| Fully supplied arithmetic helper, function scope | 2,048 calls | 0.2344–0.2668 ms | 0.1319–0.1333 ms | 1.759–2.023× | Uncertain magnitude. Lython worker spread 13.82%. [B1373]. |
| Integer range sum | 100,000 iterations | 1.798–1.813 ms | 1.709 ms | 1.052–1.061× | Diagnostic loop. Lython worker spread 0.80%. [B1361]. |
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

The helper rows use different Lython revisions. The matched placement
diagnostic holds revisions fixed. Wrapping the loop in a function lowers
Lython elapsed by 11.81–14.46%. CPython improves more under the same
change. The checkpoint includes the arithmetic and allocation controls.
[Matched helper placement](../benchmarks/results/2026-11-22-matched-helper-placement/README.md).

## Compilation and compile-to-result

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Typed ASCII CSV meter report | Compilation only | 0.04166–0.04177 ms | 0.06754–0.06762 ms | 0.616–0.618× | Diagnostic. [B1336]. |
| Typed ASCII CSV meter report | Compile-to-result | 3.102–3.286 ms | 2.655–2.677 ms | 1.159–1.237× | Diagnostic. Lython worker spread 5.93%. [B1351]. |
| Typed Unicode CSV meter report | Compilation only | 0.04218–0.04232 ms | 0.06772–0.06784 ms | 0.622–0.625× | Diagnostic. Lython worker spread 0.34%. [B1388]. |
| Typed Unicode CSV meter report | Compile-to-result | 3.799–3.876 ms | 3.298–3.439 ms | 1.104–1.175× | Diagnostic. CPython worker spread 4.29%. [B1388]. |
| Mixed literal-default factory | Compilation only | 0.02439–0.02517 ms | 0.04307–0.04325 ms | 0.564–0.584× | Diagnostic. Lython worker spread 3.17%. [B1373]. |
| Mixed literal-default factory | Compile-to-result | 1.046–1.060 ms | 0.623–0.625 ms | 1.674–1.702× | Diagnostic. Lython worker spread 1.39%. [B1373]. |
| Generated validator with a generator | Compilation only | 1.519–1.534 ms | 1.813–1.825 ms | 0.832–0.846× | Diagnostic. [B1316]. |
| Generated validator with a generator | Compile-to-result | 1.575–1.597 ms | 1.965–1.967 ms | 0.801–0.813× | Diagnostic. [B1316]. |
| Postponed nested factories | Compilation only | 0.02269–0.02273 ms | 0.03937–0.03983 ms | 0.570–0.577× | Diagnostic. [B1307]. |
| Postponed nested factories | Compile-to-result | 1.183–1.295 ms | 0.576–0.588 ms | 2.014–2.247× | Uncertain magnitude. First-use Lython worker spread is 9.44%. [B1307]. |
| JSON Lines export with expanded options | Compilation only | 0.02552–0.02588 ms | 0.04435–0.04447 ms | 0.574–0.584× | Diagnostic. Lython worker spread 1.43%. [B1378]. |
| Configuration edit | Compilation only | 0.01946–0.01953 ms | 0.03399–0.03415 ms | 0.570–0.575× | Diagnostic. CPython worker spread 0.47%. [B1378]. |
| JSON Lines export with expanded options | Compile-to-result | 5.533–5.548 ms | 6.665–6.669 ms | 0.830–0.832× | Diagnostic. Lython worker spread 0.27%. [B1378]. |
| Configuration edit | Compile-to-result | 0.108 ms | 0.125–0.127 ms | 0.848–0.869× | Diagnostic. CPython worker spread 2.11%. [B1378]. |
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
| [B1388] | `d45f2ed21` | 2026-11-22 UTC |
| [B1378] | `a3338b0a8` | 2026-11-22 UTC |
| [B1373] | `e152d1ceb` | 2026-11-22 UTC |
| [B1369] | `880c6d958` | 2026-11-22 UTC |
| [B1361] | `eb227dcac` | 2026-11-22 UTC |
| [B1356] | `2f9f818b9` | 2026-11-22 UTC |
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
| Store prepared word lengths in the UTF-8 buffer | The 4,095-word dense line allocates 50.25% less with an 11.65% lower admitted peak. | Dense-line elapsed spans zero. The ordinary text results vary across trials and the quoted invoice takes 1.50–2.54% longer on confirmation. [B1391](../benchmarks/results/2026-11-22-inline-cased-words-confirmed/README.md). |
| Normalize short float tokens in one pass | The typed Unicode CSV meter takes 2.41–4.96% less time from compilation to result. | Warm timing spans zero. The quoted invoice takes 0.90–1.06% longer warm. [B1383](../benchmarks/results/2026-11-22-unicode-decimal-normalization/README.md). |
| Bind expanded JSON options directly in final slots | The expanded-options JSON Lines export takes 7.21–11.09% less warm time and allocates 17.84% less locally. | Explicit options and guest forwarding span zero elapsed. A repeat confirms 2.87–4.07% higher positional-call elapsed with stable workers. [B1376](../benchmarks/results/2026-11-22-json-direct-mapping/README.md) · [B1377](../benchmarks/results/2026-11-22-json-positional-control/README.md). |
| Keep the first JSON traversal frame in its encoder | Warm JSON Lines exports take 4.39–5.07% less time with expanded options and 6.92–7.64% less with explicit options. Local allocation falls 17.90% and 21.51%. | The nested manifest allocates 144 bytes more and its CPU range spans zero. The unchanged call control varies 7.26% across reference workers. [B1375](../benchmarks/results/2026-11-22-json-outer-frame/README.md). |
| Return checked integer expressions from supplied arguments | The function- and module-scoped helpers run 18.84–21.21% and 10.24–12.16% faster warm. The nested invoice runs 2.06–3.10% faster. | Allocation and admitted peaks are flat. Mixed first-use CPU is 1.06–4.66% higher, and elapsed spans zero. [B1372](../benchmarks/results/2026-11-22-native-direct-integer-return-refined/README.md). |
| Forward nested fixed tuple returns | The nested-helper CSV invoice runs 17.71–18.34% faster warm on the quiet VM and allocates 39.75% less locally. | The quoted invoice and function-scoped helper span zero. Mixed first-use CPU is 0.33–8.75% higher, with 5.82% reference-worker spread. [B1368](../benchmarks/results/2026-11-22-native-fixed-returns-refined/README.md). |
| Reuse a private augmented local across a call | The function-scoped helper runs 7.29–8.60% faster warm and allocates 45.89% less. The helper-driven CSV invoice allocates 2.10% less. | Invoice timing spans zero. Initial mixed-factory time to result is 2.05–2.66% slower. The follow-up has 14.35% reference-worker spread. [B1364 and B1365](../benchmarks/results/2026-11-22-checked-augmented-local/README.md). |
| Use direct signed overflow checks in the shared integer kernel | Mixed factory warm CPU falls 1.95–3.36%, and fully supplied helper warm CPU falls 0.82–2.70%. | Mixed time to result and range-loop elapsed span zero. Quoted-invoice elapsed rises 0.15–0.35%. [B1360](../benchmarks/results/2026-11-22-signed-integer-kernel/README.md). |
| Prepare integer continuations in the executable tape | The fully supplied helper uses 1.52–3.67% less warm CPU. The ordinary invoice uses 0.95–2.88% less. | Factory timing spans zero. The ASCII control varies 7.65% across reference workers. [B1359](../benchmarks/results/2026-11-22-prepared-integer-continuations/README.md). |
| Construct a native function's first local capture with the callable | The mixed factory takes 2.11–7.56% less warm time and 8.95–13.16% less time to result. | Local allocations and peaks are flat. Fully supplied, eager and postponed elapsed ranges span zero. [B1354](../benchmarks/results/2026-11-22-native-single-capture-construction/README.md). |
| Admit a proven lexical keyword default with its callable | The mixed factory takes 5.99–10.03% less warm time and 2.61–5.60% less time to result. Reversed-order time to result falls 5.87–7.89%. | Warm allocation is flat. Observer timing spans zero on confirmation. Eager and fully supplied controls remain uncertain. [B1352](../benchmarks/results/2026-11-22-direct-lexical-keyword-defaults/README.md). |
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

[B1388]: ../benchmarks/results/2026-11-22-unicode-decimal-cpython/README.md
[B1378]: ../benchmarks/results/2026-11-22-json-delivery-cpython/README.md
[B1373]: ../benchmarks/results/2026-11-22-direct-integer-return-cpython/README.md
[B1369]: ../benchmarks/results/2026-11-22-retained-call-transport-cpython/README.md
[B1361]: ../benchmarks/results/2026-11-22-machine-arithmetic-cpython/README.md
[B1356]: ../benchmarks/results/2026-11-22-construction-csv-cpython/README.md
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
