# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 5,120 records / 68,625 B | 4.665–4.728 ms | 4.525–4.550 ms | 1.03–1.04× | `e543` | Diagnostic; L1.34%, P0.55% / 0.52% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.401–3.470 ms | 3.368 ms | 1.01–1.03× | `6f76` | Variable; 2.02% / 0.74% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.774–1.784 ms | 1.272–1.310 ms | 1.35–1.40× | `4625` | Variable; L0.54%, P2.94% / 2.48% |
| Text / captured helper + counter | 3,072 lines / 35,840 B | 2.395–2.441 ms | 1.031–1.046 ms | 2.29–2.37× | `4625` | Diagnostic; L1.95%, P1.35% / 2.10% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.861–3.939 ms | 4.470–4.495 ms | 0.86–0.88× | `e122` | Variable; L1.97%, P0.56% / 5.66% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.309–3.322 ms | 2.950–3.049 ms | 1.09–1.13× | `e122` | Variable; L0.39%, P3.35% / 6.61% |
| Config edit | 181 B / one JSON file | 140.3–140.8 µs | 124.2–127.7 µs | 1.10–1.13× | `ee44` | Variable; L0.32%, P2.67% / 3.45% |
| Protected file search | 2,049 lines / 42,010 B; stop after 1,025 | 0.290–0.297 ms | 0.209–0.211 ms | 1.37–1.42× | `ee44` | Variable; L2.50%, P0.81% / 6.87% |
| File search / keyword-default helper | 2,049 lines / 42,010 B; stop after 1,025 | 0.328–0.331 ms | 0.217–0.221 ms | 1.48–1.53× | `e573` | Variable; L0.93%, P2.03% / 1.99% |
| Copy/checksum/manifest | two files /4,107 B | 0.384–0.384 ms | 0.260 ms | 1.47–1.48× | `4598` | Diagnostic; 0.14% /1.39% |
| Patch generation | Two 1,024-line UTF-8 files / 67,597 B | 0.556–0.558 ms | 0.647–0.651 ms | 0.85–0.86× | `4aa` | Diagnostic; L0.20%, P0.65% / 1.40% |

Timed rows measure **compilation plus one execution in an already-running host**.
**Ratio = Lython elapsed / CPython elapsed; above one means slower.** It includes
mediated RAM-file I/O, full output validation and prior-result cleanup. Startup,
fixture staging/reset, protocol and final cache teardown are excluded.

**Measured sources:** `4aa` = fully qualified `4aa2d46d`, 2026-11-11 (patch);
`4625` = committed `4625d83e`, 2026-11-11 (both text shapes);
`e122` = committed `e122bdf6`, 2026-11-11 (report and
JSON-lines); `e543` = `e543bf15`, 2026-11-10 (CSV validation);
`e573` = `e5731404`, 2026-11-10 (keyword-default search);
`ee44` = `ee448715`, 2026-11-10; `6f76` = `6f76f99d`, 2026-11-08;
`4598` = `459813a3`, 2026-11-09. Timed rows use stock CPython 3.13.16 on the same VM.
Rows from earlier sources are historical and do not describe later HEAD.
Keyword-default search is distinct from the original literal-prefix search.

The [CSV/text checkpoint](../benchmarks/results/2026-11-10-construction-cpython-checkpoint/README.md)
uses fresh postcommit core `0e2bc55b`, assembly 0.11.0, all 568 source hashes and
fully qualified `e543bf15`. Frozen binaries, scripts and observations survive.
The [report/JSON-lines checkpoint](../benchmarks/results/2026-11-11-json-cpython-checkpoint/README.md)
uses fresh postcommit core `9dadd59b` from build `7714144f`, all 568 source hashes
matching runtime `e122bdf6`, subsequently fully qualified on both platforms/targets,
original stress and installed Release consumers. Later sources keep separate status.
Other rows retain their recorded sources:
[keyword-default search](../benchmarks/results/2026-11-10-dictionary-cpython-checkpoint/README.md)
and [prior five-workload checkpoint](../benchmarks/results/2026-11-10-current-cpython-checkpoint/README.md).

The [current text checkpoint](../benchmarks/results/2026-11-11-text-cpython-checkpoint/README.md)
uses frozen core `cb03fd47`, all 568 source hashes equal fully qualified `4625d83e`.
The captured-helper variant wraps execution in a function and adds an observable
nonlocal counter; each engine runs identical source. It is a distinct script shape,
not an isolated counter-cost comparison. Later frontend source keeps separate status.

Current ranges span two independent process medians **per engine**; ratio ranges
include all four pairings, not confidence intervals. Controls report the absolute
process-median difference for Lython (`L`) and CPython (`P`); max IQR covers both.
Older rows report Lython control spread. Diagnostic/Variable describe observed
stability, not design acceptance thresholds. Adverse and inconclusive cases remain
visible. Separate checkpoints cannot attribute a gain to one optimization.

## Warm execution, compilation and startup

Warm execution reuses the compiled script and excludes compilation:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 4.526–4.666 ms | 4.342–4.355 ms | 1.04–1.07× | `e543` | Variable; L3.11%, P0.29% / 1.45% |
| CSV summary/helpers | 3.282–3.302 ms | 3.201 ms | 1.03–1.03× | `6f76` | Diagnostic; 0.60% / 0.68% |
| Text summary/helpers | 1.650–1.658 ms | 1.158–1.169 ms | 1.41–1.43× | `4625` | Diagnostic; L0.48%, P0.98% / 1.91% |
| Text / captured helper + counter | 2.303–2.308 ms | 0.8825–0.8839 ms | 2.61–2.62× | `4625` | Diagnostic; L0.21%, P0.15% / 1.84% |
| JSON-lines/helpers | 3.775–3.862 ms | 4.322–4.389 ms | 0.86–0.89× | `e122` | Variable; L2.32%, P1.54% / 3.28% |
| JSON-to-Markdown report | 3.312–3.356 ms | 2.739–2.796 ms | 1.18–1.23× | `e122` | Variable; L1.31%, P2.10% / 6.35% |
| Config edit | 78.3–78.6 µs | 61.8–62.1 µs | 1.26–1.27× | `ee44` | Diagnostic; L0.41%, P0.43% / 2.18% |
| Protected file search | 0.217–0.218 ms | 137.8–138.0 µs | 1.58–1.58× | `ee44` | Diagnostic; L0.36%, P0.11% / 1.22% |
| File search / keyword-default helper | 0.246–0.250 ms | 0.164 ms | 1.50–1.52× | `e573` | Diagnostic; L1.53%, P0.15% / 2.45% |
| Copy/checksum/manifest | 0.243–0.247 ms | 0.155 ms | 1.56–1.59× | `4598` | Diagnostic; 1.72% /2.72% |
| Patch generation | 0.497–0.520 ms | 0.576–0.580 ms | 0.86–0.90× | `4aa` | Variable; L4.44%, P0.74% /1.16% |

Compilation alone includes code construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 71.1–71.3 µs | 65.7–66.2 µs | 1.07–1.09× | `e543` | Variable; L0.25%, P0.81% / 4.73% |
| CSV summary/helpers | 61.3–62.2 µs | 59.1 µs | 1.04–1.05× | `6f76` | Diagnostic; 1.46% / 0.89% |
| Text summary/helpers | 44.57–44.83 µs | 41.35–41.36 µs | 1.08–1.08× | `4625` | Diagnostic; L0.58%, P0.03% / 0.93% |
| Text / captured helper + counter | 44.16–44.60 µs | 53.65–53.65 µs | 0.82–0.83× | `4625` | Diagnostic; L1.00%, P0.01% / 2.10% |
| JSON-lines/helpers | 53.2–55.2 µs | 51.4–52.0 µs | 1.02–1.08× | `e122` | Variable; L3.81%, P1.18% / 5.75% |
| JSON-to-Markdown report | 46.5–46.9 µs | 41.1–41.4 µs | 1.12–1.14× | `e122` | Diagnostic; L0.96%, P0.66% / 2.48% |
| Config edit | 36.5–37.2 µs | 34.2–34.4 µs | 1.06–1.09× | `ee44` | Diagnostic; L1.93%, P0.51% / 1.11% |
| Protected file search | 29.0–29.3 µs | 30.8–30.9 µs | 0.94–0.95× | `ee44` | Diagnostic; L0.89%, P0.44% / 1.30% |
| File search / keyword-default helper | 29.5–29.7 µs | 32.4–32.6 µs | 0.91–0.92× | `e573` | Diagnostic; L0.78%, P0.43% / 1.15% |
| Copy/checksum/manifest | 70.9–71.9 µs | 56.7 µs | 1.25–1.27× | `4598` | Diagnostic; 1.33% /0.62% |
| Patch generation | 33.46–33.91 µs | 33.86–34.21 µs | 0.98–1.00× | `4aa` | Diagnostic; L1.34%, P1.06% /0.62% |

Startup, RSS/heap peaks, genuinely pending I/O and impact on other embedding work
are unmeasured. Small file-backed scripts do not establish large-file throughput.

## Measurement boundary and fairness

Dedicated Azure F4as_v7, Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap.
Actual .NET 11 RC3 and isolated CPython 3.13.16 use ordinary GC/tiering; five-second
warmups per role/job and seven rotated 150 ms rounds. CSV validation uses
core `0e2bc55b` (`e543bf15`); patch uses `9ee7e700` (`4aa2d46d`);
current text uses `cb03fd47` (`4625d83e`); report/JSON-lines use `9dadd59b` (`e122bdf6`);
keyword-default search uses `e364e4d9` (`e5731404`). All use common worker
`fec79afd`, optional adapter 0.4 and the existing protocol. Other rows retain their
recorded images. These are source-labelled measurements, not a claim about every
later HEAD, and do not qualify a newer regex producer.

Identical scripts and fixtures run on both engines, with complete stdout/file
digests checked every invocation. Lython uses efficient mediated filesystem
overrides; CPython uses builtin open. Lython opens include capability and path
component checks. CPython retains imported modules; Lython isolates execution
imports. Warm includes invocation and prior-result cleanup; compilation includes
construction/disposal. Independent phase medians need not add up. External process
CPU also includes reset/protocol and cannot establish an internal CPU cost.
Tables use synchronous Run; completed-I/O async diagnostics do not establish
pending-I/O latency. This is an embedding comparison, not an isolated CPU kernel.

## Coverage and uncertainty

The [patch-generation checkpoint](../benchmarks/results/2026-11-11-patch-workflow/README.md)
completes four controlled jobs in 114.763 seconds against CPython 3.13.16, using
qualified `4aa` source and frozen `9ee7e700`. Identity, output, ownership, quiet,
joined-worker, cleanup and archive gates pass. Its single-central-edit fixture
does not establish general diff throughput. Warm Lython controls differ by 4.44%;
compile-to-result controls are tighter. The unchanged config warm control is
1.26–1.27× CPython; this does not qualify config compilation at the newer source.
Earlier CPython 3.13.2 correctness smoke and all its unqualified clocks survive.

The current two-shape text checkpoint completes six jobs in 164.378 seconds, all
identity/output/ownership/quiet/join/archive gates passing. Ordinary text is warm
1.41–1.43× / compile-to-result1.35–1.40× CPython. Its complete CPython control moves
2.94%, limiting precision. The captured-helper/counter script remains warm2.61–2.62×
/ complete2.29–2.37×; compilation alone0.82–0.83× does not explain that gap. These
measurements do not attribute one optimization or establish embedding tail latency.

The earlier construction checkpoint covers six CSV/text jobs in 164.735 seconds;
all identity, output, ownership, quiet, joined-worker and archive checks pass.
Warm CSV is 1.04–1.07× CPython and text 1.36–1.47×. Their compilation is roughly
1.07–1.10×. Warm Lython control spread is 3.11% for CSV and 5.42% for text; these
cases remain variable. Complete-script controls are tighter. These ranges do not
isolate one change, establish neutrality or resolve earlier adverse timings.

The current report/JSON-lines checkpoint completes six jobs in 165.178 seconds,
with all collection gates passing. Report warm is 1.18–1.23× CPython and complete
execution 1.09–1.13×. Warm max IQR 6.35% and complete CPython control 3.35% / max
IQR 6.61% limit precision. JSON-lines warm 0.86–0.89× and complete 0.86–0.88× are
also variable: warm Lython control 2.32%, compile 3.81% and complete max IQR 5.66%.
These state measurements do not isolate the JSON decoder change. The previous
`8d44` measurements and their adversity remain linked in the checkpoint evidence.
Keyword-default search retains earlier-source warm 1.50–1.52×.

Original literal-prefix search and config retain `ee44`; CSV summary retains
`6f76`; the small-file manifest retains `4598`. File search stops at line 1,025
of 2,049. Earlier text rows remain in the linked e543 evidence. Larger-tree throughput, pending I/O, RSS and embedding tail latency remain
unqualified. The [previous guide](../benchmarks/results/2026-11-10-construction-cpython-checkpoint/prior-BENCHMARKS.md)
preserves replaced measurements and their source-specific qualification.

## Improvements within Lython

CPython remains the primary external baseline. Separate source pairs below are
not additive or universal CPU gains:

| Change / affected work | Established result | Limits / evidence |
| --- | --- | --- |
| Ordinary direct-return iteration / complete captured-helper text | CLR allocation −4.84% on both APIs; estimated execution-allowance peak −15.06% | Fully qualified `4aa2d46d` on both platforms/targets, original stress and installed consumers. Quiet elapsed −1.88..+0.34% inconclusive; ordinary control CPU up to+3.31% possible debt. Compile allocation +0.05–0.16%; all original/refined adversity retained. [R225](../benchmarks/results/2026-11-11-ordinary-return-consumption/README.md) |
| Incoming lexical stores / complete CSV, text and report counter variants | CLR allocation −41.4% / −47.9% / −27.5%; quiet warm elapsed 16–17% / 19–21% / 12–15% lower, supporting wider CPU | Separate `1208`→`4625` source pair; unchanged text neutrality unresolved and desktop adversity retained. Fully qualified `4625d83e` on both platforms/targets, original stress and installed consumers. These variants do not replace the CPython rows. [R221](../benchmarks/results/2026-11-11-incoming-lexical-stores/README.md) |
| Completed tuple construction / validated CSV | CLR allocation about 3.04% lower on both APIs; compilation allocation flat | Direct span constructors remove transient storage/admission stages. Quiet CPU remains inconclusive with possible small regression debt; original adversity preserved. Fully qualified `e543bf15` on Windows/Ubuntu, both targets, original stress and installed consumers. [R208](../benchmarks/results/2026-11-10-tuple-value-construction/README.md) |
| Private split-result iteration / complete helper text | CLR allocation about 5.2% lower on both APIs; quiet sync elapsed 1–3% lower with supporting wider CPU | Fully qualified `dba779a5`; recorded1ac→101 source pair. CSV flat; report/async controls uncertain, original adverse desktop results retained. [R203](../benchmarks/results/2026-11-10-transient-split-iteration/README.md) |
| Scope-based method contexts / complete class-renderer report | CLR allocation about 28.5% lower on both APIs; quiet warm elapsed 7–10% lower sync / 5–10% lower async, with supporting wider CPU | Fully qualified `66ebdb4b`. Separate class-helper pair from the CPython rows; unchanged report inconclusive, desktop CSV/compile adversity unresolved. [R198](../benchmarks/results/2026-11-10-method-contexts/README.md) |
| Receiver-aware native entry / complete bound-method text | Quiet warm elapsed −5.2–8.5%, supporting wider CPU; allocation effectively flat | Separate existing class-helper source pair, not the primary CPython rows. Controls inconclusive, report CPU up to+1.47%possible debt; fully qualified1a83c64f. [R192](../benchmarks/results/2026-11-10-receiver-native-entry/README.md) |
| One native dictionary accumulation / complete CSV and text | Warm elapsed −1.1–5.1%CSV/−10.9–14.9%text, supporting widerCPU; allocationflat | Frozen26d→e364 source pair; compilebytes+0.05–0.07%, reportCPU0..+3.03%possible debt. Full delivery GREEN, both platforms/targets, original Stress and installed consumers. [R190](../benchmarks/results/2026-11-10-dictionary-accumulation/README.md) |
| Shared binding / complete file search with keyword-default helper | Quiet warm elapsed −15.7–21.5%, wider process CPU −15.4–18.8%; allocation effectively flat | A separate earlier source pair, not attribution from the current CPython rows. Reference spread 6.75%; text/CSV controls inconclusive with possible small regression debt. Original adverse desktop timings retained. [R184](../benchmarks/results/2026-11-10-shared-call-binding/README.md) |
| Closed helpers inside active exception handlers / complete text script | CLR allocation−47.31% sync/−47.29% async; quiet elapsed/CPU roughly18–22% lower | Ordinary calls already use direct entry. Original adverse controls and bounded confirmation retained; no universal claim. [R179](../benchmarks/results/2026-11-10-handler-native-calls/README.md) |
| Terminal JSON storage / complete3,072-record JSONL manifest | CLR allocation−3.04% sync/−3.03% async; two final copies removed | Desktop elapsed inconclusive/adverse with moving controls. Adopted spare capacity stays charged and can raise retained peaks. [R178](../benchmarks/results/2026-11-10-json-terminal-storage/README.md) |
| Optional Unicode index metadata / complete CSV and text scripts | CLR allocation about−3.58% CSV/−4.94% text; measured string shell64→56B | Host estimates unchanged; Unicode-index control+1.26% allocation. Quiet elapsed overlaps controls; no CPU gain/equivalence. [Source pair](../benchmarks/results/2026-11-09-optional-string-index-metadata/README.md) |
| One callable engine / complete class-callable CSV, text and JSON scripts | Warm elapsed −9–12%, −38–39%, −13–15%, supporting CPU; allocation −23–76% across both APIs | Compile allocation +3–5%; report control +0.05–1.12% elapsed remains uncertain. These variants do not replace the main CPython rows. [R145](../benchmarks/results/2026-11-09-callable-consolidation/README.md) |
| Shared generator state / complete CSV, text and JSON pipelines | Prototype warm elapsed −5.8–11.0%, −11.6–12.1%, −6.2–6.3%, supporting CPU; text allocation −15.1% | Compile allocation +1.3–1.6%; report control +1.2–4.5% elapsed remains debt. Final cleanup differs from the measured prototype. [R142](../benchmarks/results/2026-11-09-generator-storage/README.md) |
| Incoming lexical reads / captured CSV and report helpers | Warm elapsed −7.3–7.9% /−5.0–9.1%; allocation −38.7% /−26.1%, supporting CPU | Async improves; captured text/search control inconclusive. Both sources include the lexical-name proof correction; original desktop adversity retained. [R139](../benchmarks/results/2026-11-09-lexical-operands/README.md) |
| Closed nested helpers / complete CSV, text and report in `main()` | Warm elapsed −8.2%, −19.6–20.0%, −10.5–13.2%; allocation −38.7%, −46.5%, −28.5%, supporting CPU | Async report −6.2–7.7%; report/control variability and original adverse desktop results retained. Same-script Lython pair; full qualification found class-scope regressions, with a [subsequent proof correction](../benchmarks/results/2026-11-09-lexical-name-proof/README.md). [R138](../benchmarks/results/2026-11-09-closed-nested-helpers/README.md) |
| Native Boolean results / CSV and search | Actual warm allocation −9.09% /−11.54–11.66%; id()/is singleton consistency corrected | No general CPU gain; report +0.11–2.44% possible elapsed debt, controls variable. [R135](../benchmarks/results/2026-11-09-native-booleans/README.md) |
| Shared static call syntax / compilation | Allocation −2.59–4.14% across five scripts | CPU/complete-config elapsed inconclusive; wider process CPU has possible debt. [R133](../benchmarks/results/2026-11-09-static-call-shape/README.md) |
| Direct native inputs / search | Consumed nodes/read dispatch removed; controlled syncsearch elapsed −2.45–3.40% | No general CPU/neutrality claim; text CPU/control uncertainty retained. [R132](../benchmarks/results/2026-11-09-native-inputs/README.md) |
| Literal construction / compilation | Allocation −2.94–9.89%; config compile elapsed −3.66–4.79%, complete config −1.08–3.35%, supporting CPU | Warm text +0.36–1.01% possible debt; original adverse desktop compile results preserved. Source459 fully qualified. [R129](../benchmarks/results/2026-11-09-literal-construction/README.md) |
| Execution-owned function lookup facts / report and CSV helpers | Controlled report elapsed −6.09–6.66%; validated CSV −3.30–5.45%, supporting CPU; allocation neutral | Separate R121 source pair; inline control ±0.60%, async float variable. Combined earlier pair retains possible +1–2% async-float debt. [R121](../benchmarks/results/2026-11-08-function-lookup-lifetimes/README.md) |
| Flat lookup storage / shared record fields | Complete CSV/JSON elapsed roughly −5–10%; custom CSV −3–6%, supporting CPU | R119 source pair; text uncertain, report CPU +1.53–6.25% possible debt. Subsequent exclusion cleanup reduces custom allocation 1.71%, without a CPU claim. [R119](../benchmarks/results/2026-11-08-record-field-resolution/README.md), [R120](../benchmarks/results/2026-11-08-cache-eligibility/README.md) |
| Primitive helper proof / complete accounting | Integer CSV/JSON elapsed −3.5–5.2%; float CSV −1.8–6.2% across APIs, supporting CPU; incremental warm allocation neutral | Metadata/ordinary forwarding consolidated. Compiler allocation +1.4–1.5%accounting; custom async +1.3–3.4%elapsed/+6.7%CPU debt, text/report uncertain. Sourceb1e17b64 fully qualified. [R113–R115](../benchmarks/results/2026-11-08-primitive-helper-delivery/README.md) |
| Known-layout record value growth / wide JSON report | Allocation −8.6% both APIs; controlled elapsed −4–10%sync/−6–8%async, coarse CPU supports | New24-field fixtures; alternating records neutral allocation, ordinary controls uncertain. Source05790317 fully qualified. [R110](../benchmarks/results/2026-11-08-json-record-capacity/README.md) |
| Native JSON construction / normalization | Eager allocation −27–28%, elapsed −9–12%; streamed allocation −24%, elapsed −6–7%, supporting CPU | Configuration CPU+4–6%possible debt retained. [R105](../benchmarks/results/2026-11-08-native-json-construction/README.md) |
| Native helper-return expressions | Allocation −19.92%sync/−19.31%async | Original elapsed improvements are diagnostic; later guarded helper source pairs are separate. [History](../benchmarks/EXPERIMENTS.md) |
| Direct UTF8 formatting/storage | Allocation −8.67%; controlled elapsed/CPU−5–8% | Original adverse15.125ms measurement remains unexplained, not erased by later controls. [History](../benchmarks/EXPERIMENTS.md) |

Other delivered source pairs, rejected designs, detailed qualification and original
failed conditions remain in [experimental history](../benchmarks/EXPERIMENTS.md),
the [previous guide](../benchmarks/results/2026-11-08-primitive-cpython-checkpoint/prior-BENCHMARKS.md)
and [benchmark tooling](../benchmarks/README.md). Sampled attribution is workload-specific:
[text](../benchmarks/results/2026-11-09-text-runtime-attribution/README.md) finds native
helper guards/evaluation small, while [protected search](../benchmarks/results/2026-11-09-search-runtime-attribution/README.md)
finds roughly 15% in those categories and little frame construction. Categories
include useful inlined work; these are not removable-cost estimates or predicted gains.
