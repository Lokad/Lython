# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/native helpers |4,096 records /57,361 B|4.764 ms|3.476 ms|1.37×|e958c9b1; RAM; Diagnostic;1.69% control /1.62% IQR|
| CSV summary/ordinary helper |4,096 records /57,361 B|5.296 ms|3.375 ms|1.57×|e958c9b1; RAM; Diagnostic;0.67% control /1.15% IQR|
| Text summary/helpers |3,072 lines /35,840 B|2.745–2.821 ms|1.270 ms|2.16–2.22×|e958c9b1; RAM; Variable;2.71% control /0.65% IQR|
| JSON-lines/helpers |4,096 records /110,592 B|4.790–4.972 ms|4.212 ms|1.14–1.18×|e958c9b1; RAM; Variable;3.67% control /0.61% IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|3.608–3.705 ms|2.474 ms|1.46–1.50×|e958c9b1; RAM; Variable;2.67% control /2.88% IQR|
| Config read/edit/write |1,024 paths /7,177 B|0.266 ms|0.253 ms|1.05×|e958c9b1; RAM; Diagnostic;0.06% control /0.88% IQR|
| CSV long fields |16 records /524,717 B|0.396 ms|2.439 ms|0.16×|e958c9b1; RAM; Diagnostic;0.57% control /1.61% IQR|
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Earlier bd3;ordinary FS; Diagnostic; 2.7% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Earlier bd3;ordinary FS; Variable; 3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Earlier bd3;ordinary FS; Diagnostic; 1.5% Lython IQR|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Earlier bd3;ordinary FS;1.1% Lython IQR|

First seven rows measured **2026-11-05**, retained source **e958c9b1**, against
**CPython3.13.16** on **.NET11RC3**. They measure **fresh compilation plus one
complete synchronous execution in an already-running host**, including mediated
RAM-file I/O, full checks and prior-result cleanup, excluding staging/reset.
Ratio=Lython elapsed/CPython elapsed; above one means slower. IQR=interquartile
range/median. Control compares identical-source role medians. Exact frozen core
**1f5919c0** and snapshots identify the measured build. Qualification on e958c9b1
passes Windows/Ubuntu, actual11/10, original stress and installed-package checks;
the terminal [CI receipt](../benchmarks/results/2026-11-05-native-dictionary-backing/delivery-ci-status.json)
is cached. Later95c10298 adds the comparison worker's final zero-balance assertion,
outside timing; the same worker is used beside both source images.
The later target-instruction source pair below does not replace these seven-script
measurements; it has separately passed full qualification on7dcc3b48.

Text/JSON/report ranges show identical-source medians, not confidence intervals.
Fresh text has2.71% current spread, JSON3.67%, report2.67%; these limit small claims.
Ordinary-function CSV uses ordinary helpers under both runtimes. Text remains
about2.2×CPython; long-field CSV does not describe ordinary CSV. Startup, genuine
suspended I/O, heap/RSS and embedding latency remain unmeasured.
[Current observations and exact identities](../benchmarks/results/2026-11-05-dictionary-cpython-checkpoint/README.md).

Four explicitly earlier file rows use 2026-11-03 **bd3f9dee** and ordinary filesystem;
filename scripts filter768 names/write256 selections, not traverse768 files.
[Previous402 checkpoint](../benchmarks/results/2026-11-05-runtime-cpython-csv-checkpoint/README.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve earlier sources and
adverse results. Earlier90785737 search remains inconclusive (fresh270%IQR/218%
control drift; warm138%/77%). The [clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
excludes staging symmetrically; it is not a runtime optimization.

## Warm execution, compilation and startup

Latest external source **e958c9b1**, RAM files, reused compiled code with the
same invocation/I/O/check/cleanup boundary:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV summary/native helpers |4.581 ms|3.408 ms|1.34×|1.36% /0.53%|
| CSV summary/ordinary helper |5.179 ms|3.235 ms|1.60×|1.59% /0.95%|
| Text summary/helpers |2.598 ms|1.181 ms|2.20×|1.43% /0.46%|
| JSON-lines/helpers |4.606–4.711 ms|4.070 ms|1.13–1.16×|2.29% /0.67%|
| JSON-to-Markdown report |3.455–3.548 ms|2.306 ms|1.50–1.54×|2.69% /2.34%|
| Config read/edit/write |0.190 ms|0.183 ms|1.04×|0.01% /1.40%|
| CSV long fields |0.248 ms|2.291 ms|0.11×|0.47% /1.50%|

Warm native-CSV CPythonIQR4.29% and report LythonIQR2.34% limit precision. Do not
subtract fresh/warm medians to infer compile cost. **Compilation only:**
CSV69.62µs vs54.61µs (**1.27×**), report50.30µs vs37.79µs (**1.33×**);
LythonIQR1.61%/2.04%, controls.04%/.55%, execution/I/O excluded.
**Startup unmeasured.** [Earlier phases/revisions](../benchmarks/EXPERIMENTS.md)
retain their own conditions.

## Machine and included work

Dedicated Azure **Standard_F4as_v7**, Ubuntu 24.04.4 x86-64, four AMD EPYC 9V45
vCPUs, one thread/core, 16 GiB RAM/no swap. CPython is GCC 13.3 PGO/LTO, ordinary
GIL/GC, no JIT/debug, launched `-I -S -X utf8`. SDK/runtime:
**11.0.100-rc.3.26522.148 /11.0.0-rc.3.26522.148**. Engines run sequentially
in independent processes with ordinary worker GC/tiering, quiet gates and
independent unchanged controls.17jobs complete in466.52s, within540s owned bound.

Both clocks include invocation setup, actual mediated guest file I/O, complete
stdout/file reads/checks and prior-result disposal within each batch. Fresh Lython
also disposes each per-invocation compilation inside the timer. Every invocation
resets/stages fresh fixture files **outside both execution clocks**; all work still
counts toward wall deadlines. The final result stays funded through response
delivery, then disposes outside that batch timer. Compile-only retains its last
compilation; cached programs dispose at worker exit. Startup, transport/response
hashing and final cached-owner/process/fixture teardown are excluded. Every worker
is joined after cleanup; Lython exit requires final reservations zero. Fresh globals
are used; CPython process module/library caches persist, while Lython execution
caches are isolated. Actual suspended-I/O semantics are tested separately; these
use `Run`.

The host supplies efficient ranged reads/appends. Containment, Python conversion,
output verification and host-boundary work are included; this is not a guest-
dispatch-only measurement. Exact worker procedures and limitations are linked below.

## Improvements against earlier Lython

These are separate source-pair results, not CPython ratios or additive gains.
Each link records its measured revisions, controls and limitations.

| Change | Established result | Evidence and limitations |
| --- | --- | --- |
| Executable target instructions | Text fresh elapsed −7.27–7.31%, completed-host async −5.60–5.67%; ordinary CSV warm −2.04–2.36% | [Whole-family deletion and controls](../benchmarks/results/2026-11-05-executable-targets/README.md). Warm-text magnitude limited; report control inconclusive; allocation and real-I/O latency unmeasured. Fully qualified on7dcc3b48. |
| Native dictionary backing | Construction variant: CSV allocation −3.3–5.2%, JSON −6.9%, report −4.49%; ordinary CSV elapsed 5.6–8.5% lower, JSON 4.1–4.3% lower | [Three frozen sources and controls](../benchmarks/results/2026-11-05-native-dictionary-backing/README.md). Final removal completion has separate checks; its two-job elapsed comparison is inconclusive with 6.6–9.2% unchanged-control spread. Fully qualified on e958c9b1. |
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

The latest process-isolated checkpoint compares **e958c9b1** with CPython,
an identical-source control and previous external **a8e476bf/402ae9f1**. Coupled
ordinary CSV warm/fresh favors current by5–7%, native CSV by2–4%, report by5–8%.
Warm text favors current by1.8–3.2%; fresh text is **+1.14%/−1.60%** across the
two current roles, so that small change is unresolved. Warm JSON is favorable;
fresh JSON/control dispersion limits magnitude. Config/long-field CSV and
compilation are near unchanged. The cache-drain CSV diagnostic favors current
by9.3–9.5% with.25% current spread; it does not isolate the dictionary component.
These coupled changes do not isolate D34's accepted roughly1% text cost or prove
it fixed. All earlier adverse points remain; percentages are not additive.

The [prepared-declaration cleanup](../benchmarks/results/2026-11-04-prepared-declarations/README.md)
removes retained metadata and duplicate construction paths. Its diagnostic source
pair does not establish a general CPU gain; report compilation is about0.7µs slower.

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves the other changes,
profiles, rejected designs and qualification details. [Existing comparison tools](../benchmarks/COMPARISON.md)
and [earlier measurements](../benchmarks/HISTORY.md) retain their own boundaries.
CPython is the primary external baseline; substantial text/report gaps remain.
