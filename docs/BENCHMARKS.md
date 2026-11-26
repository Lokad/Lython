# Performance against CPython

| Workload / warm execution | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration manifest | Two input files / 833 B | 0.18510–0.18554 ms | 0.15478–0.15563 ms | 1.189–1.199× | Diagnostic. [B1669]. |
| Shipment callback report | 2,048 records / 27,350 B | 1.83262–1.91124 ms | 1.65572–1.67061 ms | 1.097–1.154× | Diagnostic. Lython worker spread is 4.29%. [B1764]. |
| Shipment callback report, Lython `RunAsync` | 2,048 records / 27,350 B | 1.98308–2.04042 ms | 1.63465–1.64184 ms | 1.208–1.248× | Diagnostic. [B1764]. |
| File-fed CSV quantity map, Lython `RunAsync` | 2,048 records / 27,350 B | 0.45152–0.48065 ms | 1.36416–1.36826 ms | 0.330–0.352× | Diagnostic. Lython worker spread is 6.45%. [B1760]. |
| CSV meter with a float field helper | 4,096 records / 139,299 B | 4.25497–4.31230 ms | 5.27953–5.28402 ms | 0.805–0.817× | Diagnostic. [B1760]. |
| File search with early exit | 1,025 lines read / 42,010 B file | 0.18709–0.18918 ms | 0.13516–0.13913 ms | 1.345–1.400× | Diagnostic. [B1750]. |
| Bound `starmap` shipment report, Lython `RunAsync` | 2,048 records / 27,350 B | 2.52384–2.56812 ms | 1.66237–1.71676 ms | 1.470–1.545× | Diagnostic. [B1750]. |
| Typed Unicode CSV meter | 4,096 records / 223,267 B | 2.789–2.855 ms | 3.139–3.173 ms | 0.879–0.910× | Diagnostic. [B1568]. |
| CSV meter with explicit `float()` | 4,096 records / 139,299 B | 2.876–2.896 ms | 2.920–2.924 ms | 0.984–0.992× | Diagnostic. [B1568]. |
| Short Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | 0.92415–0.99757 ms | 0.95457–0.96076 ms | 0.962–1.045× | Inconclusive around parity. Lython worker spread is 7.94%. [B1720]. |
| Quote-heavy CSV policy invoice | 4,096 records / 174,147 B | 6.19443–6.33377 ms | 6.30969–6.36204 ms | 0.974–1.004× | Inconclusive around parity. [B1720]. |
| Eager annotated rules | 2,048 rule groups | 0.43268–0.43905 ms | 0.40411–0.41482 ms | 1.043–1.086× | Diagnostic. [B1685]. |
| Postponed nested annotated rules | 2,048 rule groups | 0.61825–0.66753 ms | 0.53118–0.53396 ms | 1.158–1.257× | Diagnostic. Worker spread up to 7.97%. [B1685]. |
| Generated rules with captured locals | 2,048 rule groups | 0.89205–0.89483 ms | 0.76652–0.77553 ms | 1.150–1.167× | Diagnostic. [B1633]. |
| Generated ordinary definitions | 96 definitions, built 16 times | 0.22097–0.22361 ms | 0.14817–0.15315 ms | 1.443–1.509× | Diagnostic. [B1736]. |
| Generated rules with defaults | 2,048 rule groups | 0.89580–0.90843 ms | 0.68316–0.69232 ms | 1.294–1.330× | Diagnostic. [B1694]. |
| JSON service quantity totals | 2,048 records / 78,755 B | 0.66378–0.66619 ms | 0.48497–0.49106 ms | 1.352–1.374× | Diagnostic. [B1764]. |
| Formatted JSON report | 1,024 records / 189,441 B | 2.66299–2.77436 ms | 2.58657–2.58703 ms | 1.029–1.073× | Diagnostic. Worker spread up to 4.18%. [B1727]. |
| JSON Lines export with default options | 3,072 records / 189,441 B | 4.27718–4.35394 ms | 5.06345–5.15320 ms | 0.830–0.860× | Diagnostic. [B1720]. |
| JSON Lines export with expanded options | 3,072 records / 189,441 B | 5.1299–5.2194 ms | 6.5146–6.7011 ms | 0.766–0.801× | Diagnostic. [B1587]. |

Ratios divide Lython elapsed time by CPython elapsed time. Above 1× means
Lython takes longer. Ranges span the four pairings of process medians from
two workers per engine. B1764 measures `7f7c4ed59`. B1760 measures
`bc93d4d74`, B1750 measures `0a0e32c16` and B1744 measures `a490e9d5f`.
Those comparisons ran on November 26, 2026 UTC. [B1764] · [B1760] · [B1750] · [B1744].
B1736 measures `7f0c5b996` and B1727 measures
`857cc8001` on November 25, 2026 UTC. Earlier B1731 measures `a420c3e05`.
B1720 measures `7120ee55e` on November 25, 2026 UTC.
B1713 and B1705 measure earlier revisions `75af75769` and `96d54066a` the same
day. B1694 measures warm defaulted rules, the
bound shipment report and the manifest through result at `f6cf9796c`.
B1685 measures eager and postponed annotated warm execution at `c1984f96b`.
Other rows retain their original revisions. B1669 measures the
manifest at `a45b6da7f`. B1656 measures the short report and classifier
at `6b41db7b3`. Comprehension compilation uses `b9b20aa98`. Earlier
standalone eager and captured rule phases use `d6a168491`. The earlier
short report and invoice compile-only and invoice time-to-result phases
use `6466fbb52`. The earlier manifest compile-only and sparse validator
use `0bf516c79`. Postponed compilation and time to result use `5313f9e71`.
The expanded-options JSON export uses `2cf721f73`, and the CSV meters
use `8aef89b09`.
[B1736] · [B1731] · [B1727] · [B1720] · [B1713] · [B1705] · [B1694] · [B1685] · [B1677] · [B1669] · [B1656] · [B1643] · [B1633] · [B1625] · [B1614] · [B1593] · [B1587] · [B1568].

The quiet comparison machine is an Azure F4as_v7 running Ubuntu 24.04.4
with .NET 11 RC3. The external baseline is stock CPython 3.13.16. Warm
execution uses a compiled script in a running process. Timings include
applicable RAM-backed reads and writes, verification of output and files,
and disposal of the previous result. Marked async rows use Lython's
`RunAsync` and the same synchronous guest script and file APIs on CPython.
Lython uses a mediated host with
efficient ranged reads and appends and a shared allowance with accounting
active. CPython uses its native file APIs. Fixture reset and request
framing sit outside the elapsed execution clock. The raw process CPU
measurements cover the wider request.

## Compilation and compile-to-result

| Workload | Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration manifest, 1,877 B of source | Compilation only | 0.09717–0.09837 ms | 0.15754–0.16056 ms | 0.605–0.624× | Diagnostic. [B1744]. |
| Configuration manifest | Compile-to-result | 0.37382–0.37779 ms | 0.47152–0.47225 ms | 0.792–0.801× | Diagnostic. [B1744]. |
| File search with early exit, 319 B of source | Compile-to-result | 0.22328–0.22538 ms | 0.21057–0.21383 ms | 1.044–1.070× | Diagnostic. [B1750]. |
| Generated regex selectors, 48 calls / 197,820 B of source | Compilation only | 0.79275–0.80053 ms | 0.56563–0.56772 ms | 1.396–1.415× | Diagnostic. Lython includes constant-match analysis. [B1750]. |
| Generated regex selectors, 48 calls | Compile-to-result | 1.06505–1.07562 ms | 1.27062–1.27378 ms | 0.836–0.847× | Diagnostic. Both engines execute the regex calls. [B1750]. |
| Shipment callback report, 1,376 B of source | Compilation only | 0.06341–0.06400 ms | 0.11365–0.11404 ms | 0.556–0.563× | Diagnostic. [B1764]. |
| Shipment callback report | Compile-to-result | 2.09498–2.10661 ms | 1.90312–1.94081 ms | 1.079–1.107× | Diagnostic. Lython uses the synchronous API. [B1764]. |
| Generated classifier, 6,991 B of source | Compilation only | 0.32944–0.33057 ms | 0.81943–0.82016 ms | 0.402–0.403× | Diagnostic. [B1656]. |
| Generated comprehension rules, 17,995 B of source | Compilation only | 1.09478–1.10758 ms | 2.86768–2.89394 ms | 0.378–0.386× | Diagnostic. [B1643]. |
| Sparse generated validator, 23,555 B of source | Compilation only | 1.27724–1.28559 ms | 1.81308–1.82159 ms | 0.701–0.709× | Diagnostic compiler workload. [B1614]. |
| Sparse generated validator, 96 guards / 1,714 B input | Compile-to-result | 1.50615–1.52170 ms | 1.99316–2.00943 ms | 0.750–0.763× | Diagnostic compiler workload. [B1614]. |
| Typed Unicode CSV meter | Compile-to-result | 3.151–3.295 ms | 3.480–3.556 ms | 0.886–0.947× | Diagnostic. [B1568]. |
| Short Unicode word-count report with a nonlocal helper | Compile-to-result | 0.97879–0.98058 ms | 1.07920–1.08718 ms | 0.900–0.909× | Diagnostic. [B1656]. |
| Short Unicode word-count report with a nonlocal helper, 615 B | Compilation only | 0.02850–0.02854 ms | 0.05601–0.05602 ms | 0.509–0.510× | Diagnostic. [B1625]. |
| Eager annotated rules, 319 B | Compilation only | 0.01629–0.01641 ms | 0.03060–0.03076 ms | 0.530–0.536× | Diagnostic. [B1633]. |
| Eager annotated rules | Compile-to-result | 0.48583–0.48941 ms | 0.43524–0.43622 ms | 1.114–1.124× | Diagnostic. [B1633]. |
| Postponed nested annotated rules, 465 B | Compilation only | 0.02137–0.02188 ms | 0.03949–0.03962 ms | 0.539–0.554× | Diagnostic. [B1593]. |
| Postponed nested annotated rules | Compile-to-result | 0.60983–0.63550 ms | 0.57547–0.58786 ms | 1.037–1.104× | Diagnostic. [B1593]. |
| Generated ordinary definitions, 8,641 B of source | Compilation only | 0.57294–0.57500 ms | 1.10284–1.10975 ms | 0.516–0.521× | Diagnostic. [B1736]. |
| Formatted JSON report, 454 B of source | Compilation only | 0.02121–0.02138 ms | 0.03710–0.03734 ms | 0.568–0.576× | Diagnostic. [B1727]. |
| Formatted JSON report, 16 records / 2,961 B | Compile-to-result | 0.15713–0.15865 ms | 0.16563–0.17055 ms | 0.921–0.958× | Diagnostic. [B1727]. |
| Generated rules with defaults, 427 B | Compilation only | 0.02210–0.02213 ms | 0.04138–0.04167 ms | 0.530–0.535× | Diagnostic. [B1705]. |
| Generated ordinary definitions | Compile-to-result | 0.78635–0.80450 ms | 1.11361–1.12001 ms | 0.702–0.722× | Diagnostic. [B1736]. |
| Generated rules with defaults | Compile-to-result | 0.91836–0.95004 ms | 0.73616–0.75183 ms | 1.221–1.291× | Diagnostic. Worker spread up to 3.45%. [B1720]. |
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
| Use the existing integer path for exact boolean increments | Warm async shipment CPU falls 3.91–11.33% initially and 5.75–8.05% in the repeat. | Peak admission matches. Sync elapsed falls in both blocks and repeat sync CPU spans zero. The unchanged text control reverses direction. [B1766](../benchmarks/results/2026-11-26-post-update-shipment-profile/boolean-increments/confirmation/README.md). |
| Complete exact dictionary updates with integer field conversions | Warm CPU falls 6.98–15.28% on synchronous shipment, 2.06–12.98% on async shipment and 21.10–28.33% on JSON totals. The targets allocate 65,140–65,382 B less per run. | Peak admission matches. Both control CPU ranges span zero. The async shipment candidate workers differ by 10.18%. [B1763](../benchmarks/results/2026-11-26-converted-dictionary-update/README.md). |
| Complete eligible record-field conversions from supplied operands | Warm CPU falls 9.06–12.36% on file-fed map and 2.60–9.01% on the float-helper meter. | Allocation and peak admission match. Shipment control CPU rises 0.13–1.96% with zero uses of the changed path. [B1759](../benchmarks/results/2026-11-26-direct-record-field-returns/README.md). |
| Parse wide JSON integers without a CLR token string | The .NET 11 reports allocate 14.21% less for 40-digit values and 32.74% less for 4,096-digit values. The 40-digit report uses 1.19–3.53% less warm async CPU. | Peak admission matches. Timing for 4,096-digit values and the ordinary JSON control spans zero. Long inputs keep the CLR-string path on .NET 10. [B1751](../benchmarks/results/2026-11-26-wide-json-integer-parsing/README.md). |
| Return exact string affixes without a callee workspace | Protected file search uses 19.12–25.06% less process CPU and 23.17–29.17% less elapsed through result in confirmation. | Memory matches. The file-fed control reverses between blocks. Unicode text uses 2.74–4.57% more CPU in confirmation. [B1746/B1747](../benchmarks/results/2026-11-26-direct-affix-return/README.md). |
| Reuse compiled regex subjects | Both long literal cases allocate 16,496 B less during compilation. Two analysis preparations and one execution preparation share one 8,202 B backing. | Allocation diagnostic. Retained admission and three non-regex controls match. [B1740](../benchmarks/results/2026-11-25-compiled-regex-subject/README.md). |
| Admit buffered CSV fields and dictionary together | In the reordered block, file-fed map uses 10.00–13.40% less warm async CPU and shipment uses 4.00–7.43% less. | Quoted invoice and plain reader timing span zero. The text control also moves, and peak admission matches. [B1738c](../benchmarks/results/2026-11-25-combined-csv-admission/confirmation/README.md). |
| Forward comprehension bindings directly | The shipment callback report allocates 19,712 B less. Warm CPU falls 0.49–3.38% sync and 4.02–5.15% async. | Time through result falls 1.58–4.45%, with CPU spanning zero. File-fed map async CPU rises 1.20–4.39%. [B1737i](../benchmarks/results/2026-11-25-forwarded-comprehension-binding/isolation/README.md). |
| Defer unpublished lexical function construction | Generated ordinary definitions allocate 88.41% less and peak admission falls 90.45%. Warm CPU falls 14.80–18.19% sync and 14.08–15.55% async. | Time through result falls 5.03–7.75%. Compilation allocates 2,792 B more. The scope observer peaks 48 B higher and shipment timing spans zero. [B1735c](../benchmarks/results/2026-11-25-deferred-callable-locals/confirmation/README.md). |
| Share compiled regex text and narrow comprehension metadata | The 4,096-scalar regex literal uses one 8,192 B UTF-8 backing. Compilation allocates 8,280 B less on that case and 456–9,640 B less across three non-regex controls. | Local allocation diagnostics with matching retained admission. [B1734](../benchmarks/results/2026-11-25-shared-regex-literals/README.md). |
| Carry computed scalar arguments directly to eligible returns | Generated ordinary definitions use 11.33–16.83% less warm sync CPU and 14.86–17.75% less warm async CPU in confirmation. Elapsed through result falls 4.68–6.62%. | CLR allocation and admitted peaks match. Compile-only elapsed rises 0.20–0.78%. The escaping-rule control is inconclusive. [B1730c](../benchmarks/results/2026-11-25-arithmetic-call-operands/confirmation/README.md). |
| Share f-string field scanning | Report compilation allocates 680 B less after removing a second delimiter walk and the ordinary debug builder. Both comparisons use less CPU through result on the 16-record report. | Warm timing remains uncertain. [B1726](../benchmarks/results/2026-11-25-single-formatted-field-scan/confirmation/README.md). |
| Combine fixed admission for private function definitions | Generated ordinary definitions make 1,536 fewer allowance growth requests, and defaulted factories make 4,096 fewer. Both scripts show lower warm elapsed in two blocks. | CLR allocations and admitted peaks match. Movement in the unchanged controls limits timing attribution. [B1721](../benchmarks/results/2026-11-25-private-definition-admission/README.md). |
| Admit completed CSV storage directly | Quoted invoice allocates 262,144 B less and the file-fed map allocates 130,904 B less per run. File-fed map uses 1.70–5.13% less warm async CPU in the first block and 5.30–8.38% less in confirmation. | Peak admission matches. Quoted invoice CPU and the text-control movement remain unresolved. [B1719](../benchmarks/results/2026-11-25-csv-direct-record-admission/README.md). |
| Compile executable lambda defaults directly | Generated scripts with 48 defaulted lambdas allocate 28,288 B less per compilation. Live-alias compilation uses 1.48–4.16% less CPU in one block. | Retained compilation and peak execution admission match. Compile-to-result CPU remains inconclusive. [B1716](../benchmarks/results/2026-11-25-executable-lambda-headers/README.md). |
| Bind fixed-width math calls into managed stack slots | Tolerance reports allocate 56 B less per call and use 8.52–14.48% less warm CPU against the interim direct binder. | Against the pre-interim runtime, short-call sync timing remains inconclusive and async CPU falls 0.06–2.98%. [B1715](../benchmarks/results/2026-11-25-borrowed-builtin-bindings/README.md). |
| Run short callbacks in iterator-owned slots | File-fed map uses 6.26–13.95% less warm sync CPU and removes 2,048 private workspace starts per run in both hosting modes. | Peak admission matches. Sync reference-worker spread is 6.19%. Async magnitude and the controls remain uncertain. [B1708b](../benchmarks/results/2026-11-25-iteration-native-storage/prepared/README.md). |
| Finish buffered CSV reads inline | File-fed map uses 1.18–6.46% less warm async CPU in the first block and 1.37–3.29% less in confirmation. | Admitted peaks match. Quote-heavy invoice CPU is inconclusive, and the text control reverses direction between blocks. [B1704](../benchmarks/results/2026-11-25-csv-completed-read/README.md). |
| Parse conjunctions and disjunctions in one pass | Sparse guards take 1.00–2.98% less time through result with lower wider CPU. Manifest compilation takes 0.31–1.32% less elapsed. | Executable spans and admitted peaks match. Mixed-guard time through result and warm invoice timing span zero. [B1693](../benchmarks/results/2026-11-25-boolean-linear/README.md). |
| Start short async `map` continuations at suspension | Original shipment uses 9.01–11.21% less warm async CPU and the file-fed report uses 1.61–7.50% less. Bound defaulted readings use 17.95–24.60% less. | Admitted peaks match. The sync shipment and defaulted through-result clocks cross zero. The unchanged sync invoice moves favorably. [B1692](../benchmarks/results/2026-11-25-async-map-prefix/README.md). |
| Keep the executable directly on the compiled script | Six warm cases allocate 32 B less per invocation. Defaulted rules use 1.90–4.91% less CPU through result in the quiet comparison. | Most elapsed ranges cross zero. Manifest compile-only CPU rises 0.57–3.47%. [B1689](../benchmarks/results/2026-11-25-compiled-entry/README.md). |
| Share short `map` and `starmap` callback operands | The bound shipment report allocates 34.13% less and uses 8.06–9.59% less warm CPU and 16.13–17.02% less async CPU. A bound defaulted `map` over 4,096 readings allocates 94.92% less and uses 49.61–51.72% less warm CPU. | The original unbound shipment report uses 0.99–4.04% more async CPU. Defaulted-rule time through result is inconclusive. [B1687](../benchmarks/results/2026-11-25-closed-iteration-call/README.md). |
| Keep singleton default ownership in its operand slot | Ordinary and eager definitions allocate 19.69% and 20.16% less against `a39d00228`. Confirmation uses 3.50–7.80% and 3.97–5.87% less warm CPU. | Admitted peaks and default-free allocation match. Defaulted-rule CPU increases in the first run and spans zero in the repeat. [B1682](../benchmarks/results/2026-11-25-tagged-default-ownership/README.md). |
| Carry operand slots through short `map` callbacks | A nested scalar report over 4,096 readings allocates 96.66% less and uses 20.76–21.50% less warm CPU. | Admitted peaks match. Shipment and retained zip timing are inconclusive. Native map and invoice warm elapsed rise up to 1.34% and 1.33%. [B1678–B1679](../benchmarks/results/2026-11-25-shared-map-operands/README.md). |
| Carry evaluated integers through ordinary definition construction | Against `01f1f6118`, ordinary definitions allocate 11.60% less and use 2.44–4.82% less warm CPU in the confirmation. | Peak admission matches. Shipment timing rose in the first block and was inconclusive in the repeat. Ordinary time-to-result timing is inconclusive. [B1675](../benchmarks/results/2026-11-25-ordinary-default-operands/README.md). |
| Keep computed singleton positional integers in the callable slot | Defaulted rules use 7.22–12.73% less warm CPU across two blocks and allocate 8.15% less. The longer block takes 2.82–5.43% less time to result. | At this revision, the ordinary-definition control allocated 9.55% more and used 1.45–4.47% more CPU. Peak admission matches. Eager annotated timing is inconclusive. [B1670](../benchmarks/results/2026-11-25-default-operand-storage/README.md). |
| Deliver fixed zip unpacks directly into frame slots | The shipment report takes 8.28–13.84% less warm time across two blocks and 8.95–13.17% less time to result. Allocation falls 12.10% and peak admission 4.38%. | The retained-tuple control varies in the first block and shows a small favorable range in longer batches. [B1665](../benchmarks/results/2026-11-25-zip-unpack/README.md). |
| Share function-local binding tables with emitted code | Defaulted rules allocate 1.81% less during compilation and use less CPU through result in two blocks. Manifest compile allocation falls 0.79%. | Tape and retained admission match. Manifest and invoice elapsed cross zero with longer batches. Defaulted-rule reference spread limits the CPU estimate. [B1662](../benchmarks/results/2026-11-25-shared-function-local-slots/README.md). |
| Borrow positional arguments for direct callback returns | The batched shipment policy report takes 2.77–4.69% less time to result and 2.56–6.73% less warm async time, with lower CPU in both lanes. | Warm CPU and both controls span zero. Allocation and peak admission match. [B1661](../benchmarks/results/2026-11-25-direct-callback-return/README.md). |
| Return existing operands before building a workspace | The shipment report takes 14.17–15.39% less warm time, 11.18–14.15% less time to result and 12.89–13.47% less warm async time. | CPU falls on the report, and peak admission matches. Defaulted-rule CPU through result rises 1.39–1.89% in the longer block. The invoice slowdown does not repeat. [B1659](../benchmarks/results/2026-11-24-direct-operand-return/README.md). |
| Materialize evaluated singleton default tuples on inspection | Defaulted and annotated rules and generated definitions allocate 10.30–15.48% less per run. | Peak admission matches. The generated CPU increase does not repeat with longer batches. Postponed-rule worker variation leaves timing inconclusive. [B1655](../benchmarks/results/2026-11-24-inline-positional-default/README.md). |
| Share binding spellings and allocate only used pattern syntax | Classifier compilation allocation falls from 954,640 B to 888,504 B, down 6.93%. | Emitted tape and retained admission match. Sparse validator and manifest allocation shifts are under 0.3%. Elapsed and CPU remain unmeasured. [B1654](../benchmarks/results/2026-11-24-shared-binding-spelling/README.md). |
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
[B1656]: ../benchmarks/results/2026-11-24-inline-defaults-cpython/README.md
[B1663]: ../benchmarks/results/2026-11-25-shared-locals-cpython/README.md
[B1643]: ../benchmarks/results/2026-11-24-frontend-cpython/README.md
[B1669]: ../benchmarks/results/2026-11-25-post-zip-cpython/README.md
[B1677]: ../benchmarks/results/2026-11-25-ordinary-defaults-cpython/README.md
[B1685]: ../benchmarks/results/2026-11-25-retained-defaults-cpython/README.md
[B1744]: ../benchmarks/results/2026-11-26-file-processing-cpython/README.md
[B1750]: ../benchmarks/results/2026-11-26-short-path-cpython/README.md
[B1760]: ../benchmarks/results/2026-11-26-record-field-cpython/README.md
[B1764]: ../benchmarks/results/2026-11-26-converted-update-cpython/README.md
[B1736]: ../benchmarks/results/2026-11-25-deferred-locals-cpython/README.md
[B1731]: ../benchmarks/results/2026-11-25-arithmetic-call-cpython/README.md
[B1727]: ../benchmarks/results/2026-11-25-formatted-report-cpython/README.md
[B1720]: ../benchmarks/results/2026-11-25-direct-record-cpython/README.md
[B1713]: ../benchmarks/results/2026-11-25-retained-workspace-cpython/README.md
[B1705]: ../benchmarks/results/2026-11-25-retained-csv-cpython/README.md
[B1694]: ../benchmarks/results/2026-11-25-retained-async-cpython/README.md
