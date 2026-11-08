# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.502–5.587 ms | 4.463 ms | 1.23–1.25× | Diagnostic; 1.52% / 1.13% |
| CSV summary/helpers | 4,096 records / 57,361 B | 3.590–3.624 ms | 3.386 ms | 1.06–1.07× | Diagnostic; 0.93% / 1.59% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.930–1.931 ms | 1.273 ms | 1.52–1.52× | Diagnostic; 0.03% / 1.23% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.886–4.088 ms | 4.247 ms | 0.92–0.96× | Variable; 4.92% / 0.88% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.500–3.590 ms | 2.460 ms | 1.42–1.46× | Variable; 2.59% / 4.96% |

**2026-11-08, source `b1e17b64`, CPython 3.13.16, .NET 11 RC3.**
The table measures **compilation plus one execution in an already-running host**,
including mediated RAM-file I/O, complete output validation and prior-result
cleanup. Worker/engine startup and fixture staging/reset are excluded.
**Ratio = Lython elapsed / CPython elapsed; above one means slower.** Ranges span
two independent Lython process medians, not confidence intervals. Control is their
absolute difference; max IQR includes CPython. Diagnostic/Variable describe short
observations, not design acceptance thresholds. Variable cases remain visible.

These measurements describe the named source, rather than subsequent HEAD changes.
The later [record-field comparison](../benchmarks/results/2026-11-08-record-field-resolution/README.md)
measures Lython against that revision separately; it does not update these CPython ratios.

Frozen core `93e926fe` matches the measured production inventory. Windows/Ubuntu
Release actual.NET11/.NET10, original stress both and installed core consumers pass
[cached CI42512127664](../benchmarks/results/2026-11-08-primitive-helper-delivery/delivery-ci.json).
Source qualification and performance evidence are separate. The [R117 checkpoint](../benchmarks/results/2026-11-08-primitive-cpython-checkpoint/README.md)
reuses five unchanged scripts, catalog/goldens and workers:15jobs/311.431s, all
quiet/output/ownership/cleanup/archive gates pass. No remaining VM users/fixtures.

## Warm execution, compilation and startup

Warm execution reuses the compiled script and excludes compilation:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.327–5.355 ms | 4.328 ms | 1.23–1.24× | Diagnostic; 0.52% / 0.67% |
| CSV summary/helpers | 3.427–3.553 ms | 3.320 ms | 1.03–1.07× | Variable; 3.55% / 1.11% |
| Text summary/helpers | 1.875–1.960 ms | 1.186 ms | 1.58–1.65× | Variable; 4.32% / 0.70% |
| JSON-lines/helpers | 3.747–4.073 ms | 4.135 ms | 0.91–0.98× | Variable; 8.01% / 1.01% |
| JSON-to-Markdown report | 3.367–3.438 ms | 2.319 ms | 1.45–1.48× | Variable; 2.07% / 4.04% |

Compilation alone includes code construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 71.4–72.0 µs | 66.2 µs | 1.08–1.09× | Diagnostic; 0.82% / 1.03% |
| CSV summary/helpers | 61.2–61.7 µs | 59.8 µs | 1.02–1.03× | Diagnostic; 0.76% / 1.12% |
| Text summary/helpers | 44.9–45.2 µs | 41.5 µs | 1.08–1.09× | Diagnostic; 0.69% / 2.12% |
| JSON-lines/helpers | 52.8–54.5 µs | 51.2 µs | 1.03–1.07× | Variable; 3.19% / 4.32% |
| JSON-to-Markdown report | 42.0–42.7 µs | 37.8 µs | 1.11–1.13× | Diagnostic; 1.61% / 2.67% |

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

Configuration edit, protected file search and copy/checksum/manifest were last
measured at source7265538c, not b1e17b64. Their historical warm ratios were1.26–1.27×,
1.68–1.69× and1.54×; compile-to-result1.16×,1.49–1.51× and1.48× respectively.
Guarded text and eager/streamed JSON normalization are also historical: warm
1.76–1.78×,1.23–1.25× and0.61–0.62×, with compilation/startup unmeasured for these
variants. [Prior guide and evidence](../benchmarks/results/2026-11-08-json-conditional-checkpoint/README.md)
retain the exact source, adverse controls and boundaries. These cases were not
rerun merely to fill a table. The five current cases above include inconclusive
roles and do not isolate individual optimizations or explain earlier regressions.

## Improvements within Lython

CPython remains the primary external baseline. Separate source pairs below are
not additive or universal CPU gains:

| Change / affected work | Established result | Limits / evidence |
| --- | --- | --- |
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
