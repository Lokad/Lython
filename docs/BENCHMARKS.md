# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Typed Unicode CSV meter | 4,096 records / 223,267 B | 2.789–2.855 ms | 3.139–3.173 ms | 0.879–0.910× | Diagnostic. [B1568]. |
| CSV meter with explicit `float()` | 4,096 records / 139,299 B | 2.876–2.896 ms | 2.920–2.924 ms | 0.984–0.992× | Diagnostic. [B1568]. |
| Short Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | 1.2223–1.2588 ms | 0.9300–0.9467 ms | 1.291–1.354× | Diagnostic. [B1575]. |
| Quote-heavy CSV policy invoice | 4,096 records / 174,147 B | 6.2566–6.2964 ms | 6.2693–6.3878 ms | 0.980–1.004× | Diagnostic. Spans parity. [B1575]. |
| Generated rules with captured locals | 2,048 rule groups | 1.5302–1.5510 ms | 0.7667–0.7943 ms | 1.926–2.023× | Diagnostic. [B1575]. |
| Generated rules with defaults | 2,048 rule groups | 1.4250–1.4631 ms | 0.6848–0.6872 ms | 2.074–2.137× | Diagnostic. [B1575]. |
| JSON Lines export with expanded options | 3,072 records / 189,441 B | 5.072–5.107 ms | 6.529–6.552 ms | 0.774–0.782× | Diagnostic. [B1568]. |

Ratios divide Lython elapsed time by CPython elapsed time. Above 1× means
Lython takes longer. Ranges span the four pairings of process medians from
two workers per engine. [B1575] measures frozen `531499f09`, with
runtime and tests matching `f17898c19`. The CSV meters and JSON
export come from frozen `95aa40601`, with runtime and tests matching
`8aef89b09`. Both collections ran on November 24, 2026 UTC and report
those revisions. [B1568].

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
| Typed Unicode CSV meter | Compile-to-result | 3.151–3.295 ms | 3.480–3.556 ms | 0.886–0.947× | Diagnostic. [B1568]. |
| Short Unicode word-count report with a nonlocal helper | Compile-to-result | 1.3674–1.3775 ms | 1.0831–1.0935 ms | 1.250–1.272× | Diagnostic. [B1575]. |
| Short Unicode word-count report with a nonlocal helper, 615 B | Compilation only | 0.02877–0.02900 ms | 0.05551–0.05574 ms | 0.516–0.522× | Diagnostic. [B1575]. |
| Generated rules with captured locals, 446 B | Compilation only | 0.02673–0.02718 ms | 0.04714–0.04720 ms | 0.566–0.577× | Diagnostic. [B1575]. |
| Generated rules with captured locals | Compile-to-result | 1.5749–1.5790 ms | 0.8204–0.8395 ms | 1.876–1.925× | Diagnostic. [B1575]. |
| Generated rules with defaults | Compile-to-result | 1.4840–1.5129 ms | 0.7290–0.7324 ms | 2.026–2.075× | Diagnostic. [B1575]. |
| Quote-heavy CSV policy invoice, 1,293 B | Compilation only | 0.05936–0.06033 ms | 0.09800–0.09855 ms | 0.602–0.616× | Diagnostic. [B1534]. |
| Quote-heavy CSV policy invoice | Compile-to-result | 6.539–6.644 ms | 6.799–6.825 ms | 0.958–0.977× | Diagnostic. [B1534]. |
| Configuration edit, 181 B | Compilation only | 0.01898–0.01912 ms | 0.03401–0.03426 ms | 0.554–0.562× | Diagnostic. [B1491]. |
| JSON Lines export with expanded options | Compile-to-result | 5.533–5.548 ms | 6.665–6.669 ms | 0.830–0.832× | Earlier diagnostic. [B1378]. |

Compilation includes disposal of compiled code owners. Time to result
combines compilation, one execution and cleanup in an already-running
host. Workers measure the phases independently. These clocks end before
final process teardown. The older invoice, configuration and JSON rows use
`0112138c1` and `2b501135a` from November 23, 2026 UTC and `a3338b0a8` from
November 22, 2026 UTC, respectively. [B1534] · [B1491] · [B1378].

## Fresh process

| Workload | Input | Lython median | CPython median | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Empty script | `pass` | 122.1 ms | 29.6 ms | 4.12× | Diagnostic control. [B1451]. |
| Tiny arithmetic | One calculation | 150.7 ms | 29.9 ms | 5.03× | Diagnostic control. [B1451]. |
| Configuration edit | One JSON file / 181 B | 278.0 ms | 32.2 ms | 8.62× | Diagnostic. [B1451]. |

At the earlier `dfabddb88` checkpoint on November 23, 2026 UTC, seven fresh
processes per row and engine measure launch through exit and pipe drain.
The clock includes runtime and worker initialization,
compilation, one run, applicable RAM-backed I/O and in-worker cleanup.
OS caches stay warm. Case order rotates. The smoke starts Lython first
within each pair. Ratios use observed medians. This describes the
comparison worker launch path. It is a separate measurement from using
Lython inside an already-running application. [B1451].

## Improvements within Lython

| Change | Measured effect | Limits |
| --- | --- | --- |
| Drain eager JSON inside the shared traversal | Three JSON Lines variants take 0.50–4.71% less warm elapsed. Default options use 0.68–6.03% less wider process CPU. | Allocation and admitted peaks are effectively flat. Both unchanged controls span zero. [B1586](../benchmarks/results/2026-11-24-json-traversal/README.md). |
| Cache regex wrappers before flag normalization | The module log scan takes 2.00–2.54% less warm elapsed and uses 0.71–3.71% less wider process CPU. | Admitted peaks match. Saved-bound and quoted-invoice elapsed span zero. [B1581](../benchmarks/results/2026-11-24-regex-wrapper-cache/README.md). |
| Reuse immutable ASCII results in decoded text reads | The short report and mixed-width histogram allocate 57,344 fewer CLR bytes per run, down 15.07% and 12.62%. Admitted peaks fall by 129 KiB each. | All warm elapsed ranges span zero. The nonblank and distinct-word controls retain the same allocation and admission. [B1580](../benchmarks/results/2026-11-24-decoded-ascii-file-values/README.md). |
| Return captured and conditional scalars before activation | Captured rules take 12.62–13.88% less warm elapsed and allocate 10.25% less. Defaulted rules reach their result 6.54–10.21% sooner with lower process CPU. | Admitted peaks match. Short-report warm elapsed rises 4.67–5.79% and quoted-invoice warm elapsed rises 0.17–2.05%. Annotated timing is inconclusive. [B1572](../benchmarks/results/2026-11-24-closed-scalar-branches/README.md). |
| Convert short Unicode decimals directly | The typed Unicode meter takes 12.09–16.88% less warm elapsed and 12.06–16.17% less process CPU. The site-observing variant also improves. | Allocation and admitted peaks match. Explicit-float warm elapsed rises 0.43–1.78% with mixed CPU. The quoted invoice spans zero on elapsed. [B1560](../benchmarks/results/2026-11-24-exact-unicode-float/README.md). |
| Reuse JSON option flags | The expanded JSON Lines export allocates 9.65% less and takes 3.21–3.53% less warm elapsed. | Admitted peaks and default allocation match. The expanded CPU and unchanged positional timing ranges span zero. [B1553](../benchmarks/results/2026-11-24-json-encoding-flags/README.md). |
| Native helper returns | Warm allocation falls 19.92% for CSV helpers and 19.31% for JSON helpers. | Release, stress and installed consumers qualified at `8f07764a`. The original elapsed improvements and slower text control remain diagnostic. [Source pair and qualification](../benchmarks/results/2026-11-02-native-function-returns/README.md). |
| Direct UTF-8 formatting/storage | Allocation falls 8.67%. The controlled report uses 5–8% less elapsed time and CPU. | The initial adverse elapsed result of 15.125 ms remains unexplained. [Initial result and confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md). |
| Construct native CSV records at their final capacity | Across two blocks, CSV CPU falls about 7–11% and allocation falls about 14–15%. | Plain CSV elapsed is flat in the first block. The text control is noisy. The revised constructor shipped in `8f07764a`. [Source pair and qualification](../benchmarks/results/2026-11-02-native-csv-records/README.md). |

Browse the [previous comparison tables](https://github.com/Lokad/Lython/blob/8db7b955e21b4489512862760d6b9c6b57a654d4/docs/BENCHMARKS.md)
for earlier workloads, revisions and intervals. The
[experiment index](../benchmarks/EXPERIMENTS.md) links to raw observations,
adverse results and design decisions. The [benchmark tools](../benchmarks/README.md)
document reproduction.

[B1491]: ../benchmarks/results/2026-11-23-forwarded-global-cpython/README.md
[B1568]: ../benchmarks/results/2026-11-24-runtime-checkpoint-cpython/README.md
[B1575]: ../benchmarks/results/2026-11-24-rule-activation-cpython/README.md
[B1534]: ../benchmarks/results/2026-11-23-owned-class-cpython/README.md
[B1451]: ../benchmarks/results/2026-11-23-runtime-cpython-checkpoint/README.md
[B1378]: ../benchmarks/results/2026-11-22-json-delivery-cpython/README.md
