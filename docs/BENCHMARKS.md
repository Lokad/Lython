# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Quote-heavy CSV policy invoice | 4,096 records / 174,147 B | 7.258–7.294 ms | 6.303–6.359 ms | 1.141–1.157× | Diagnostic. [B1419]. |
| Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | 1.479–1.572 ms | 0.977–0.980 ms | 1.510–1.609× | Diagnostic. Lython worker spread 6.29%. [B1419]. |
| JSON Lines export with expanded options | 3,072 records / 189,441 B | 5.565–5.657 ms | 6.767–6.868 ms | 0.810–0.836× | Diagnostic. [B1419]. |
| Postponed nested factories | 2,048 inputs / 4,096 definitions | 1.027–1.094 ms | 0.547–0.563 ms | 1.825–2.000× | Diagnostic. Lython worker spread 6.48%. [B1419]. |

Ratios divide Lython elapsed time by CPython elapsed time. Above 1× means
Lython takes longer. Ranges span the four pairings of process medians from
two workers per engine. The opening table measures the November 22, 2026
UTC checkpoint at `6ac96ea93`. Each linked checkpoint gives its exact image
IDs, worker spread and batch variation.

The direct Lython source pair from `32c50290d` to `55424591f` reproduces
higher warm factory time and a 14.91% spread between the later workers.
The same factory reaches its result 1.93–2.39% sooner when measured from
compilation. [Paired factory checkpoint](../benchmarks/results/2026-11-22-factory-checkpoint-source-pair/README.md).

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
| Sparse generated validator | Compilation only | 1.463–1.481 ms | 1.822–1.865 ms | 0.784–0.813× | Diagnostic. [B1419]. |
| Sparse generated validator | Compile-to-result | 1.545–1.569 ms | 1.964–1.968 ms | 0.785–0.799× | Diagnostic. [B1419]. |
| Postponed nested factories | Compile-to-result | 1.078–1.107 ms | 0.587–0.592 ms | 1.820–1.886× | Diagnostic. [B1419]. |
| Configuration edit | Compile-to-result | 0.1135–0.1140 ms | 0.1303–0.1304 ms | 0.871–0.875× | Diagnostic. [B1419]. |
| Typed ASCII CSV meter report | Compilation only | 0.04166–0.04177 ms | 0.06754–0.06762 ms | 0.616–0.618× | Diagnostic. [B1336]. |
| Typed ASCII CSV meter report | Compile-to-result | 3.102–3.286 ms | 2.655–2.677 ms | 1.159–1.237× | Diagnostic. Lython worker spread 5.93%. [B1351]. |
| Typed Unicode CSV meter report | Compilation only | 0.04218–0.04232 ms | 0.06772–0.06784 ms | 0.622–0.625× | Diagnostic. Lython worker spread 0.34%. [B1388]. |
| Typed Unicode CSV meter report | Compile-to-result | 3.799–3.876 ms | 3.298–3.439 ms | 1.104–1.175× | Diagnostic. CPython worker spread 4.29%. [B1388]. |
| Mixed literal-default factory | Compilation only | 0.02439–0.02517 ms | 0.04307–0.04325 ms | 0.564–0.584× | Diagnostic. Lython worker spread 3.17%. [B1373]. |
| Mixed literal-default factory | Compile-to-result | 1.046–1.060 ms | 0.623–0.625 ms | 1.674–1.702× | Diagnostic. Lython worker spread 1.39%. [B1373]. |
| Generated validator with a generator | Compilation only | 1.519–1.534 ms | 1.813–1.825 ms | 0.832–0.846× | Diagnostic. [B1316]. |
| Generated validator with a generator | Compile-to-result | 1.575–1.597 ms | 1.965–1.967 ms | 0.801–0.813× | Diagnostic. [B1316]. |
| Postponed nested factories | Compilation only | 0.02269–0.02273 ms | 0.03937–0.03983 ms | 0.570–0.577× | Diagnostic. [B1307]. |
| JSON Lines export with expanded options | Compilation only | 0.02552–0.02588 ms | 0.04435–0.04447 ms | 0.574–0.584× | Diagnostic. Lython worker spread 1.43%. [B1378]. |
| Configuration edit | Compilation only | 0.01946–0.01953 ms | 0.03399–0.03415 ms | 0.570–0.575× | Diagnostic. CPython worker spread 0.47%. [B1378]. |
| JSON Lines export with expanded options | Compile-to-result | 5.533–5.548 ms | 6.665–6.669 ms | 0.830–0.832× | Diagnostic. Lython worker spread 0.27%. [B1378]. |
| JSON normalization | Compilation only | 0.03536–0.03571 ms | 0.05254–0.05284 ms | 0.669–0.680× | Diagnostic. [B1252]. |
| JSON normalization | Compile-to-result | 2.771–2.801 ms | 2.813–3.007 ms | 0.922–0.996× | Inconclusive. Initial run spans parity and the repeat varies more. [B1253]. |

Compilation includes disposal of compiled code owners. Time to result
combines compilation, one execution and cleanup in an already-running
host. Workers measure the phases independently. Use time to result for
new scripts. Process startup and final teardown are excluded from these
clocks. The archived tables include the earlier startup evidence.

## Earlier warm execution checkpoints

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Typed ASCII CSV meter report | 4,096 records / 139,299 B | 3.034–3.049 ms | 2.488–2.522 ms | 1.203–1.226× | Diagnostic. Lython worker spread 0.49%. [B1361]. |
| CSV meter report with explicit `float()` | 4,096 records / 139,299 B | 4.460–4.664 ms | 2.903–2.907 ms | 1.534–1.607× | Diagnostic. Lython worker spread 4.58%. [B1336]. |
| Typed Unicode CSV meter report | 4,096 records / 223,267 B | 3.712–3.713 ms | 3.139–3.152 ms | 1.178–1.183× | Diagnostic. CPython worker spread 0.40%. [B1388]. |
| Ordinary CSV invoice | 4,096 records / 127,031 B | 6.163–6.246 ms | 6.092–6.213 ms | 0.992–1.025× | Diagnostic. Spans parity. [B1356]. |
| Nested-helper CSV invoice | 4,096 records / 127,031 B | 5.926–5.941 ms | 5.558–5.559 ms | 1.066–1.069× | Diagnostic. Lython worker spread 0.26%. [B1373]. |
| CSV restock export | 3,072 records / 182,437 B | 3.759–3.907 ms | 5.102–5.129 ms | 0.733–0.766× | Historical diagnostic. [B1278]. |
| Map-based text report | 3,072 lines / 35,840 B | 1.440–1.495 ms | 1.217–1.220 ms | 1.181–1.228× | Historical diagnostic. [B1274]. |
| JSON Lines export with explicit options | 3,072 records / 189,441 B | 5.432–5.470 ms | 6.340–6.367 ms | 0.853–0.863× | Diagnostic. Lython worker spread 0.71%. [B1378]. |
| Configuration edit | One JSON file / 181 B | 0.077 ms | 0.062 ms | 1.236–1.251× | Diagnostic. CPython worker spread 0.94%. [B1378]. |
| Mixed literal-default factory | 2,048 inputs / 4,096 definitions | 1.023–1.075 ms | 0.576–0.581 ms | 1.760–1.867× | Diagnostic. Lython worker spread 5.09%. [B1356]. |
| Fully supplied arithmetic helper, module scope | 2,048 calls | 0.3546–0.3587 ms | 0.2008–0.2117 ms | 1.675–1.787× | Diagnostic. CPython worker spread 5.46%. [B1361]. |
| Fully supplied arithmetic helper, function scope | 2,048 calls | 0.2344–0.2668 ms | 0.1319–0.1333 ms | 1.759–2.023× | Uncertain magnitude. Lython worker spread 13.82%. [B1373]. |
| Integer range sum | 100,000 iterations | 1.798–1.813 ms | 1.709 ms | 1.052–1.061× | Diagnostic loop. Lython worker spread 0.80%. [B1361]. |
| Generated validator with a generator | 96 guards / 1,714 B JSON | 0.07462–0.07479 ms | 0.03998–0.04031 ms | 1.851–1.871× | Diagnostic. [B1316]. |

The helper rows use different Lython revisions. The matched placement
diagnostic holds revisions fixed. Wrapping the loop in a function lowers
Lython elapsed by 11.81–14.46%. CPython improves more under the same
change. The checkpoint includes the arithmetic and allocation controls.
[Matched helper placement](../benchmarks/results/2026-11-22-matched-helper-placement/README.md).

## Measured revisions

| Checkpoint | Lython source | Date |
| --- | --- | --- |
| [B1419] | `6ac96ea93` | 2026-11-22 UTC |
| [B1406] | `55424591f` | 2026-11-22 UTC |
| [B1397] | `32c50290d` | 2026-11-22 UTC |
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
| Share diagnostic identity with executable code | Both nested factories allocate 3.34% less. The defaults observer allocates 2.47% less. Admitted peaks match. | Initial eager and postponed warm results move in opposite directions. The larger confirmation spans zero for both. [B1417 and B1418](../benchmarks/results/2026-11-22-function-diagnostic-identity/README.md). |
| Share native function call eligibility | The large positional control takes 1.16–3.10% less elapsed and 0.74–2.22% less process CPU. Admitted peaks match. | Postponed factory timing spans zero at both boundaries. The fully supplied helper allows a small slowdown. [B1415](../benchmarks/results/2026-11-22-native-call-selection-single-proof/README.md). |
| Remove captured delegates in the optional regex adapter | Named-capture reports allocate 5.01–5.50% less and the file rewrite allocates 3.10% less at matching admitted peaks. | The local comparison measures allocation only. [B1416](../benchmarks/results/2026-11-22-regex-adapter-operation-state/README.md). |
| Keep histogram totals in their numeric slots | The nonlocal report allocates 14.71% less at the same admitted peak. The ordinary histogram allocates 3.63% less and takes 2.38–10.26% less warm time. | Nonlocal elapsed spans zero at both boundaries and its warm worker spreads exceed 6%. [B1411](../benchmarks/results/2026-11-22-histogram-total-slots/README.md). |
| Reuse tokenizer indentation state | Removes the separate leading-source scan. A synthetic 64 KiB first-line literal compiles 13.46–22.39% faster. | Sparse validator compilation takes 0.15–1.37% longer and time to result spans zero. [B1405](../benchmarks/results/2026-11-22-module-leading-indentation/README.md). |
| Store a singleton default in its tuple | The postponed factory allocates 49,152 fewer bytes at the same admitted peak and runs 2.09–8.63% faster warm. | First-use elapsed and the tuple observer span zero. The quote-heavy control still allows an adverse result. [B1400](../benchmarks/results/2026-11-22-inline-default-tuple/README.md). |
| Consume prepared histogram words in place | The dense Unicode report runs 22.69–24.16% faster warm while allocations and admitted peak stay effectively flat. | Ordinary histogram, nonlocal warm and first-use elapsed span zero. [B1402](../benchmarks/results/2026-11-22-prepared-histogram-refined/README.md). |
| Pass regex provider state directly | Compiling 64 named rules allocates 5.36% less and runs 2.26–3.22% faster warm. File rewrites allocate 1.74% less. | The ASCII report runs 0.77–1.61% slower warm. The file rewrite takes 0.29–1.09% longer from compilation to result. [B1403](../benchmarks/results/2026-11-22-regex-provider-state/README.md). |
| Snapshot identical eager annotations once | The eager factory allocates 9.95% less, runs 9.51–13.12% faster warm and reaches its result 2.87–9.82% sooner. | Admitted peak stays equal. Postponed warm elapsed and the fully supplied helper span zero. [B1398](../benchmarks/results/2026-11-22-repeated-eager-annotations/README.md). |
| Admit compiled defaults together | Eager and postponed nested factories each make 2,048 fewer host growth requests, with matching allocation. Warm elapsed falls 1.00–3.94% and 1.09–3.71%. | The fully supplied helper spans zero. Postponed time to result spans zero with 8.03% candidate worker spread. [B1396](../benchmarks/results/2026-11-22-compiled-default-admission/README.md). |
| Merge compiler assignment facts from branch edits | The sparse validator compiles 5.16–7.61% faster and reaches its result 0.32–3.55% sooner. A generated branch stress fixture compiles 35.11–36.40% faster. | Dataclass compilation takes 0.67–2.38% longer. Postponed-factory time to result spans zero across workers. [B1395](../benchmarks/results/2026-11-22-compiler-branch-edits/README.md). |
| Borrow regex template literals and group decoded escapes | Long-literal expansion takes 21.47–22.23% less warm time and allocates 45.08% less locally. | Dense expansion spans zero and allocates 4.76% less. The positional control is adverse with 4.75% candidate worker spread. [B1394](../benchmarks/results/2026-11-22-regex-escape-runs/README.md). |
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

[B1419]: ../benchmarks/results/2026-11-22-callable-checkpoint-cpython/README.md
[B1406]: ../benchmarks/results/2026-11-22-current-source-cpython/README.md
[B1397]: ../benchmarks/results/2026-11-22-retained-source-cpython/README.md
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
