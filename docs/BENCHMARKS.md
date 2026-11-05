# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers | 4,096 records / 57,361 B | 5.013 ms | 3.485 ms | 1.44× | f6e51d1c; RAM; Diagnostic; 1.06% control / 1.22% IQR |
| JSON-lines/helpers | 4,096 records / 110,592 B | 5.256 ms | 4.116 ms | 1.28× | f6e51d1c; RAM; Control-limited; 2.40% control / 1.40% IQR |
| Text summary/helpers | 3,072 lines / 35,840 B | 3.123 ms | 1.286 ms | 2.43× | f6e51d1c; RAM; Diagnostic; 0.79% control / 0.85% IQR |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 4.348 ms | 2.551 ms | 1.70× | f6e51d1c; RAM; Variable; 3.29% control / 2.91% IQR |
| Config read/edit/write | 1,024 paths / 7,177 B | 0.273 ms | 0.255 ms | 1.07× | f6e51d1c; RAM; Diagnostic; 0.13% control / 1.51% IQR |
| CSV long fields | 16 records / 524,717 B | 0.406 ms | 2.423 ms | 0.17× | f6e51d1c; RAM; Diagnostic; 0.95% control / 1.70% IQR |
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Earlier bd3;ordinary FS; Diagnostic; 2.7% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Earlier bd3;ordinary FS; Variable; 3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Earlier bd3;ordinary FS; Diagnostic; 1.5% Lython IQR|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Earlier bd3;ordinary FS;1.1% Lython IQR|

First six rows measured **2026-11-04**, committed runtime source **f6e51d1c**,
against **CPython 3.13.16** on **.NET 11 RC3**. They measure **fresh compilation plus
one complete synchronous execution in an already-running host**, including mediated
RAM-file I/O, full checks and prior-result cleanup, excluding staging/reset.
Ratio=Lython elapsed/CPython elapsed; above one means slower. IQR=interquartile
range/median; control=absolute difference of identical-source role medians/current
median. Exact precommit core **df206f47** and snapshots identify the measured build.
Four explicitly earlier file rows use 2026-11-03 **bd3f9dee** and ordinary filesystem;
filename scripts filter768 names/write256 selections, not traverse768 files.

Long-field CSV benefits from bulk scanning; it does not describe ordinary CSV.
Text/report gaps remain. Report Lython/control IQR2.91%/2.03% and opposing warm/fresh
control directions limit small claims. Startup, genuinely suspended I/O, heap/RSS
and embedding latency remain unmeasured. Full platform/Stress/package checks pass
on f6e51d1c. [Current observations, controls and exact identities](../benchmarks/results/2026-11-04-runtime-cpython-declaration-checkpoint/README.md).

[Previous5c checkpoint](../benchmarks/results/2026-11-04-csv-runtime-cpython-checkpoint/README.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve earlier sources,
conditions and adverse results. Earlier90785737 search remains inconclusive
(fresh270%IQR/218%control drift; warm138%/77%). The [clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
excludes staging symmetrically; it is not a runtime optimization.

## Warm execution, compilation and startup

Latest external source **f6e51d1c**, RAM files, reused compiled code with the same
invocation/I/O/check/cleanup boundary:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV summary/helpers | 4.874 ms | 3.339 ms | 1.46× | 2.23% / 0.56% |
| JSON-lines/helpers | 5.013 ms | 4.051 ms | 1.24× | 0.78% / 0.77% |
| Text summary/helpers | 2.898 ms | 1.176 ms | 2.46× | 2.33% / 0.40% |
| JSON-to-Markdown report | 4.367 ms | 2.367 ms | 1.84× | 2.93% / 3.20% |
| Config read/edit/write | 0.191 ms | 0.186 ms | 1.03× | 0.15% / 0.83% |
| CSV long fields | 0.253 ms | 2.368 ms | 0.11× | 0.40% / 1.73% |

Warm identical-source spreads0.15–2.93% limit small claims; report remains variable.
Do not infer compile cost by subtracting fresh/warm medians. **Compilation only:**
CSV80.49µs vs54.86µs (**1.47×**), report55.34µs vs37.70µs (**1.47×**);
Lython IQR2.42%/2.53%, controls2.27%/2.25%, execution/I/O excluded.
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
| Native JSON container construction | JSON aggregation allocation −3.79%; report −2.24% | [Source pair](../benchmarks/results/2026-11-05-json-container-construction/README.md). Async report elapsed −4.3–4.7%; other timings have control/dispersion limits. Fresh config +0.8–1.3% (~2–4µs) accepted; full delivery pending. |
| Native dictionary ownership operations | CSV elapsed −5.0–5.1%; allocation unchanged | [Final refinement](../benchmarks/results/2026-11-04-dictionary-owner-operations/README.md). Text/async JSON near unchanged; full dispatch rewrite withdrawn. Qualified. |
| Shared completed JSON loop | JSON aggregation elapsed −2.1–3.8%; synchronous report −2.0–2.7%; allocation unchanged | [Both APIs and controls](../benchmarks/results/2026-11-04-completed-json-prefix/README.md). Text controls inconclusive; delivery qualified. |
| Bulk newline / CSV state parser | Long-field CSV elapsed −86.99%; allocation −2.54% | [Combined changes](../benchmarks/results/2026-11-04-csv-boundary-state/README.md). Twenty parser lines removed and two Python bugs fixed; typical warm CSV debt remains uncertain. Qualified. |
| Managed bulk newline scan | Complete long-line scripts elapsed −23–27%; allocation unchanged | [Source pair](../benchmarks/results/2026-11-04-bulk-newline-search/README.md). Seventeen net lines removed; possible warm CSV +1.2–2.8% debt retained. Qualified. |
| Shared UTF-8 search windows | Complete long-line prefix/count elapsed −22–23%; allocation unchanged | [Source pair](../benchmarks/results/2026-11-04-utf8-search-window/README.md). Fifty-nine net lines removed; possible warm CSV +0.6–3.6% debt retained. Qualified. |
| Shared compiler facts | Compilation allocation −4.25–4.83%; text/report compile elapsed −4–7% | [Module facts](../benchmarks/results/2026-11-04-module-lexical-facts/README.md). CSV control-limited; fresh scripts near unchanged. Qualified. |
| Native helper returns | CSV/JSON allocation −19.9% / −19.3% | [Original source pair](../benchmarks/results/2026-11-02-native-function-returns/README.md). Elapsed diagnostic; qualified. |
| Bulk CSV construction | Allocation −13.7–15.1%; CPU −7.2–11.1% | [Controlled comparison](../benchmarks/results/2026-11-02-native-csv-records/README.md). Original threshold misses and adverse control preserved; qualified. |
| Direct UTF-8 formatting | Allocation −8.67%; confirmation elapsed/CPU −5–8% | [Confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md). Original adverse timing remains unexplained; qualified. |

The latest external checkpoint compares **f6e51d1c** with CPython, an identical-source
control and previous external **5c827e68**. Typical scripts are near unchanged or
control-limited; report fresh favors the current source but is variable. Earlier
adverse results remain evidence; independent percentages are not additive.

The [prepared-declaration cleanup](../benchmarks/results/2026-11-04-prepared-declarations/README.md)
removes retained metadata and duplicate construction paths. Its diagnostic source
pair does not establish a general CPU gain; report compilation is about0.7µs slower.

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves the other changes,
profiles, rejected designs and qualification details. [Existing comparison tools](../benchmarks/COMPARISON.md)
and [earlier measurements](../benchmarks/HISTORY.md) retain their own boundaries.
CPython is the primary external baseline; substantial text/report gaps remain.
