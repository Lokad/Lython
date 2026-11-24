# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Typed Unicode CSV meter | 4,096 records / 223,267 B | 2.789–2.855 ms | 3.139–3.173 ms | 0.879–0.910× | Diagnostic. [B1568]. |
| CSV meter with explicit `float()` | 4,096 records / 139,299 B | 2.876–2.896 ms | 2.920–2.924 ms | 0.984–0.992× | Diagnostic. [B1568]. |
| Short Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | 1.25140–1.30890 ms | 0.93864–0.94297 ms | 1.327–1.394× | Diagnostic. [B1597]. |
| Quote-heavy CSV policy invoice | 4,096 records / 174,147 B | 6.21401–6.25986 ms | 6.32403–6.34140 ms | 0.980–0.990× | Diagnostic. Near parity. [B1600]. |
| Eager annotated rules | 2,048 rule groups | 0.43634–0.47390 ms | 0.41110–0.41435 ms | 1.053–1.153× | Diagnostic. Worker spread up to 8.61%. [B1593]. |
| Postponed nested annotated rules | 2,048 rule groups | 0.60049–0.60089 ms | 0.53475–0.55209 ms | 1.088–1.124× | Diagnostic. [B1604]. |
| Generated rules with captured locals | 2,048 rule groups | 0.93583–0.98667 ms | 0.76178–0.77355 ms | 1.210–1.295× | Diagnostic. Lython worker spread 5.43%. [B1604]. |
| Generated rules with defaults | 2,048 rule groups | 0.97193–0.97791 ms | 0.68186–0.68939 ms | 1.410–1.434× | Diagnostic. [B1597]. |
| JSON Lines export with expanded options | 3,072 records / 189,441 B | 5.1299–5.2194 ms | 6.5146–6.7011 ms | 0.766–0.801× | Diagnostic. [B1587]. |

Ratios divide Lython elapsed time by CPython elapsed time. Above 1× means
Lython takes longer. Ranges span the four pairings of process medians from
two workers per engine. Captured rules and warm postponed rules use
frozen `d6437c26f`, matching `0400f185c`. Warm quoted invoice uses
`642c8f957`, matching `c40540135`. Defaulted rules and the warm short
report use `67be15bd0`, matching `70b04c0af`.
Eager annotated rules and the postponed compilation lanes use `4eb8c9559`,
matching `5313f9e71`. JSON export and the short-report compilation
lanes use `ccdf43a59`, matching `2cf721f73`.
The two CSV meters use `95aa40601`, matching `8aef89b09`. These
collections ran on November 24, 2026 UTC. [B1604] · [B1600] · [B1597] · [B1593] · [B1587] · [B1568].

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
| Short Unicode word-count report with a nonlocal helper | Compile-to-result | 1.2728–1.4254 ms | 1.0919–1.0922 ms | 1.165–1.305× | Diagnostic. Worker spread up to 11.99%. [B1587]. |
| Short Unicode word-count report with a nonlocal helper, 615 B | Compilation only | 0.02878–0.02890 ms | 0.05530–0.05561 ms | 0.518–0.523× | Diagnostic. [B1587]. |
| Eager annotated rules, 319 B | Compilation only | 0.01642–0.01643 ms | 0.03059–0.03088 ms | 0.532–0.537× | Diagnostic. [B1593]. |
| Eager annotated rules | Compile-to-result | 0.46870–0.47116 ms | 0.43951–0.44135 ms | 1.062–1.072× | Diagnostic. [B1593]. |
| Postponed nested annotated rules, 465 B | Compilation only | 0.02137–0.02188 ms | 0.03949–0.03962 ms | 0.539–0.554× | Diagnostic. [B1593]. |
| Postponed nested annotated rules | Compile-to-result | 0.60983–0.63550 ms | 0.57547–0.58786 ms | 1.037–1.104× | Diagnostic. [B1593]. |
| Generated rules with defaults, 427 B | Compilation only | 0.02384–0.02414 ms | 0.04129–0.04160 ms | 0.573–0.585× | Diagnostic. [B1597]. |
| Generated rules with defaults | Compile-to-result | 0.97730–0.97877 ms | 0.72738–0.73606 ms | 1.328–1.346× | Diagnostic. [B1597]. |
| Generated rules with captured locals, 446 B | Compilation only | 0.02672–0.02691 ms | 0.04689–0.04742 ms | 0.563–0.574× | Diagnostic. [B1604]. |
| Generated rules with captured locals | Compile-to-result | 0.95557–0.96751 ms | 0.82570–0.82767 ms | 1.155–1.172× | Diagnostic. [B1604]. |
| Quote-heavy CSV policy invoice, 1,293 B | Compilation only | 0.05936–0.06033 ms | 0.09800–0.09855 ms | 0.602–0.616× | Diagnostic. [B1534]. |
| Quote-heavy CSV policy invoice | Compile-to-result | 6.539–6.644 ms | 6.799–6.825 ms | 0.958–0.977× | Diagnostic. [B1534]. |
| Configuration edit, 181 B | Compilation only | 0.01898–0.01912 ms | 0.03401–0.03426 ms | 0.554–0.562× | Diagnostic. [B1491]. |
| JSON Lines export with expanded options | Compile-to-result | 5.2734–5.3220 ms | 6.6247–6.7093 ms | 0.786–0.803× | Diagnostic. [B1587]. |

Compilation includes disposal of compiled code owners. Time to result
combines compilation, one execution and cleanup in an already-running
host. Workers measure the phases independently. These clocks end before
final process teardown. The older invoice and configuration rows use
`0112138c1` and `2b501135a`, respectively, from November 23, 2026 UTC.
[B1534] · [B1491].

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
| Complete cased text helpers before workspace setup | The short Unicode report takes 24.78–27.37% less warm elapsed and 19.01–25.68% less time through result with lower CPU. Mixed-width warm elapsed falls 6.62–11.94%. | Target allocations and admission match. Distinct-word CPU and the inlined control are inconclusive. [B1608](../benchmarks/results/2026-11-24-closed-line-helper/README.md). |
| Complete captured pairs before the outer activation | Captured rules take 19.60–29.07% less warm elapsed and 27.18–31.68% less time through result, with lower CPU and matching allocation and admission. | Warm worker spread is 5.95–6.99%. Postponed warm elapsed rises 2.58–7.56% with higher CPU. Other control elapsed ranges cross zero. [B1603](../benchmarks/results/2026-11-24-closed-capture-pair/seeded/README.md). |
| Complete private closure calls at the definition | Captured rules take 17.56–18.41% less warm elapsed and 13.34–16.51% less time through result, with lower process CPU. CLR allocation falls 208 KiB and peak admission falls 448 KiB per run. | Postponed warm elapsed rises 1.41–5.00% and quoted-invoice elapsed rises 1.97–2.79%. Other warm elapsed ranges cross zero. [B1599](../benchmarks/results/2026-11-24-private-definition-call/isolation/README.md). |
| Return pairs from closed factories before workspace setup | Defaulted rules take 27.50–29.70% less warm elapsed and 30.05–32.30% less time through result, with lower process CPU in both phases. | Allocations and admitted peaks match. Captured-rule warm elapsed rises 2.17–3.61% with higher CPU. The other warm control ranges cross zero. [B1596](../benchmarks/results/2026-11-24-closed-factory-pairs/native-entry/README.md). |
| Reuse decoded arithmetic arguments during closed definitions | Eager annotated rules take 2.88–4.48% less warm elapsed and 3.01–4.66% less process CPU against the computed-default revision. | Allocation and admission match. The three-image postponed comparison spans zero and the rule and text controls move. [B1591](../benchmarks/results/2026-11-24-decoded-closed-defaults/README.md). |
| Evaluate closed defaults before entering a native workspace | Eager annotated rules take 36.64–43.34% less warm elapsed with lower CPU and 13.38% less CLR allocation. | Admitted peaks match. Postponed annotated elapsed rises 0.14–5.85% with mixed CPU. Other rule controls span zero. [B1590](../benchmarks/results/2026-11-24-closed-default-evaluation/README.md). |
| Use one eager JSON builder owner | Expanded and default options take 0.65–2.75% less warm elapsed and 0.63–3.09% less wider CPU. | Peak admission matches. Escape-heavy timing is inconclusive, the unchanged CSV control improves slightly and the text control is noisy. [B1588](../benchmarks/results/2026-11-24-eager-json-owner/README.md). |
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
[B1534]: ../benchmarks/results/2026-11-23-owned-class-cpython/README.md
[B1451]: ../benchmarks/results/2026-11-23-runtime-cpython-checkpoint/README.md
[B1587]: ../benchmarks/results/2026-11-24-shared-traversal-cpython/README.md
[B1593]: ../benchmarks/results/2026-11-24-annotated-factories-cpython/README.md
[B1597]: ../benchmarks/results/2026-11-24-closed-pairs-cpython/README.md
[B1604]: ../benchmarks/results/2026-11-24-closed-capture-cpython/README.md
[B1600]: ../benchmarks/results/2026-11-24-private-calls-cpython/README.md
