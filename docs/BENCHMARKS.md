# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers | 4,096 records / 57,361 B | 5.157 ms | 3.458 ms | 1.49× | 0997baa8; RAM; Diagnostic; 1.43% control / 0.66% IQR |
| JSON-lines/helpers | 4,096 records / 110,592 B | 5.022 ms | 4.225 ms | 1.19× | 0997baa8; RAM; Diagnostic; 1.09% control / 1.31% IQR |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.983–3.082 ms | 1.354 ms | 2.20–2.28× | 0997baa8; RAM; Variable; 3.32% control / 0.82% IQR |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.935 ms | 2.498 ms | 1.57× | 0997baa8; RAM; Variable; 0.87% control / 4.24% IQR |
| Config read/edit/write | 1,024 paths / 7,177 B | 0.265 ms | 0.253 ms | 1.05× | 0997baa8; RAM; Diagnostic; 1.00% control / 2.64% IQR |
| CSV long fields | 16 records / 524,717 B | 0.400 ms | 2.432 ms | 0.16× | 0997baa8; RAM; Diagnostic; 0.13% control / 0.61% IQR |
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Earlier bd3;ordinary FS; Diagnostic; 2.7% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Earlier bd3;ordinary FS; Variable; 3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Earlier bd3;ordinary FS; Diagnostic; 1.5% Lython IQR|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Earlier bd3;ordinary FS;1.1% Lython IQR|

First six rows measured **2026-11-05**, committed runtime source **0997baa8**,
against **CPython 3.13.16** on **.NET 11 RC3**. They measure **fresh compilation plus
one complete synchronous execution in an already-running host**, including mediated
RAM-file IO, full checks and prior-result cleanup, excluding staging/reset.
Ratio=Lython elapsed/CPython elapsed; above one means slower. IQR=interquartile
range/median. Control compares identical-source role medians. Exact precommit core
**68a9f3f8** and snapshots identify the measured build. This source passes all
Windows/Ubuntu, actual .NET 11/10, original Stress and installed Release consumer
gates. Later prepared-format and list-storage changes are measured separately below.

Text ranges show medians of identical-source roles, not confidence intervals.
Fresh controls differ 3.32%; warm controls differ 5.85%. Earlier text collections
also varied and remain preserved. Warm text is measured separately below.
Report also remains variable. These limits
do not erase the substantial text/report gaps. Long-field CSV does not describe
ordinary CSV. Startup, genuine suspended IO, heap/RSS and embedding latency remain
unmeasured. [Current observations, conditions and exact identities](../benchmarks/results/2026-11-05-runtime-cpython-construction-checkpoint/README.md).

Four explicitly earlier file rows use 2026-11-03 **bd3f9dee** and ordinary filesystem;
filename scripts filter768 names/write256 selections, not traverse768 files.
[Previous cf3 checkpoint](../benchmarks/results/2026-11-05-runtime-cpython-pipeline-checkpoint/README.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve earlier sources and
adverse results. Earlier90785737 search remains inconclusive (fresh270%IQR/218%
control drift; warm138%/77%). The [clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
excludes staging symmetrically; it is not a runtime optimization.

## Warm execution, compilation and startup

Latest external source **0997baa8**, RAM files, reused compiled code with the same
invocation/IO/check/cleanup boundary:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV summary/helpers | 4.866 ms | 3.345 ms | 1.45× | 0.09% / 0.82% |
| JSON-lines/helpers | 4.806 ms | 4.084 ms | 1.18× | 1.72% / 0.77% |
| Text summary/helpers | 2.673–2.839 ms | 1.151 ms | 2.32–2.47× | 5.85% / 1.12% |
| JSON-to-Markdown report | 3.952 ms | 2.329 ms | 1.70× | 2.13% / 2.86% |
| Config read/edit/write | 0.190 ms | 0.186 ms | 1.02× | 0.75% / 0.21% |
| CSV long fields | 0.253 ms | 2.296 ms | 0.11× | 0.70% / 2.42% |

Identical-source spreads 0.09–5.85% limit small claims; text/report remain variable.
Do not subtract fresh/warm medians to infer compile cost. **Compilation only:**
CSV70.69µs vs54.47µs (**1.30×**),
report50.79µs vs37.60µs (**1.35×**);
Lython IQR1.54%/3.14%, controls1.00%/0.21%, execution/IO excluded.
**Startup unmeasured.** [Earlier phases/revisions](../benchmarks/EXPERIMENTS.md)
retain their own conditions; older measurements do not describe this source.

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
| Private CSV record arrays | CSV allocation −4.7–7.6%; public-reader warm elapsed −4.2–5.3% | [Source variants and confirmation](../benchmarks/results/2026-11-05-private-csv-records/README.md). DictReader warm timing control-limited; roughly1% warm-text cost accepted, larger original adverse point preserved. Focused checks pass; full delivery pending. |
| Known/empty list backing | Text allocation −8.61%, warm elapsed −2.4–2.9%; CSV allocation −2.1–3.3% | [Complete scripts and confirmation](../benchmarks/results/2026-11-05-known-list-storage/README.md). Report favorable; original native-CSV elapsed cost not reproduced in unchanged-bit confirmation and remains visible. Focused checks pass; full delivery pending. |
| Ordered native formatted bodies | Report warm elapsed −3.5–5.4%, async −5.6–7.2%; allocation flat | [Initial and refined sources](../benchmarks/results/2026-11-05-ordered-native-format/README.md). Possible text ~1% and compile ~2% costs accepted; fresh magnitude uncertain. Focused checks pass; full qualification batched at next delivery. |
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

The latest external checkpoint compares **0997baa8** with CPython, an identical-
source control and previous external **cf3dc9cd**. Coupled report warm/fresh favors
current source by about6%, with dispersion limiting its magnitude. Text differences
are control-limited; other workloads and compilation are near unchanged at this
resolution. Earlier adverse results remain. These combined effects do not identify
individual causes; percentages are not additive.

The [prepared-declaration cleanup](../benchmarks/results/2026-11-04-prepared-declarations/README.md)
removes retained metadata and duplicate construction paths. Its diagnostic source
pair does not establish a general CPU gain; report compilation is about0.7µs slower.

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves the other changes,
profiles, rejected designs and qualification details. [Existing comparison tools](../benchmarks/COMPARISON.md)
and [earlier measurements](../benchmarks/HISTORY.md) retain their own boundaries.
CPython is the primary external baseline; substantial text/report gaps remain.
