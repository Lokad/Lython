# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 5,120 records / 68,625 B | 5.227–5.247 ms | 4.467 ms | 1.17–1.17× | `6f76` | Diagnostic; 0.39% / 0.87% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.401–3.470 ms | 3.368 ms | 1.01–1.03× | `6f76` | Variable; 2.02% / 0.74% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.938–1.959 ms | 1.276 ms | 1.52–1.53× | `6f76` | Diagnostic; 1.09% / 1.33% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.877–4.084 ms | 4.261 ms | 0.91–0.96× | `6f76` | Variable; 5.36% / 0.56% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.240–3.247 ms | 2.450 ms | 1.32–1.33× | `6f76` | Diagnostic; 0.23% / 2.35% |
| Config edit | 181 B / one JSON file | 0.138–0.138 ms | 0.126 ms | 1.09–1.10× | `4598` | Variable; 0.54% /3.95% |
| Protected file search | 1,025 lines /42,010 B | 0.297–0.299 ms | 0.212 ms | 1.40–1.41× | `4598` | Diagnostic; 0.94% /0.95% |
| Copy/checksum/manifest | two files /4,107 B | 0.384–0.384 ms | 0.260 ms | 1.47–1.48× | `4598` | Diagnostic; 0.14% /1.39% |

**Sources: `6f76` = `6f76f99d`, 2026-11-08; `4598` = `459813a3`, 2026-11-09.**
Both use CPython 3.13.16 and .NET 11 RC3 on the same reference VM.
The table measures **compilation plus one execution in an already-running host**,
including mediated RAM-file I/O, complete output validation and prior-result
cleanup. Worker/engine startup and fixture staging/reset are excluded.
**Ratio = Lython elapsed / CPython elapsed; above one means slower.** Ranges span
two independent Lython process medians, not confidence intervals. Control is their
absolute difference; max IQR includes CPython. Diagnostic/Variable describe short
observations, not design acceptance thresholds. Variable cases remain visible.

Each row describes its named runtime source, rather than subsequent HEAD changes.
For `6f76`, frozen core `c6172fec` matches the production inventory. Test-only commit
`d97ca8f2` corrects a stale internal fixture. Combined Windows/Ubuntu actual.NET11/
.NET10 full Release, original stress and installed-core consumers pass
[cached CI42527042378](../benchmarks/results/2026-11-08-function-lookup-lifetimes/delivery-ci.json).
Source qualification and performance evidence are separate; later prototypes are
outside this named-source checkpoint. Source `459813a3`/core `1be2a325` has
focused both-target semantic/cancellation/ownership checks and controlled performance
evidence; [full platform/stress/package qualification](../benchmarks/results/2026-11-09-literal-construction/delivery-ci.json)
also passes independently. Later working prototypes are outside these source rows.

The [R122 checkpoint](../benchmarks/results/2026-11-08-function-cpython-checkpoint/README.md)
reuses five unchanged scripts, catalog/goldens and workers: 15 jobs / 311.685s.
All quiet/output/ownership/cleanup/archive gates pass; no remaining VM users or
fixtures. The [prior guide](../benchmarks/results/2026-11-08-function-cpython-checkpoint/prior-BENCHMARKS.md)
preserves the earlier qualified `b1e17b64` comparison. Ratios from separate
checkpoints do not isolate individual optimization gains. The [R130 targeted refresh](../benchmarks/results/2026-11-09-agent-workloads/README.md)
adds the three previously stale config/file workloads: nine jobs /193.226s, all
source/quiet/golden/ownership/cleanup/archive gates pass. Config compile-to-result
remains variable (max IQR3.95% despite close Lython controls).

## Warm execution, compilation and startup

Warm execution reuses the compiled script and excludes compilation:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 5.038–5.070 ms | 4.317 ms | 1.17–1.17× | `6f76` | Diagnostic; 0.63% / 1.32% |
| CSV summary/helpers | 3.282–3.302 ms | 3.201 ms | 1.03–1.03× | `6f76` | Diagnostic; 0.60% / 0.68% |
| Text summary/helpers | 1.829–1.883 ms | 1.168 ms | 1.57–1.61× | `6f76` | Variable; 2.91% / 0.68% |
| JSON-lines/helpers | 3.795–3.948 ms | 4.059 ms | 0.93–0.97× | `6f76` | Variable; 4.02% / 0.60% |
| JSON-to-Markdown report | 3.142–3.231 ms | 2.297 ms | 1.37–1.41× | `6f76` | Variable; 2.84% / 3.59% |
| Config edit | 0.078–0.079 ms | 0.062 ms | 1.26–1.27× | `4598` | Diagnostic; 0.73% /1.54% |
| Protected file search | 0.231–0.235 ms | 0.137 ms | 1.69–1.71× | `4598` | Diagnostic; 1.61% /1.43% |
| Copy/checksum/manifest | 0.243–0.247 ms | 0.155 ms | 1.56–1.59× | `4598` | Diagnostic; 1.72% /2.72% |

Compilation alone includes code construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Source | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- | --- |
| CSV validation/helper calls | 71.6–71.9 µs | 65.4 µs | 1.09–1.10× | `6f76` | Variable; 0.33% / 3.12% |
| CSV summary/helpers | 61.3–62.2 µs | 59.1 µs | 1.04–1.05× | `6f76` | Diagnostic; 1.46% / 0.89% |
| Text summary/helpers | 45.2–45.8 µs | 41.1 µs | 1.10–1.11× | `6f76` | Diagnostic; 1.36% / 0.48% |
| JSON-lines/helpers | 52.9–53.0 µs | 50.9 µs | 1.04–1.04× | `6f76` | Diagnostic; 0.30% / 1.88% |
| JSON-to-Markdown report | 42.7–42.9 µs | 38.0 µs | 1.12–1.13× | `6f76` | Diagnostic; 0.45% / 1.13% |
| Config edit | 37.2–37.5 µs | 34.4 µs | 1.08–1.09× | `4598` | Diagnostic; 0.72% /0.76% |
| Protected file search | 29.6–29.8 µs | 30.9 µs | 0.96–0.97× | `4598` | Diagnostic; 0.89% /0.66% |
| Copy/checksum/manifest | 70.9–71.9 µs | 56.7 µs | 1.25–1.27× | `4598` | Diagnostic; 1.33% /0.62% |

Startup, RSS/heap peaks, genuinely pending I/O and impact on other embedding work
are unmeasured. Small file-backed scripts do not establish large-file throughput.

## Measurement boundary and fairness

Dedicated Azure F4as_v7, Ubuntu24.04.4, four AMD EPYC9V45vCPUs,16GiB/no swap.
Actual.NET11RC3 and isolated CPython3.13.16 use normal GC/tiering; five-second
warmup per role/job and seven rotated150msrounds. Frozen worker7cf/optional0.4
adapter unchanged. This checkpoint certifies no newer regex producer.

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

The eight rows use two explicit source checkpoints. Config/search/manifest were
previously measured at7265538c; the targeted459813a3 refresh replaces their stale
figures, with prior evidence retained. The manifest fixture has two small files;
these cases do not establish large-tree or large-file throughput. File-search warm
execution remains1.69–1.71× despite compilation0.96–0.97×; the whole-script boundary
includes host I/O and cleanup, so the ratio alone does not identify interpreter cost.
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
and [benchmark tooling](../benchmarks/README.md). Current text CPU attribution
finds useful native processing and required checks; it does not establish dominant
call/frame construction or justify a new interpreter from inclusive samples alone.
