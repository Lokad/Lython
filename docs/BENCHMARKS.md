# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records / 57,361 B|7.297 ms|3.647 ms|2.00×|Diagnostic;2.9% Lython IQR|
| Text summary/helpers |3,072 lines / 35,840 B|5.688 ms|1.513 ms|3.76×|Variable diagnostic;4.5% control drift|
| JSON-lines/helpers |4,096 records / 110,592 B|7.483 ms|4.443 ms|1.68×|Diagnostic;3.6% control drift|
| Config read/edit/write |7,177 B|3.668 ms|1.960 ms|1.87×|Historical conditioned;6.1% IQR|
| Search with early exit |42,010 B file|1.997 ms†|0.385 ms|—|Inconclusive;270% IQR/218% control drift|
| Copy/checksum/manifest |Two files / 4,107 B|2.164 ms|0.569 ms|3.80×|Historical variable;10.2% IQR|
| JSON-to-Markdown report |3,072 records / 189,441 B|8.893 ms|2.767 ms|3.21×|Variable diagnostic;5.6% IQR|

Measured **2026-11-02** against **CPython3.13.16**, using **.NET11RC3**.
CSV/text/JSON/search/report describe **`90785737`**. Config/copy remain historical
**`082179c4`**, not later HEAD. These are **fresh compilation plus one complete
synchronous execution in an already-running host**. Identical source/inputs and
full stdout/files are checked. Ratio is Lython elapsed divided by CPython elapsed;
above one means slower. IQR is interquartile range divided by median.

These diagnostic points are not portable release multipliers. †Search's median
is shown for transparency, but its unstable identical controls make a reliable
ratio unavailable. The current gap remains unresolved; historical fresh5.36×/
warm7.75× points describe082179c4 only. Narrow within-worker IQR alone also does
not resolve current text/JSON control drift.
[Current observations and all uncertainty](../benchmarks/results/2026-11-02-current-cpython-checkpoint/README.md).
[Earlier report](../benchmarks/results/2026-11-02-report-cpython-checkpoint/README.md),
[CSV/text](../benchmarks/results/2026-11-02-call-construction-cpython-checkpoint/README.md)
and [historical workloads](../benchmarks/results/2026-11-02-representative-checkpoint/README.md)
retain original clocks, adverse results and measurement corrections.

## Warm execution, compilation and startup

Reused compiled code, including the invocation/file/check boundaries below,
measured separately at90785737 except historical config/copy at082179c4:

| Warm workload | Lython | CPython | Ratio | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers |7.092 ms|3.468 ms|2.05×|1.1% /0.4%|
| Text helpers |5.568 ms|1.365 ms|4.08×|1.7% /0.3%|
| JSON-lines helpers |7.426 ms|4.265 ms|1.74×|1.6% /0.4%|
| Config edit |3.792 ms|2.033 ms|1.87×|3.4% /5.2%|
| Search/early exit |4.520 ms†|0.262 ms|—|138.0% /0.7%|
| Copy/manifest |2.202 ms|0.441 ms|4.99×|7.5% /1.2%|
| Report rendering |8.792 ms|2.560 ms|3.43×|4.1% /2.6%|

Identical warm Lython controls differ1.2% for CSV,1.7% for text,1.1% for JSON,
2.2% for report and77.4% for inconclusive search. Independent phase/revision
medians are not additive and do not establish a source speedup or regression.

**Compilation only at90785737:** CSV helpers83.79µs versus54.46µs (**1.54×**),
Lython/CPython IQR0.92%/0.25%, identical control1.94%. Report57.23µs versus37.62µs
(**1.52×**), IQR2.11%/0.22%, control0.58%. No guest execution is included.
**Startup is unmeasured here.**
[Historical startup observations](../benchmarks/results/2026-10-29-foundation/README.md)
describe their original revisions.

## Machine and included work

Dedicated Azure **Standard_F4as_v7**, Ubuntu 24.04.4 x86-64, four AMD EPYC 9V45
vCPUs, one thread/core,16 GiB RAM/no swap. Reference CPython is GCC 13.3 PGO/LTO,
ordinary GIL/GC, no JIT/debug, launched `-I -S -X utf8`. SDK/runtime:
**11.0.100-rc.3.26522.148 / 11.0.0-rc.3.26522.148**. Engines run sequentially
with ordinary worker GC/tiering; each dataset records its actual quiet-gate coverage.

Execution timing includes fixture reset/staging, efficient mediated file I/O,
complete stdout/file reads/checks and prior-result disposal within each batch.
Fresh Lython execution also disposes each per-invocation compilation inside the
timer. The final result stays funded through response delivery, then is disposed
outside that batch timer. Compile-only retains its last compilation; cached
programs dispose at worker exit. Startup, transport/response hashing and final
cached-owner/process/fixture teardown are excluded. Every worker is joined after
cleanup. Actual suspended-I/O semantics are tested separately; these use `Run`.

The host supplies efficient ranged reads/appends. These comparisons include
containment, Python conversion and host-boundary work; they do not isolate a
single cause of the remaining gaps. The linked measurement accounts preserve
original, corrected and inconclusive observations separately.

## Earlier Lython and further work

Allocation reductions and earlier-Lython improvements are separate from this
external baseline: [native helper returns](../benchmarks/results/2026-11-02-native-function-returns/README.md),
[CSV construction](../benchmarks/results/2026-11-02-native-csv-records/README.md),
[direct formatting storage](../benchmarks/results/2026-11-02-formatting-confirmation/README.md),
and [consuming output storage](../benchmarks/results/2026-11-02-owned-output-storage/README.md).
The [compound helper comparison](../benchmarks/results/2026-11-02-native-return-expressions/README.md)
separates text/report gains from CSV/JSON regression debt and inconclusive search CPU.
The [combined routing checkpoint](../benchmarks/results/2026-11-02-prepared-operation-routing/README.md)
records the subsequent source pair and remaining CSV debt.
The [shared formatted-output comparison](../benchmarks/results/2026-11-02-shared-formatted-output/README.md)
records14.26% less report allocation and about8.3% lower warm elapsed against
unchanged controls. Explicit-format results are flat/inconclusive. These are
separate from the external CPython ratios above.
The [native JSON token comparison](../benchmarks/results/2026-11-02-native-json-tokens/README.md)
records subsequent report/JSON-lines gains and the scan-cancellation correction;
those source-pair observations are separate from the refreshed external table.
The [native call-context comparison](../benchmarks/results/2026-11-02-native-call-contexts/README.md)
records10–13% less allocation for complete CSV/JSON/text helpers and36.6% for
search. Elapsed/CPU results are mixed; it establishes no general CPU improvement.
Do not compare independently collected medians to infer an optimization speedup.

The [previous representative checkpoint](../benchmarks/results/2026-11-01-representative-checkpoint/README.md)
and [earlier file workloads](../benchmarks/results/2026-10-30-native-json-writer/README.md)
remain historical. [Existing tools](../benchmarks/COMPARISON.md),
[earlier-Lython evidence](../benchmarks/HISTORY.md) and
[experimental history](../benchmarks/EXPERIMENTS.md) preserve detailed procedures,
profiles and rejected designs. CPython is the primary external baseline.
