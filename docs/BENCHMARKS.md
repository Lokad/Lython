# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 5,120 records / 68,625 B | 5.068–5.237 ms | 4.470 ms | 1.13–1.17× | `65ed` | Variable; 3.32% /2.18% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.401–3.470 ms | 3.368 ms | 1.01–1.03× | `6f76` | Variable; 2.02% / 0.74% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.952–1.973 ms | 1.307 ms | 1.49–1.51× | `65ed` | Diagnostic; 1.04% /1.70% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.877–4.084 ms | 4.261 ms | 0.91–0.96× | `6f76` | Variable; 5.36% / 0.56% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.361–3.366 ms | 2.490 ms | 1.35–1.35× | `65ed` | Variable; 0.17% /4.81% |
| Config edit | 181 B / one JSON file | 0.138–0.138 ms | 0.126 ms | 1.09–1.10× | `4598` | Variable; 0.54% /3.95% |
| Protected file search | 2,049 lines /42,010 B; stop after 1,025 | 0.288–0.291 ms | 0.215 ms | 1.34–1.35× | `65ed` | Variable; 0.90% /3.57% |
| Copy/checksum/manifest | two files /4,107 B | 0.384–0.384 ms | 0.260 ms | 1.47–1.48× | `4598` | Diagnostic; 0.14% /1.39% |

**Sources: `65ed` = `65ed6cf7`, 2026-11-09; `4598` = `459813a3`, 2026-11-09;
`6f76` = `6f76f99d`, 2026-11-08.** All use CPython 3.13.16 and .NET 11 RC3 on the same reference VM.
The table measures **compilation plus one execution in an already-running host**,
including mediated RAM-file I/O, complete output validation and prior-result
cleanup. Worker/engine startup and fixture staging/reset are excluded.
**Ratio = Lython elapsed / CPython elapsed; above one means slower.** Ranges span
two independent Lython process medians, not confidence intervals. Control is their
absolute difference; max IQR includes CPython. Diagnostic/Variable describe short
observations, not design acceptance thresholds. Variable cases remain visible.

Each row reports its named source checkpoint, rather than arbitrary later HEAD.
Source `65ed` uses a fresh nonincremental post-commit Release core `082aaa7c`, with
the unchanged R136 worker/adapter and external source/fixtures/goldens. Focused
semantic, cancellation and ownership checks pass on actual .NET 11/10; its full
delivery found a lazy-type/module-super classification regression and 109 raw-module
fixture cases per OS. Corrected `58d3ec0f` passes 200 internal/752 public focused checks
per actual framework; its full Windows/Ubuntu .NET 11/10, original Stress and
installed-package delivery checks pass. These timings still describe `65ed`, not that correction.
Earlier `4598` and `6f76` sources are fully qualified. The later generator source
`081052fa`, with test correction `27353cfb`, passes those full delivery gates.
Qualification and performance evidence are separate; comparing these checkpoint
ratios cannot attribute a gain to an individual optimization.

The [current checkpoint](../benchmarks/results/2026-11-09-cpython-callable-checkpoint/README.md)
refreshes validated CSV, text helpers, JSON report and protected file search:
twelve short jobs/250.847s. All quiet, output, ownership, cleanup
and archive gates pass. Its [prior guide](../benchmarks/results/2026-11-09-cpython-callable-checkpoint/prior-BENCHMARKS.md)
retains every displaced row and original source; the other four rows remain
historical. Variable controls and earlier adverse observations stay explicit.

## Warm execution, compilation and startup

Warm execution reuses the compiled script and excludes compilation:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 4.926–5.007 ms | 4.289 ms | 1.15–1.17× | `65ed` | Diagnostic; 1.65% /1.98% |
| CSV summary/helpers | 3.282–3.302 ms | 3.201 ms | 1.03–1.03× | `6f76` | Diagnostic; 0.60% / 0.68% |
| Text summary/helpers | 1.832–1.878 ms | 1.176 ms | 1.56–1.60× | `65ed` | Variable; 2.50% /2.26% |
| JSON-lines/helpers | 3.795–3.948 ms | 4.059 ms | 0.93–0.97× | `6f76` | Variable; 4.02% / 0.60% |
| JSON-to-Markdown report | 3.199–3.222 ms | 2.338 ms | 1.37–1.38× | `65ed` | Diagnostic; 0.71% /2.17% |
| Config edit | 0.078–0.079 ms | 0.062 ms | 1.26–1.27× | `4598` | Diagnostic; 0.73% /1.54% |
| Protected file search | 0.220–0.223 ms | 0.137 ms | 1.61–1.63× | `65ed` | Diagnostic; 1.36% /1.64% |
| Copy/checksum/manifest | 0.243–0.247 ms | 0.155 ms | 1.56–1.59× | `4598` | Diagnostic; 1.72% /2.72% |

Compilation alone includes code construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 70.4–71.0 µs | 65.8 µs | 1.07–1.08× | `65ed` | Variable; 0.75% /3.95% |
| CSV summary/helpers | 61.3–62.2 µs | 59.1 µs | 1.04–1.05× | `6f76` | Diagnostic; 1.46% / 0.89% |
| Text summary/helpers | 44.0–44.1 µs | 41.7 µs | 1.05–1.06× | `65ed` | Diagnostic; 0.33% /1.82% |
| JSON-lines/helpers | 52.9–53.0 µs | 50.9 µs | 1.04–1.04× | `6f76` | Diagnostic; 0.30% / 1.88% |
| JSON-to-Markdown report | 40.7–41.1 µs | 37.9 µs | 1.07–1.09× | `65ed` | Diagnostic; 1.14% /1.70% |
| Config edit | 37.2–37.5 µs | 34.4 µs | 1.08–1.09× | `4598` | Diagnostic; 0.72% /0.76% |
| Protected file search | 29.2–29.2 µs | 30.9 µs | 0.95–0.95× | `65ed` | Diagnostic; 0.18% /2.40% |
| Copy/checksum/manifest | 70.9–71.9 µs | 56.7 µs | 1.25–1.27× | `4598` | Diagnostic; 1.33% /0.62% |

Startup, RSS/heap peaks, genuinely pending I/O and impact on other embedding work
are unmeasured. Small file-backed scripts do not establish large-file throughput.

## Measurement boundary and fairness

Dedicated Azure F4as_v7, Ubuntu24.04.4, four AMD EPYC9V45vCPUs,16GiB/no swap.
Actual .NET 11 RC3 and isolated CPython 3.13.16 use ordinary GC/tiering; five-second
warmup per role/job and seven rotated 150ms rounds. Current core `082a` uses frozen
worker `22331` and optional 0.4 adapter; no benchmark worker/adapter source changed
since R136. Older rows use their recorded workers. The external supplied catalog,
host operations, protocol and timing boundary are unchanged. This checkpoint
qualifies no newer regex producer.

Identical scripts/fixtures run on both engines, with full stdout/file digests
checked every invocation. Lython uses efficient mediated filesystem overrides;
CPython uses builtin open. Lython opens include capability/path-component checks.
RAM fixtures include guest reads/writes; fixture staging/reset and protocol are
outside clocks. Warm includes invocation/prior-result cleanup. Compilation includes
construction/disposal; compile-to-result adds it to execution. Final cached teardown
is excluded. CPython retains imported modules; Lython isolates execution imports.
This is an embedding comparison, not an isolated CPU kernel. Tables use synchronous
Run; completed-I/O async diagnostics do not establish pending-I/O latency.

## Coverage and uncertainty

Four of eight rows use `65ed`; CSV summary/JSON-lines remain `6f76`, and config/manifest
remain `4598`. Search has 2,049 physical input lines but exits at line 1,025; both engines
run that same early exit. The manifest has two small files. These inputs do not
establish large-tree/file throughput. Search warm execution remains 1.61–1.63×;
the host-mediated whole-script ratio alone does not identify removable interpreter
cost. Startup, genuinely pending I/O and impact on other embedding work remain
unmeasured. R145's class-callable source-pair improvements below do not describe
these unchanged main scripts or settle earlier report regression attribution.
Guarded text and eager/streamed JSON normalization remain historical: warm
1.76–1.78×,1.23–1.25× and0.61–0.62×, with compilation/startup unmeasured for these
variants. [Prior guide and evidence](../benchmarks/results/2026-11-08-json-conditional-checkpoint/README.md)
retain exact sources, adverse controls and boundaries. Inconclusive rows remain
visible; separate checkpoints cannot attribute individual changes or settle all
earlier regression debts.

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
