# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records / 57,361 B|7.448 ms|3.691 ms|2.02×|Diagnostic;1.2% Lython IQR|
| Text summary/helpers |3,072 lines / 35,840 B|5.779 ms|1.463 ms|3.95×|Variable diagnostic;5.6% IQR|
| JSON-lines/helpers |4,096 records / 110,592 B|8.181 ms|4.621 ms|1.77×|Historical diagnostic;5.8% IQR|
| Config read/edit/write |7,177 B|3.668 ms|1.960 ms|1.87×|Historical conditioned;6.1% IQR|
| Search with early exit |42,010 B file|2.064 ms|0.385 ms|5.36×|Historical conditioned;3.6% IQR|
| Copy/checksum/manifest |Two files / 4,107 B|2.164 ms|0.569 ms|3.80×|Historical variable;10.2% IQR|
| JSON-to-Markdown report |3,072 records / 189,441 B|9.819 ms|2.989 ms|3.28×|Diagnostic;4.7% IQR|

Measured **2026-11-02** against **CPython 3.13.16**, using .NET 11RC3.
The report describes **`c3fa26cc`**, CSV/text **`609e5de1`**; the other four rows
are historical **`082179c4`** measurements. These measure **fresh compilation plus
one complete synchronous execution in an already-running host**. Both engines
run identical source/inputs and check complete stdout and file contents. Ratio is
Lython elapsed divided by CPython elapsed; above one means slower. IQR is the
interquartile range divided by median. These are diagnostic points, not portable
release multipliers or measurements of later HEAD.

Current report and CSV/text jobs completed all checks, quiet gates and joined
cleanup. Report warm IQR is6.0% with3.6% unchanged-control drift; text also remains
variable. The report source has focused correctness/ownership qualification;
its full platform/stress/package delivery is tracked separately.
[Report observations](../benchmarks/results/2026-11-02-report-cpython-checkpoint/README.md).
[Exact current observations and uncertainty](../benchmarks/results/2026-11-02-call-construction-cpython-checkpoint/README.md).
The [earlier af82 checkpoint and supervisor failure](../benchmarks/results/2026-11-02-affected-cpython-checkpoint/README.md)
remain preserved separately.
[Historical observations and inconclusive cases](../benchmarks/results/2026-11-02-representative-checkpoint/README.md)
retain all original adverse/noisy results. No source optimization is credited for
a measurement correction.

## Warm execution, compilation and startup

Reused compiled code, including public invocation and the file/check boundaries
below, measured separately (report `c3fa26cc`, CSV/text `609e5de1`, other rows `082179c4`):

| Warm workload | Lython | CPython | Ratio | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers |7.676 ms|3.600 ms|2.13×|1.0% / 3.8%|
| Text helpers |5.551 ms|1.359 ms|4.08×|1.3% / 0.5%|
| JSON-lines helpers |7.195 ms|4.299 ms|1.67×|5.3% / 0.4%|
| Config edit |3.792 ms|2.033 ms|1.87×|3.4% / 5.2%|
| Search/early exit |2.031 ms|0.262 ms|7.75×|2.7% / 0.6%|
| Copy/manifest |2.202 ms|0.441 ms|4.99×|7.5% / 1.2%|
| Report rendering |9.508 ms|2.869 ms|3.31×|6.0% / 1.9%|

Identical current Lython controls differ1.8% for warm CSV and6.2% for warm text;
warm text's ratio is variable despite its narrow within-worker IQR. Fresh controls
differ1.3%/2.4%. All older clocks and adverse controls remain preserved.
Independently collected medians do not establish a source regression or speedup.
The search and text gaps remain substantial.

**Compilation only at `af82bdfc`:** CSV helpers85.11 µs versus54.21 µs (1.57×);
Lython IQR0.66%. **Report compilation at `c3fa26cc`:**57.52 µs versus37.70 µs
(1.53×), Lython/CPython IQR0.92%/0.30%, unchanged control0.12%.
No guest execution is included.
**Startup is unmeasured here.** Independent phase medians are not additive;
warm config's point exceeding its fresh point does not imply negative compilation
cost. [Historical startup observations](../benchmarks/results/2026-10-29-foundation/README.md)
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
Do not compare independently collected medians to infer an optimization speedup.

The [previous representative checkpoint](../benchmarks/results/2026-11-01-representative-checkpoint/README.md)
and [earlier file workloads](../benchmarks/results/2026-10-30-native-json-writer/README.md)
remain historical. [Existing tools](../benchmarks/COMPARISON.md),
[earlier-Lython evidence](../benchmarks/HISTORY.md) and
[experimental history](../benchmarks/EXPERIMENTS.md) preserve detailed procedures,
profiles and rejected designs. CPython is the primary external baseline.
