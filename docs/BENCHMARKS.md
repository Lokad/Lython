# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 5,120 records / 68,625 B | 4.668–4.714 ms | 4.437–4.459 ms | 1.05–1.06× | `e573` | Diagnostic; L0.99%, P0.48% / 1.00% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.401–3.470 ms | 3.368 ms | 1.01–1.03× | `6f76` | Variable; 2.02% / 0.74% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.796–1.856 ms | 1.282–1.302 ms | 1.38–1.45× | `e573` | Variable; L3.21%, P1.53% / 1.01% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.877–4.084 ms | 4.261 ms | 0.91–0.96× | `6f76` | Variable; 5.36% / 0.56% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.356–3.410 ms | 2.519–2.746 ms | 1.22–1.35× | `ee44` | Variable; L1.60%, P9.00% / 4.36% |
| Config edit | 181 B / one JSON file | 140.3–140.8 µs | 124.2–127.7 µs | 1.10–1.13× | `ee44` | Variable; L0.32%, P2.67% / 3.45% |
| Protected file search | 2,049 lines / 42,010 B; stop after 1,025 | 0.290–0.297 ms | 0.209–0.211 ms | 1.37–1.42× | `ee44` | Variable; L2.50%, P0.81% / 6.87% |
| File search / keyword-default helper | 2,049 lines / 42,010 B; stop after 1,025 | 0.328–0.331 ms | 0.217–0.221 ms | 1.48–1.53× | `e573` | Variable; L0.93%, P2.03% / 1.99% |
| Copy/checksum/manifest | two files /4,107 B | 0.384–0.384 ms | 0.260 ms | 1.47–1.48× | `4598` | Diagnostic; 0.14% /1.39% |

The table measures **compilation plus one execution in an already-running host**.
**Ratio = Lython elapsed / CPython elapsed; above one means slower.** It includes
mediated RAM-file I/O, full output validation and prior-result cleanup. Startup,
fixture staging/reset, protocol and final cache teardown are excluded.

**Measured sources:** `e573` = committed `e5731404` production source,
2026-11-10 (CSV, text and keyword-default search); `ee44` = `ee448715`, 2026-11-10;
`6f76` = `6f76f99d`, 2026-11-08; `4598` = `459813a3`, 2026-11-09.
All use stock CPython 3.13.16 on the same reference VM. Rows from earlier sources
are historical and do not describe arbitrary later HEAD. Keyword-default search
is a distinct script; the original literal-prefix search remains visible.

The [current three-workload checkpoint](../benchmarks/results/2026-11-10-dictionary-cpython-checkpoint/README.md)
reuses precommit core `e364e4d9`; all 567 production sources match the committed
revision after explicit Git checkout line-ending normalization. Exact frozen hashes,
source proof, workers and observations are retained. This is not a postcommit
rebuild. The [prior five-workload checkpoint](../benchmarks/results/2026-11-10-current-cpython-checkpoint/README.md)
preserves the earlier source and control variation.

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
| CSV validation/helper calls | 4.568–4.570 ms | 4.258–4.283 ms | 1.07× | `e573` | Diagnostic; L0.03%, P0.58% / 0.63% |
| CSV summary/helpers | 3.282–3.302 ms | 3.201 ms | 1.03–1.03× | `6f76` | Diagnostic; 0.60% / 0.68% |
| Text summary/helpers | 1.713–1.751 ms | 1.181–1.182 ms | 1.45–1.48× | `e573` | Variable; L2.22%, P0.07% / 2.81% |
| JSON-lines/helpers | 3.795–3.948 ms | 4.059 ms | 0.93–0.97× | `6f76` | Variable; 4.02% / 0.60% |
| JSON-to-Markdown report | 3.175–3.185 ms | 2.577–2.585 ms | 1.23–1.24× | `ee44` | Variable; L0.32%, P0.31% / 3.36% |
| Config edit | 78.3–78.6 µs | 61.8–62.1 µs | 1.26–1.27× | `ee44` | Diagnostic; L0.41%, P0.43% / 2.18% |
| Protected file search | 0.217–0.218 ms | 137.8–138.0 µs | 1.58–1.58× | `ee44` | Diagnostic; L0.36%, P0.11% / 1.22% |
| File search / keyword-default helper | 0.246–0.250 ms | 0.164 ms | 1.50–1.52× | `e573` | Diagnostic; L1.53%, P0.15% / 2.45% |
| Copy/checksum/manifest | 0.243–0.247 ms | 0.155 ms | 1.56–1.59× | `4598` | Diagnostic; 1.72% /2.72% |

Compilation alone includes code construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 70.8–71.1 µs | 65.6–66.2 µs | 1.07–1.08× | `e573` | Diagnostic; L0.40%, P0.90% / 2.38% |
| CSV summary/helpers | 61.3–62.2 µs | 59.1 µs | 1.04–1.05× | `6f76` | Diagnostic; 1.46% / 0.89% |
| Text summary/helpers | 44.8–45.2 µs | 41.7–41.9 µs | 1.07–1.08× | `e573` | Variable; L0.90%, P0.51% / 3.52% |
| JSON-lines/helpers | 52.9–53.0 µs | 50.9 µs | 1.04–1.04× | `6f76` | Diagnostic; 0.30% / 1.88% |
| JSON-to-Markdown report | 41.3–41.5 µs | 37.7–37.9 µs | 1.09–1.10× | `ee44` | Diagnostic; L0.59%, P0.36% / 2.27% |
| Config edit | 36.5–37.2 µs | 34.2–34.4 µs | 1.06–1.09× | `ee44` | Diagnostic; L1.93%, P0.51% / 1.11% |
| Protected file search | 29.0–29.3 µs | 30.8–30.9 µs | 0.94–0.95× | `ee44` | Diagnostic; L0.89%, P0.44% / 1.30% |
| File search / keyword-default helper | 29.5–29.7 µs | 32.4–32.6 µs | 0.91–0.92× | `e573` | Diagnostic; L0.78%, P0.43% / 1.15% |
| Copy/checksum/manifest | 70.9–71.9 µs | 56.7 µs | 1.25–1.27× | `4598` | Diagnostic; 1.33% /0.62% |

Startup, RSS/heap peaks, genuinely pending I/O and impact on other embedding work
are unmeasured. Small file-backed scripts do not establish large-file throughput.

## Measurement boundary and fairness

Dedicated Azure F4as_v7, Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap.
Actual .NET 11 RC3 and isolated CPython 3.13.16 use ordinary GC/tiering; five-second
warmup per role/job and seven rotated 150 ms rounds. Current core `e364e4d9`, common
worker `fec79afd`, optional adapter 0.4 and the existing protocol. Older rows retain
their frozen images. These timings do not qualify a newer regex producer.

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

The three current workloads complete nine jobs in246.994seconds with all
identity, output, ownership, quiet, joined-worker and archived-evidence checks
passing. Warm text remains1.45–1.48× CPython, keyword-default search1.50–1.52×,
and CSV validation about1.07×. Compilation ranges0.91× to1.08×.

Text compile max IQR3.52%, warm Lython spread2.22%, complete Lython spread3.21%
and complete search CPython spread2.03% remain explicit. All pairings and adverse
observations are retained. These measurements do not isolate a change's benefit
or establish equivalence or statistical significance.

Original literal-prefix search, report and config retain `ee44`; CSV summary and
JSON-lines retain `6f76`; the small-file manifest retains `4598`. File search stops
at line 1,025 of 2,049. Larger-tree throughput, pending I/O, RSS and embedding tail
latency remain unqualified. The [previous guide](../benchmarks/results/2026-11-10-dictionary-cpython-checkpoint/prior-BENCHMARKS.md)
and its linked artifacts preserve replaced observations and older qualification.

## Improvements within Lython

CPython remains the primary external baseline. Separate source pairs below are
not additive or universal CPU gains:

| Change / affected work | Established result | Limits / evidence |
| --- | --- | --- |
| One native dictionary accumulation / complete CSV and text | Warm elapsed −1.1–5.1%CSV/−10.9–14.9%text, supporting widerCPU; allocationflat | Frozen26d→e364 source pair; compilebytes+0.05–0.07%, reportCPU0..+3.03%possible debt. Full delivery qualification pending. [R190](../benchmarks/results/2026-11-10-dictionary-accumulation/README.md) |
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
