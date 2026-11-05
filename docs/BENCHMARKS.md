# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/native helpers |4,096records /57,361B|5.005 ms|3.497 ms|1.43×|a8e476bf; RAM; Diagnostic;0.25%control /0.72%IQR|
| CSV summary/ordinary helper |4,096records /57,361B|5.943 ms|3.540 ms|1.68×|a8e476bf; RAM; Diagnostic;0.76%control /1.80%IQR|
| Text summary/helpers |3,072lines /35,840B|2.745 ms|1.279 ms|2.15×|a8e476bf; RAM; Diagnostic;0.10%control /0.84%IQR|
| JSON-lines/helpers |4,096records /110,592B|4.992–5.182 ms|4.191 ms|1.19–1.24×|a8e476bf; RAM; Variable;3.65%control /1.78%IQR|
| JSON-to-Markdown report |3,072records /189,441B|3.710–3.928 ms|2.506 ms|1.48–1.57×|a8e476bf; RAM; Variable;5.54%control /5.25%IQR|
| Config read/edit/write |1,024paths /7,177B|0.267 ms|0.255 ms|1.05×|a8e476bf; RAM; Diagnostic;0.53%control /0.89%IQR|
| CSV long fields |16records /524,717B|0.398 ms|2.430 ms|0.16×|a8e476bf; RAM; Diagnostic;1.20%control /1.17%IQR|
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Earlier bd3;ordinary FS; Diagnostic; 2.7% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Earlier bd3;ordinary FS; Variable; 3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Earlier bd3;ordinary FS; Diagnostic; 1.5% Lython IQR|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Earlier bd3;ordinary FS;1.1% Lython IQR|

First seven rows measured **2026-11-05**, retained source **a8e476bf** (same
production in **c6d71108**), against **CPython3.13.16** on **.NET11RC3**. They
measure **fresh compilation plus one complete synchronous execution in an
already-running host**, including mediated RAM-file I/O, full checks and prior-
result cleanup, excluding staging/reset. Ratio=Lython elapsed/CPython elapsed;
above one means slower. IQR=interquartile range/median. Control compares identical-
source role medians. Exact frozen core **402ae9f1** and snapshots identify the
measured build. Final qualification on **c6d71108** passes Windows/Ubuntu,
actual11/10, original stress and installed-package checks; the terminal CI result
is [cached with the evidence](../benchmarks/results/2026-11-05-private-csv-records/delivery-ci-status.json).

JSON/report ranges show identical-source medians, not confidence intervals. Warm
JSON controls differ5.23%; freshreport controls5.54%. The selected warmJSON point
is5.5% slower than0997 while its identical-source control is unchanged; keep that
uncertainty visible. Ordinary-function CSV is a distinct complete script using
ordinary helpers under both runtimes. Text remains about2.15×CPython; long-field
CSV does not describe ordinary CSV. Startup, genuine suspended I/O, heap/RSS and
embedding latency remain unmeasured.
[Current observations and exact identities](../benchmarks/results/2026-11-05-runtime-cpython-csv-checkpoint/README.md).

Four explicitly earlier file rows use 2026-11-03 **bd3f9dee** and ordinary filesystem;
filename scripts filter768 names/write256 selections, not traverse768 files.
[Previous cf3 checkpoint](../benchmarks/results/2026-11-05-runtime-cpython-pipeline-checkpoint/README.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve earlier sources and
adverse results. Earlier90785737 search remains inconclusive (fresh270%IQR/218%
control drift; warm138%/77%). The [clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
excludes staging symmetrically; it is not a runtime optimization.

## Warm execution, compilation and startup

Latest external source **a8e476bf**, RAM files, reused compiled code with the
same invocation/I/O/check/cleanup boundary:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV/native helpers |4.845 ms|3.314 ms|1.46×|1.40% /3.21%|
| CSV/ordinary helper |5.463 ms|3.200 ms|1.71×|0.32% /1.21%|
| Text/helpers |2.609 ms|1.203 ms|2.17×|0.23% /0.44%|
| JSON-lines/helpers |4.774–5.038 ms|4.074 ms|1.17–1.24×|5.23% /0.31%|
| JSON-to-Markdown |3.685 ms|2.343 ms|1.57×|0.74% /4.38%|
| Config read/edit/write |0.194 ms|0.183 ms|1.06×|0.89% /1.32%|
| CSV long fields |0.251 ms|2.287 ms|0.11×|0.18% /2.13%|

JSON/report and warmCSV dispersion limit small claims. Do not subtract fresh/warm
medians to infer compile cost. **Compilation only:** CSV69.79µs vs54.73µs
(**1.28×**), report51.03µs vs37.66µs (**1.36×**); LythonIQR2.58%/.86%,
controls.62%/1.77%, execution/I/O excluded. **Startup unmeasured.**
[Earlier phases/revisions](../benchmarks/EXPERIMENTS.md) retain their own conditions.

## Machine and included work

Dedicated Azure **Standard_F4as_v7**, Ubuntu 24.04.4 x86-64, four AMD EPYC 9V45
vCPUs, one thread/core, 16 GiB RAM/no swap. CPython is GCC 13.3 PGO/LTO, ordinary
GIL/GC, no JIT/debug, launched `-I -S -X utf8`. SDK/runtime:
**11.0.100-rc.3.26522.148 /11.0.0-rc.3.26522.148**. Engines run sequentially
with ordinary worker GC/tiering, quiet gates and independent unchanged controls.

Both clocks include invocation setup, actual mediated guest file I/O, complete
stdout/file reads/checks and prior-result disposal within each batch. Fresh Lython
also disposes each per-invocation compilation inside the timer. Every invocation
resets/stages fresh fixture files **outside both execution clocks**; all work still
counts toward wall deadlines. The final result stays funded through response
delivery, then disposes outside that batch timer. Compile-only retains its last
compilation; cached programs dispose at worker exit. Startup, transport/response
hashing and final cached-owner/process/fixture teardown are excluded. Every worker
is joined after cleanup. Fresh script globals are used; CPython process-level
module/library caches persist, while Lython execution caches are isolated. Actual
suspended-I/O semantics are tested separately; these use `Run`.

The host supplies efficient ranged reads/appends. Containment, Python conversion,
output verification and host-boundary work are included; this is not a guest-
dispatch-only measurement. Exact worker procedures and limitations are linked below.

## Improvements against earlier Lython

These are separate source-pair results, not CPython ratios or additive gains.
Each link records its measured revisions, controls and limitations.

| Change | Established result | Evidence and limitations |
| --- | --- | --- |
| Native dictionary backing | Construction variant: CSV allocation −3.3–5.2%, JSON −6.9%, report −4.49%; ordinary CSV elapsed 5.6–8.5% lower, JSON 4.1–4.3% lower | [Three frozen sources and controls](../benchmarks/results/2026-11-05-native-dictionary-backing/README.md). Final removal completion has separate checks; its two-job elapsed comparison is inconclusive with 6.6–9.2% unchanged-control spread. Full delivery pending. |
| Final list storage / lazy activation | Text allocation −5.0–5.4%; ordinary CSV −6.3–6.5%; async text elapsed −3.2–3.4% | [Complete source pair](../benchmarks/results/2026-11-05-final-storage-activation/README.md). Warm text/CSV magnitudes control-limited; report near unchanged. Fully qualified on e5017ce4; original Windows timeout and unchanged-source confirmation preserved. |
| Private CSV record arrays | CSV allocation −4.7–7.6%; public-reader warm elapsed −4.2–5.3% | [Source variants and confirmation](../benchmarks/results/2026-11-05-private-csv-records/README.md). DictReader warm timing control-limited; roughly1% warm-text cost accepted, larger original adverse point preserved. Fully qualified on c6d71108. |
| Known/empty list backing | Text allocation −8.61%, warm elapsed −2.4–2.9%; CSV allocation −2.1–3.3% | [Complete scripts and confirmation](../benchmarks/results/2026-11-05-known-list-storage/README.md). Report favorable; original native-CSV elapsed cost not reproduced in unchanged-bit confirmation and remains visible. Fully qualified on7efe581b. |
| Ordered native formatted bodies | Report warm elapsed −3.5–5.4%, async −5.6–7.2%; allocation flat | [Initial and refined sources](../benchmarks/results/2026-11-05-ordered-native-format/README.md). Possible text ~1% and compile ~2% costs accepted; fresh magnitude uncertain. Fully qualified on7efe581b. |
| Shared f-string construction | Report allocation −10.3% warm/async, −10.0% fresh; elapsed −7.0–8.9% warm, −5.8–7.8% async, −8.5–9.0% fresh | [Complete report source pair](../benchmarks/results/2026-11-05-formatted-construction/README.md). Numeric control near flat; compilation/control effects limit small claims. Fully qualified on0997baa8. |
| Prepared native helpers | Text warm elapsed −4.7–8.0%, async −5.3–7.3%; text compilation allocation −2.3% | [Whole source pair](../benchmarks/results/2026-11-05-prepared-native-bodies/README.md). Warm allocation flat; CSV/JSON confirmation near unchanged; possible CSV ~1% cost and fresh/report uncertainty retained. Qualified on8475a8dd. |
| Final-word casing / whitespace split | Text allocation −14.1% warm/async, −13.2% fresh; fresh elapsed −5.2–6.6%, async −4.9–5.2% | [Complete source pair](../benchmarks/results/2026-11-05-cased-whitespace-pipeline/README.md). Warm magnitude control-limited; JSON control +0.9–1.1% accepted, compilation inconclusive. Qualified on cf3dc9cd. |
| Arithmetic precedence consolidation | CSV/text compilation elapsed −6.7–8.4%; report −8.5–11.4%; allocation effectively unchanged | [Current-source reassessment](../benchmarks/results/2026-11-05-precedence-consolidation/README.md). Thirty parser lines removed. Fresh config −1.1–1.3%; warm text inconclusive with adverse signs preserved. Qualified on f7fed42c. |
| Native JSON container construction | JSON aggregation allocation −3.79%; report −2.24% | [Source pair](../benchmarks/results/2026-11-05-json-container-construction/README.md). Async report elapsed −4.3–4.7%; other timings have control/dispersion limits. Fresh config +0.8–1.3% (~2–4µs) accepted; qualified on ab487dfa. |
| Native dictionary ownership operations | CSV elapsed −5.0–5.1%; allocation unchanged | [Final refinement](../benchmarks/results/2026-11-04-dictionary-owner-operations/README.md). Text/async JSON near unchanged; full dispatch rewrite withdrawn. Qualified. |
| Shared completed JSON loop | JSON aggregation elapsed −2.1–3.8%; synchronous report −2.0–2.7%; allocation unchanged | [Both APIs and controls](../benchmarks/results/2026-11-04-completed-json-prefix/README.md). Text controls inconclusive; delivery qualified. |
| Bulk newline / CSV state parser | Long-field CSV elapsed −86.99%; allocation −2.54% | [Combined changes](../benchmarks/results/2026-11-04-csv-boundary-state/README.md). Twenty parser lines removed and two Python bugs fixed; typical warm CSV debt remains uncertain. Qualified. |
| Managed bulk newline scan | Complete long-line scripts elapsed −23–27%; allocation unchanged | [Source pair](../benchmarks/results/2026-11-04-bulk-newline-search/README.md). Seventeen net lines removed; possible warm CSV +1.2–2.8% debt retained. Qualified. |
| Shared UTF-8 search windows | Complete long-line prefix/count elapsed −22–23%; allocation unchanged | [Source pair](../benchmarks/results/2026-11-04-utf8-search-window/README.md). Fifty-nine net lines removed; possible warm CSV +0.6–3.6% debt retained. Qualified. |
| Shared compiler facts | Compilation allocation −4.25–4.83%; text/report compile elapsed −4–7% | [Module facts](../benchmarks/results/2026-11-04-module-lexical-facts/README.md). CSV control-limited; fresh scripts near unchanged. Qualified. |
| Native helper returns | CSV/JSON allocation −19.9% / −19.3% | [Original source pair](../benchmarks/results/2026-11-02-native-function-returns/README.md). Elapsed diagnostic; qualified. |
| Bulk CSV construction | Allocation −13.7–15.1%; CPU −7.2–11.1% | [Controlled comparison](../benchmarks/results/2026-11-02-native-csv-records/README.md). Original threshold misses and adverse control preserved; qualified. |
| Direct UTF-8 formatting | Allocation −8.67%; confirmation elapsed/CPU −5–8% | [Confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md). Original adverse timing remains unexplained; qualified. |

The latest external checkpoint compares **a8e476bf** with CPython, an identical-
source control and previous external **0997baa8**. Coupled text warm/fresh favors
current by about2–3%; reportwarm about4–5%, with dispersion limiting magnitude.
WarmJSON has conflicting current-role results; nativeCSV and compilation are near
unchanged. These coupled changes do not isolate D34's accepted roughly1% text
cost or prove it fixed. Original adverse points remain; percentages are not additive.

The [prepared-declaration cleanup](../benchmarks/results/2026-11-04-prepared-declarations/README.md)
removes retained metadata and duplicate construction paths. Its diagnostic source
pair does not establish a general CPU gain; report compilation is about0.7µs slower.

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves the other changes,
profiles, rejected designs and qualification details. [Existing comparison tools](../benchmarks/COMPARISON.md)
and [earlier measurements](../benchmarks/HISTORY.md) retain their own boundaries.
CPython is the primary external baseline; substantial text/report gaps remain.
