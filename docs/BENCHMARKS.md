# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records /57,361 B|7.480 ms|3.847 ms|1.94×|Diagnostic;2.3% Lython IQR|
| Text summary/helpers |3,072 lines /35,840 B|5.168 ms|1.528 ms|3.38×|Variable diagnostic;4.5% control drift|
| JSON-lines/helpers |4,096 records /110,592 B|7.646 ms|4.676 ms|1.64×|Diagnostic;1.4% Lython IQR|
| Config read/edit/write |7,177 B|3.668 ms|1.960 ms|1.87×|Historical conditioned;6.1% IQR|
| Search with early exit |42,010 B file|1.986 ms|0.376 ms|5.28×|Diagnostic;3.2% IQR/2.1% control drift|
| Copy/checksum/manifest |Two files /4,107 B|2.164 ms|0.569 ms|3.80×|Historical variable;10.2% IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|9.128 ms|2.742 ms|3.33×|Variable diagnostic;7.6% control drift|

Measured **2026-11-02** against **CPython3.13.16**, using **.NET11RC3**.
CSV/text/JSON/search/report describe **`fd7bc7e4`**. Config/copy remain historical
**`082179c4`**, not later HEAD. These are **fresh compilation plus one complete
synchronous execution in an already-running host**. Identical source/inputs and
full stdout/files are checked. Ratio is Lython elapsed divided by CPython elapsed;
above one means slower. IQR is interquartile range divided by median.

These diagnostic points are not portable release multipliers. Text/report's
identical-control drift limits precision despite modest within-worker IQR.
The [previous90785737 search result](../benchmarks/results/2026-11-02-current-cpython-checkpoint/README.md)
remains **inconclusive**: fresh270% IQR/218% control drift, warm138%/77%.
Current search is steadier; this does not establish the cause of the earlier
waiting or a causal source speedup. No adverse observations are discarded.
[Current observations and uncertainty](../benchmarks/results/2026-11-02-linear-engine-cpython-checkpoint/README.md).

## Warm execution, compilation and startup

Reused compiled code, including the invocation/file/check boundaries below,
measured separately atfd7bc7e4 except historical config/copy at082179c4:

| Warm workload | Lython | CPython | Ratio | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers |7.258 ms|3.687 ms|1.97×|1.2% /1.0%|
| Text helpers |5.333 ms|1.384 ms|3.85×|1.2% /0.9%|
| JSON-lines helpers |7.631 ms|4.520 ms|1.69×|0.4% /0.4%|
| Config edit |3.792 ms|2.033 ms|1.87×|3.4% /5.2%|
| Search/early exit |1.914 ms|0.266 ms|7.18×|4.3% /0.9%|
| Copy/manifest |2.202 ms|0.441 ms|4.99×|7.5% /1.2%|
| Report rendering |8.331 ms|2.555 ms|3.26×|3.0% /0.3%|

Identical warm Lython controls differ0.6% for CSV,3.5% for text,0.9% for JSON,
1.6% for report and2.9% for search. Independent phase/revision medians are not
additive and do not establish a source speedup or regression.

**Compilation only atfd7bc7e4:** CSV helpers85.65µs versus54.67µs (**1.57×**),
Lython/CPython IQR1.47%/0.42%, identical control0.52%. Report59.78µs versus37.92µs
(**1.58×**), IQR1.21%/0.20%, control0.50%. No guest execution is included.
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
The [shared iteration comparison](../benchmarks/results/2026-11-02-shared-iteration/README.md)
records a modest text improvement and1.42% less text allocation, alongside a
2–3% range-loop elapsed regression. The subsequent
[structured fallthrough comparison](../benchmarks/results/2026-11-02-structured-fallthrough/README.md)
removes redundant block transitions: range loops improve about8–9% against controls,
closing that regression; complete CSV/text results remain mixed.
These comparisons do not refresh the external ratios above.
The [helper forwarding comparison](../benchmarks/results/2026-11-02-native-local-forwarding/README.md)
addresses ordinary named intermediates: complete named CSV/text allocation falls
30.5%/34.7%, warm elapsed about5–8%/8–10%, fresh compile-to-result about5% each.
The [preceding style comparison](../benchmarks/results/2026-11-02-helper-source-styles/README.md)
records the original gap. These source pairs remain separate from CPython ratios.
The [compiled operand-layout comparison](../benchmarks/results/2026-11-02-compiled-operand-layout/README.md)
removes runtime buffer growth, frame-depth tables and nested block dispatch.
Range execution improves about4% against control; report allocation falls1.51%.
Other complete-script timings are mixed, and compilation costs about2% more.
Do not compare independently collected medians to infer an optimization speedup.
The [shared JSON-name comparison](../benchmarks/results/2026-11-03-shared-json-property-names/README.md)
records13.8% less report allocation and21.5% less JSON-lines allocation; report warm
execution improves about9–13% against controls. This is an intermediate milestone:
all-unique names cost aboutone-third more elapsed and28% more allocation. That
regression remains open; these source pairs do not update the CPython table.
The [native wildcard comparison](../benchmarks/results/2026-11-03-native-wildcard-cursors/README.md)
removes rune materialization: complete Unicode/ASCII file-list filtering is about
48%/11% faster, with85%/44% less allocation. Text/report allocation falls2.88%/0.79%;
their timing controls do not establish CPU gains. These are bounded source-pair
diagnostics, separate from the external ratios above.

The [previous representative checkpoint](../benchmarks/results/2026-11-01-representative-checkpoint/README.md)
and [earlier file workloads](../benchmarks/results/2026-10-30-native-json-writer/README.md)
remain historical. [Existing tools](../benchmarks/COMPARISON.md),
[earlier-Lython evidence](../benchmarks/HISTORY.md) and
[experimental history](../benchmarks/EXPERIMENTS.md) preserve detailed procedures,
profiles and rejected designs. CPython is the primary external baseline.
