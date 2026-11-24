# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration manifest | Two input files / 833 B | 0.18590–0.18848 ms | 0.15368–0.15581 ms | 1.193–1.226× | Diagnostic. [B1643]. |
| Typed Unicode CSV meter | 4,096 records / 223,267 B | 2.789–2.855 ms | 3.139–3.173 ms | 0.879–0.910× | Diagnostic. [B1568]. |
| CSV meter with explicit `float()` | 4,096 records / 139,299 B | 2.876–2.896 ms | 2.920–2.924 ms | 0.984–0.992× | Diagnostic. [B1568]. |
| Short Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | 0.89681–0.92250 ms | 0.93302–0.94764 ms | 0.946–0.989× | Diagnostic. [B1643]. |
| Quote-heavy CSV policy invoice | 4,096 records / 174,147 B | 6.41158–6.48784 ms | 6.37358–6.39124 ms | 1.003–1.018× | Diagnostic. Near parity. [B1643]. |
| Eager annotated rules | 2,048 rule groups | 0.44264–0.46513 ms | 0.41287–0.42079 ms | 1.052–1.127× | Diagnostic. Worker spread up to 5.08%. [B1633]. |
| Postponed nested annotated rules | 2,048 rule groups | 0.60049–0.60089 ms | 0.53475–0.55209 ms | 1.088–1.124× | Diagnostic. [B1604]. |
| Generated rules with captured locals | 2,048 rule groups | 0.89205–0.89483 ms | 0.76652–0.77553 ms | 1.150–1.167× | Diagnostic. [B1633]. |
| Generated rules with defaults | 2,048 rule groups | 0.88961–0.90533 ms | 0.68258–0.69674 ms | 1.277–1.326× | Diagnostic. [B1643]. |
| JSON Lines export with expanded options | 3,072 records / 189,441 B | 5.1299–5.2194 ms | 6.5146–6.7011 ms | 0.766–0.801× | Diagnostic. [B1587]. |

Ratios divide Lython elapsed time by CPython elapsed time. Above 1× means
Lython takes longer. Ranges span the four pairings of process medians from
two workers per engine. B1643 measures `b9b20aa98` and refreshes the manifest,
short report, quoted invoice and defaulted rules in the phases marked in
the tables. Captured and eager annotated rules use `d6a168491`. The short
report's earlier compile-only row and the quoted invoice's earlier
compilation and time-to-result rows use `6466fbb52`. The earlier manifest
compile-only row and the sparse validator use `0bf516c79`. Postponed rules
use frozen `d6437c26f`, matching `0400f185c`, for warm execution and
`4eb8c9559`, matching `5313f9e71`, for compilation and time to result.
JSON export uses `ccdf43a59`, matching `2cf721f73`. The two CSV meters use
`95aa40601`, matching `8aef89b09`. These collections ran on November 24,
2026 UTC.
[B1643] · [B1633] · [B1625] · [B1614] · [B1604] · [B1593] · [B1587] · [B1568].

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
| Configuration manifest, 1,877 B of source | Compilation only | 0.11208–0.11333 ms | 0.16582–0.16676 ms | 0.672–0.683× | Diagnostic. [B1614]. |
| Configuration manifest | Compile-to-result | 0.38295–0.38747 ms | 0.46271–0.46322 ms | 0.827–0.837× | Diagnostic. [B1643]. |
| Generated comprehension rules, 17,995 B of source | Compilation only | 1.09478–1.10758 ms | 2.86768–2.89394 ms | 0.378–0.386× | Diagnostic. [B1643]. |
| Sparse generated validator, 23,555 B of source | Compilation only | 1.27724–1.28559 ms | 1.81308–1.82159 ms | 0.701–0.709× | Diagnostic compiler workload. [B1614]. |
| Sparse generated validator, 96 guards / 1,714 B input | Compile-to-result | 1.50615–1.52170 ms | 1.99316–2.00943 ms | 0.750–0.763× | Diagnostic compiler workload. [B1614]. |
| Typed Unicode CSV meter | Compile-to-result | 3.151–3.295 ms | 3.480–3.556 ms | 0.886–0.947× | Diagnostic. [B1568]. |
| Short Unicode word-count report with a nonlocal helper | Compile-to-result | 0.94947–1.03345 ms | 1.07671–1.08030 ms | 0.879–0.960× | Diagnostic. Worker spread up to 8.84%. [B1643]. |
| Short Unicode word-count report with a nonlocal helper, 615 B | Compilation only | 0.02850–0.02854 ms | 0.05601–0.05602 ms | 0.509–0.510× | Diagnostic. [B1625]. |
| Eager annotated rules, 319 B | Compilation only | 0.01629–0.01641 ms | 0.03060–0.03076 ms | 0.530–0.536× | Diagnostic. [B1633]. |
| Eager annotated rules | Compile-to-result | 0.48583–0.48941 ms | 0.43524–0.43622 ms | 1.114–1.124× | Diagnostic. [B1633]. |
| Postponed nested annotated rules, 465 B | Compilation only | 0.02137–0.02188 ms | 0.03949–0.03962 ms | 0.539–0.554× | Diagnostic. [B1593]. |
| Postponed nested annotated rules | Compile-to-result | 0.60983–0.63550 ms | 0.57547–0.58786 ms | 1.037–1.104× | Diagnostic. [B1593]. |
| Generated rules with defaults, 427 B | Compilation only | 0.02437–0.02474 ms | 0.04256–0.04315 ms | 0.565–0.581× | Diagnostic. [B1643]. |
| Generated rules with defaults | Compile-to-result | 0.93820–0.94499 ms | 0.72759–0.73066 ms | 1.284–1.299× | Diagnostic. [B1643]. |
| Generated rules with captured locals, 446 B | Compilation only | 0.02626–0.02666 ms | 0.04672–0.04684 ms | 0.561–0.571× | Diagnostic. [B1633]. |
| Generated rules with captured locals | Compile-to-result | 0.96229–0.97110 ms | 0.87069–0.87170 ms | 1.104–1.115× | Diagnostic. [B1633]. |
| Quote-heavy CSV policy invoice, 1,293 B | Compilation only | 0.06070–0.06112 ms | 0.10070–0.10165 ms | 0.597–0.607× | Diagnostic. [B1625]. |
| Quote-heavy CSV policy invoice | Compile-to-result | 6.44385–6.48525 ms | 6.51016–6.53856 ms | 0.986–0.996× | Diagnostic. [B1625]. |
| Configuration edit, 181 B | Compilation only | 0.01898–0.01912 ms | 0.03401–0.03426 ms | 0.554–0.562× | Diagnostic. [B1491]. |
| JSON Lines export with expanded options | Compile-to-result | 5.2734–5.3220 ms | 6.6247–6.7093 ms | 0.786–0.803× | Diagnostic. [B1587]. |

Compilation includes disposal of compiled code owners. Time to result
combines compilation, one execution and cleanup in an already-running
host. Workers measure the phases independently. These clocks end before
final process teardown. The small configuration edit uses `2b501135a` from
November 23, 2026 UTC. [B1491].

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
| Assign operand slots during forwarding | Compilation elapsed falls 2.04–5.30% across the classifier, sparse validator and manifest. Time to result falls 0.81–1.71% for the manifest and 2.95–3.30% for the validator. | Tape, allocation and peak admission match. Classifier and validator CPU through result cross zero. [B1653](../benchmarks/results/2026-11-24-terminal-backed-operand-layout/README.md). |
| Share structural-pattern capture bindings | The 48-case classifier allocates 10.47% less during compilation and reaches its result 8.05–8.84% sooner. Process CPU to result falls 7.54–10.83%. | Warm allocation and peak admission match. Sparse compile timing spans zero. Manifest process CPU rises 0.33–1.00% with inconclusive elapsed. [B1648](../benchmarks/results/2026-11-24-shared-match-bindings/README.md). |
| Reuse the proved default-pair boundary | Defaulted rules reach their result in 0.35–4.07% less elapsed time with 2.50–7.22% less wider CPU. | Allocation and admission match. Warm timing is inconclusive, and the earlier eager control slowdown remains open. [B1645–B1646](../benchmarks/results/2026-11-24-prepared-default-pair-boundaries/README.md). |
| Share compiler comprehension bindings | Generated rules compile 14.48–15.01% faster and use 11.34% less allocation. Time to result falls 5.97–7.09%, with lower CPU in both phases. | Retained admission matches. The sparse compiler control spans zero. The manifest takes 1.17–1.88% less time to result. [B1642](../benchmarks/results/2026-11-24-shared-comprehension-facts/README.md). |
| Carry forwarded integers through the existing native call entry | Against `0c907c166`, defaulted rules take 5.70–13.25% less warm elapsed and captured rules take 2.24–8.18% less, with lower CPU. Both allocate 43,680 fewer bytes per run. | Admitted peaks match. Against `cdaf31164` before both defaults and call changes, defaulted warm elapsed falls 3.85–8.11% and time to result spans zero. [B1631–B1632](../benchmarks/results/2026-11-24-borrow-forwarded-integers/README.md). |
| Forward local positional operands through native calls | The short Unicode report takes 10.79–13.48% less warm elapsed and captured rules take 0.54–7.64% less. Defaulted rules reach their result 1.88–4.34% sooner. | Peak admission matches. Eager annotated CPU rises 0.70–5.27%. Defaulted warm and quoted-invoice timings are inconclusive. [B1624](../benchmarks/results/2026-11-24-forwarded-positional/framed-entry/README.md). |
| Discard native JSON file write results before boxing | The configuration manifest allocates 3,104 fewer bytes per run, down 3.12%. The file-backed JSON summary saves 128 bytes. | Peak admission matches. Manifest and default JSON Lines timing span zero. [B1619](../benchmarks/results/2026-11-24-native-json-file-drain/typed-result/README.md). |
| Reuse compiler collection facts | The sparse validator's invalidation scans visit 14 bindings, down from 1,170. Immutable tuple diagnostics survive unrelated mutations. | Representative allocations match and CPU ranges span zero. [B1613](../benchmarks/results/2026-11-24-abstract-collection-graphs/README.md). |
| Share statement parsing and use its existing source spans | The sparse generated validator compiles in 3.10–5.04% less elapsed time and reaches its result 1.69–3.52% sooner, with lower CPU. | Compilation allocation and retained admission match. Manifest time-to-result CPU rises 0.79–2.98% while elapsed spans zero. [B1612](../benchmarks/results/2026-11-24-shared-parser-statements/README.md). |
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
[B1451]: ../benchmarks/results/2026-11-23-runtime-cpython-checkpoint/README.md
[B1587]: ../benchmarks/results/2026-11-24-shared-traversal-cpython/README.md
[B1593]: ../benchmarks/results/2026-11-24-annotated-factories-cpython/README.md
[B1604]: ../benchmarks/results/2026-11-24-closed-capture-cpython/README.md
[B1614]: ../benchmarks/results/2026-11-24-abstract-compiler-cpython/README.md
[B1625]: ../benchmarks/results/2026-11-24-forwarded-positional-cpython/README.md
[B1633]: ../benchmarks/results/2026-11-24-forwarded-integers-cpython/README.md
[B1643]: ../benchmarks/results/2026-11-24-frontend-cpython/README.md
