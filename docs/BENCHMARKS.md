# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records / 57,361 B|5.082 ms|3.438 ms|1.48×|5c827e68; RAM; Diagnostic;0.52% control / 1.48% IQR|
| JSON-lines/helpers |4,096 records / 110,592 B|5.361 ms|4.139 ms|1.30×|5c827e68; RAM; Control-limited;2.19% control / 1.25% IQR|
| Text summary/helpers |3,072 lines / 35,840 B|3.107 ms|1.297 ms|2.40×|5c827e68; RAM; Control-limited;2.65% control / 1.03% IQR|
| JSON-to-Markdown report |3,072 records / 189,441 B|4.536 ms|2.535 ms|1.79×|5c827e68; RAM; Variable;2.20% control / 1.06% IQR|
| Config read/edit/write |1,024 paths / 7,177 B|0.279 ms|0.255 ms|1.09×|5c827e68; RAM; Diagnostic;0.18% control / 0.79% IQR|
| CSV long fields |16 records / 524,717 B|0.417 ms|2.516 ms|0.17×|5c827e68; RAM; Variable;3.84% control / 5.31% IQR|
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Earlier bd3;ordinary FS; Diagnostic; 2.7% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Earlier bd3;ordinary FS; Variable; 3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Earlier bd3;ordinary FS; Diagnostic; 1.5% Lython IQR|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Earlier bd3;ordinary FS;1.1% Lython IQR|

First six rows measured **2026-11-04**, committed runtime source **5c827e68**,
against **CPython 3.13.16** on **.NET 11 RC3**. These are **fresh compilation plus one
complete synchronous execution in an already-running host**, including mediated
RAM-file I/O, full checks and prior-result cleanup, excluding fixture staging/reset.
Ratio=Lython elapsed/CPython elapsed; above one means slower. IQR=interquartile
range/median; control is identical-source Lython-role spread. Exact measured
precommit core **21cddae7** and source snapshots distinguish source from build metadata.
Four explicitly earlier file rows use 2026-11-03 **bd3f9dee** and the ordinary filesystem;
filename scripts filter 768 names/write 256 selections, not traverse 768 files.

The long-field case shows the benefit of bulk scanning; it does not describe all
CSV work. The usual text/report gaps remain. Long-field Lython 5.31% IQR and report
control 6.15% IQR limit precision. Startup, genuinely suspended I/O, heap/RSS and
embedding latency remain unmeasured. Runtime qualification is independent;
full platform/Stress/package checks on 5c827e68 pass. [Current observations,
controls and exact identities](../benchmarks/results/2026-11-04-csv-runtime-cpython-checkpoint/README.md).

[Previous a00 checkpoint](../benchmarks/results/2026-11-04-utf8-runtime-cpython-checkpoint/README.md)
and [earlier experimental history](../benchmarks/EXPERIMENTS.md) retain original
sources, conditions and adverse results. Earlier 90785737 search remains inconclusive
(fresh 270% IQR / 218% control drift; warm 138% / 77%). The [clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
excludes staging symmetrically; it is not a runtime optimization.

## Warm execution, compilation and startup

Latest external source **5c827e68**, RAM files, reused compiled code with the same
invocation/I/O/check/cleanup boundary:

| Warm workload | Lython | CPython | Ratio | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV summary/helpers |4.994 ms|3.332 ms|1.50×|0.42% /0.76%|
| JSON-lines/helpers |5.333 ms|4.305 ms|1.24×|0.52% /2.31%|
| Text summary/helpers |3.017 ms|1.150 ms|2.62×|1.03% /0.72%|
| JSON-to-Markdown report |4.467 ms|2.398 ms|1.86×|3.47% /2.16%|
| Config edit/write |0.198 ms|0.187 ms|1.06×|3.00% /2.71%|
| CSV long fields |0.255 ms|2.427 ms|0.10×|5.52% /3.55%|

Warm identical-source spreads 0.66–3.02% limit small claims. Report and long-field
rows remain variable. Do not infer compile cost by subtracting fresh/warm medians.
**Compilation only:** CSV 79.96 µs vs 55.01 µs (**1.45×**), report 56.41 µs vs 37.67 µs
(**1.50×**); Lython IQR 3.83% / 4.69%, controls 0.22% / 3.65%, execution/I/O excluded.
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
| Native dictionary ownership operations | CSV elapsed −5.0–5.1%; allocation unchanged | [Final refinement](../benchmarks/results/2026-11-04-dictionary-owner-operations/README.md). Text/async JSON near unchanged; full dispatch rewrite withdrawn. New-source delivery pending. |
| Shared completed JSON loop | JSON aggregation elapsed −2.1–3.8%; synchronous report −2.0–2.7%; allocation unchanged | [Both APIs and controls](../benchmarks/results/2026-11-04-completed-json-prefix/README.md). Text controls inconclusive; delivery qualified. |
| Bulk newline / CSV state parser | Long-field CSV elapsed −86.99%; allocation −2.54% | [Combined changes](../benchmarks/results/2026-11-04-csv-boundary-state/README.md). Twenty parser lines removed and two Python bugs fixed; typical warm CSV debt remains uncertain. Qualified. |
| Managed bulk newline scan | Complete long-line scripts elapsed −23–27%; allocation unchanged | [Source pair](../benchmarks/results/2026-11-04-bulk-newline-search/README.md). Seventeen net lines removed; possible warm CSV +1.2–2.8% debt retained. Qualified. |
| Shared UTF-8 search windows | Complete long-line prefix/count elapsed −22–23%; allocation unchanged | [Source pair](../benchmarks/results/2026-11-04-utf8-search-window/README.md). Fifty-nine net lines removed; possible warm CSV +0.6–3.6% debt retained. Qualified. |
| Shared compiler facts | Compilation allocation −4.25–4.83%; text/report compile elapsed −4–7% | [Module facts](../benchmarks/results/2026-11-04-module-lexical-facts/README.md). CSV control-limited; fresh scripts near unchanged. Qualified. |
| Native helper returns | CSV/JSON allocation −19.9% / −19.3% | [Original source pair](../benchmarks/results/2026-11-02-native-function-returns/README.md). Elapsed diagnostic; qualified. |
| Bulk CSV construction | Allocation −13.7–15.1%; CPU −7.2–11.1% | [Controlled comparison](../benchmarks/results/2026-11-02-native-csv-records/README.md). Original threshold misses and adverse control preserved; qualified. |
| Direct UTF-8 formatting | Allocation −8.67%; confirmation elapsed/CPU −5–8% | [Confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md). Original adverse timing remains unexplained; qualified. |

The current coupled checkpoint compares **5c827e68** with CPython, an identical-source
control and qualified **a00fe76d**. Long-field gains persist; common scripts remain
near unchanged or control-limited against that reference. Earlier adverse results
remain evidence, even where later coupled work offsets them.

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves the other changes,
profiles, rejected designs and qualification details. [Existing comparison tools](../benchmarks/COMPARISON.md)
and [earlier measurements](../benchmarks/HISTORY.md) retain their own boundaries.
CPython is the primary external baseline; substantial text/report gaps remain.
