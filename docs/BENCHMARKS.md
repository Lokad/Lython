# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 5,120 records / 68,625 B | 4.975–4.976 ms | 4.497–4.550 ms | 1.09–1.11× | `4335` | Diagnostic; L0.02%, P1.19% / 1.88% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.401–3.470 ms | 3.368 ms | 1.01–1.03× | `6f76` | Variable; 2.02% / 0.74% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.966–2.008 ms | 1.270–1.276 ms | 1.54–1.58× | `4335` | Variable; L2.10%, P0.45% / 0.91% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.877–4.084 ms | 4.261 ms | 0.91–0.96× | `6f76` | Variable; 5.36% / 0.56% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.494–3.567 ms | 2.478–2.489 ms | 1.40–1.44× | `4335` | Variable; L2.05%, P0.47% / 3.16% |
| Config edit | 181 B / one JSON file | 140.6–141.3 µs | 125.8–126.6 µs | 1.11–1.12× | `4335` | Diagnostic; L0.52%, P0.64% / 2.30% |
| Protected file search | 2,049 lines / 42,010 B; stop after 1,025 | 0.288–0.291 ms | 0.212–0.212 ms | 1.36–1.37× | `4335` | Variable; L0.72%, P0.06% / 5.92% |
| Copy/checksum/manifest | two files /4,107 B | 0.384–0.384 ms | 0.260 ms | 1.47–1.48× | `4598` | Diagnostic; 0.14% /1.39% |

The table measures **compilation plus one execution in an already-running host**.
**Ratio = Lython elapsed / CPython elapsed; above one means slower.** It includes
mediated RAM-file I/O, full output validation and prior-result cleanup. Startup,
fixture staging/reset, protocol and final cache teardown are excluded.

**Measured sources:** `4335` = `43350e4f`-equivalent production source, 2026-11-09;
`6f76` = `6f76f99d`, 2026-11-08; `4598` = `459813a3`, 2026-11-09. All compare with
stock CPython 3.13.16 on the same reference VM. The three older rows are historical;
none describes arbitrary later HEAD.

The [current five-workload checkpoint](../benchmarks/results/2026-11-09-current-cpython-checkpoint/README.md)
uses the freshly nonincremental R167 precommit freeze: every production source
byte matches `43350e4f` and documentation-only `220732b7`. This is not a fresh
postcommit binary. Exact cores, sources, workers and observations are linked there.

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
| CSV validation/helper calls | 4.712–4.771 ms | 4.305–4.328 ms | 1.09–1.11× | `4335` | Diagnostic; L1.24%, P0.55% / 1.25% |
| CSV summary/helpers | 3.282–3.302 ms | 3.201 ms | 1.03–1.03× | `6f76` | Diagnostic; 0.60% / 0.68% |
| Text summary/helpers | 1.897–1.945 ms | 1.159–1.164 ms | 1.63–1.68× | `4335` | Variable; L2.47%, P0.43% / 0.98% |
| JSON-lines/helpers | 3.795–3.948 ms | 4.059 ms | 0.93–0.97× | `6f76` | Variable; 4.02% / 0.60% |
| JSON-to-Markdown report | 3.204–3.274 ms | 2.328–2.571 ms | 1.25–1.41× | `4335` | Variable; L2.15%, P10.43% / 4.16% |
| Config edit | 78.2–78.3 µs | 61.3–62.3 µs | 1.26–1.28× | `4335` | Diagnostic; L0.11%, P1.64% / 1.58% |
| Protected file search | 0.220–0.226 ms | 0.137–0.139 ms | 1.59–1.65× | `4335` | Variable; L2.59%, P1.42% / 3.94% |
| Copy/checksum/manifest | 0.243–0.247 ms | 0.155 ms | 1.56–1.59× | `4598` | Diagnostic; 1.72% /2.72% |

Compilation alone includes code construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 70.6–71.0 µs | 65.8–65.9 µs | 1.07–1.08× | `4335` | Variable; L0.54%, P0.08% / 3.88% |
| CSV summary/helpers | 61.3–62.2 µs | 59.1 µs | 1.04–1.05× | `6f76` | Diagnostic; 1.46% / 0.89% |
| Text summary/helpers | 44.1–44.7 µs | 41.3–41.4 µs | 1.07–1.08× | `4335` | Diagnostic; L1.52%, P0.23% / 1.02% |
| JSON-lines/helpers | 52.9–53.0 µs | 50.9 µs | 1.04–1.04× | `6f76` | Diagnostic; 0.30% / 1.88% |
| JSON-to-Markdown report | 41.2–41.8 µs | 37.6–37.8 µs | 1.09–1.11× | `4335` | Diagnostic; L1.35%, P0.58% / 2.87% |
| Config edit | 36.8–37.1 µs | 34.1–34.3 µs | 1.07–1.09× | `4335` | Diagnostic; L0.96%, P0.40% / 0.56% |
| Protected file search | 28.8–29.4 µs | 30.8–31.0 µs | 0.93–0.95× | `4335` | Variable; L2.04%, P0.70% / 1.41% |
| Copy/checksum/manifest | 70.9–71.9 µs | 56.7 µs | 1.25–1.27× | `4598` | Diagnostic; 1.33% /0.62% |

Startup, RSS/heap peaks, genuinely pending I/O and impact on other embedding work
are unmeasured. Small file-backed scripts do not establish large-file throughput.

## Measurement boundary and fairness

Dedicated Azure F4as_v7, Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap.
Actual .NET 11 RC3 and isolated CPython 3.13.16 use ordinary GC/tiering; five-second
warmup per role/job and seven rotated 150 ms rounds. Current core `cc596abc`, common
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

The five current workloads complete 15 jobs in 410.313 seconds with all identity,
output, ownership, quiet, joined-worker and archived-evidence checks passing.
Text warm/complete and search timings remain variable. JSON report warm CPython
controls differ by 10.43%; that 1.25–1.41× range needs this qualification. Config
and CSV execution are more stable. These observations do not settle earlier
single-change regression debts or establish statistical significance.

CSV summary/JSON-lines retain `6f76`; the two-small-file manifest retains `4598`.
Search exits at line 1,025 of 2,049. Large-tree throughput and pending I/O remain
unqualified. Earlier guarded-text and eager/streamed-JSON warm measurements
(1.76–1.78×, 1.23–1.25× and 0.61–0.62×) are historical, with compilation/startup
unmeasured. [Prior guide and evidence](../benchmarks/results/2026-11-08-json-conditional-checkpoint/README.md)
and the [replaced checkpoint guide](../benchmarks/results/2026-11-09-current-cpython-checkpoint/prior-BENCHMARKS.md)
preserve their exact sources and adverse controls.

## Improvements within Lython

CPython remains the primary external baseline. Separate source pairs below are
not additive or universal CPU gains:

| Change / affected work | Established result | Limits / evidence |
| --- | --- | --- |
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
