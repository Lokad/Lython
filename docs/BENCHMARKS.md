# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/native helpers |4,096 records /57,361 B|4.583–4.616 ms|3.479 ms|1.32–1.33×|9c1d7e74; RAM; Diagnostic;0.73% control /0.76% IQR|
| CSV summary/ordinary helper |4,096 records /57,361 B|5.128–5.281 ms|3.410 ms|1.50–1.55×|9c1d7e74; RAM; Variable;2.89% control /1.76% IQR|
| Text summary/helpers |3,072 lines /35,840 B|2.583–2.605 ms|1.292 ms|2.00–2.02×|9c1d7e74; RAM; Diagnostic;0.87% control /1.24% IQR|
| JSON-lines/helpers |4,096 records /110,592 B|4.728–4.833 ms|4.212 ms|1.12–1.15×|9c1d7e74; RAM; Variable;2.19% control /0.47% IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|3.652–3.670 ms|2.492 ms|1.47×|9c1d7e74; RAM; Variable;0.50% control /2.47% IQR|
| Config read/edit/write |1,024 paths /7,177 B|0.264–0.266 ms|0.255 ms|1.03–1.04×|9c1d7e74; RAM; Diagnostic;0.60% control /1.18% IQR|
| CSV long fields |16 records /524,717 B|0.346–0.348 ms|2.464 ms|0.14×|9c1d7e74; RAM; Diagnostic;0.70% control /1.23% IQR|
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Earlier bd3;ordinary FS; Diagnostic; 2.7% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Earlier bd3;ordinary FS; Variable; 3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Earlier bd3;ordinary FS; Diagnostic; 1.5% Lython IQR|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Earlier bd3;ordinary FS;1.1% Lython IQR|

First seven rows measured **2026-11-05**, retained production source **9c1d7e74**,
against **CPython3.13.16** on **.NET11RC3**. They measure **fresh compilation plus
one complete synchronous execution in an already-running host**, including
mediated RAM-file I/O, full checks and prior-result cleanup; staging/reset is
excluded. Ratio=Lython elapsed/CPython elapsed; above one means slower.

Ranges show two independent identical-build medians, not confidence intervals.
Control=their relative spread; IQR=interquartile range/median, showing the larger
current-role IQR. Variable rows have material control/dispersion uncertainty;
small differences are diagnostic, not portable performance guarantees. Exact
frozen core **6cb05f8e**, catalog, raw samples and identities are in the
[current checkpoint](../benchmarks/results/2026-11-05-current-cpython-checkpoint/README.md).
The measured runtime includes executable targets, owned CSV input and quoted-EOF
correction; the slower call-transport prototype is absent. CSV storage source
723b9638 and retained-source74de3388 have passed Windows/Ubuntu, actual .NET11/10,
original stress and installed-package checks. The [terminal receipt](../benchmarks/results/2026-11-05-current-cpython-checkpoint/delivery-ci-status.json)
is cached. Publication remains postponed.

Ordinary helpers execute ordinary Python functions under both runtimes. Fresh
text remains about2.0×CPython and report about1.5×; long-field CSV does not describe
ordinary CSV. Startup, genuine suspended-I/O latency, heap/RSS and embedding
latency remain unmeasured.

Four explicitly earlier file rows use 2026-11-03 **bd3f9dee** and ordinary filesystem;
filename scripts filter768 names/write256 selections, not traverse768 files.
The [earlier e958 checkpoint](../benchmarks/results/2026-11-05-dictionary-cpython-checkpoint/README.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve their own sources
and adverse results. Earlier90785737 search remains inconclusive (fresh270%IQR/
218% control drift; warm138%/77%). The
[clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
excludes staging symmetrically; it is not a runtime optimization.

## Warm execution, compilation and startup

Same9c1d source and invocation/I/O/check/cleanup boundary, reusing compiled code:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/native helpers |4.339–4.414 ms|3.355 ms|1.29–1.32×|1.70% control /1.03% IQR|
| CSV summary/ordinary helper |4.900–4.941 ms|3.199 ms|1.53–1.54×|0.83% control /0.91% IQR|
| Text summary/helpers |2.554–2.668 ms|1.173 ms|2.18–2.27×|4.45% control /0.82% IQR|
| JSON-lines/helpers |4.526–4.624 ms|4.086 ms|1.11–1.13×|2.12% control /0.90% IQR|
| JSON-to-Markdown report |3.553–3.603 ms|2.318 ms|1.53–1.55×|1.41% control /3.37% IQR|
| Config read/edit/write |0.189–0.190 ms|0.185 ms|1.02×|0.27% control /1.52% IQR|
| CSV long fields |0.203–0.206 ms|2.294 ms|0.09×|1.62% control /2.17% IQR|

Warm text has4.45% current spread; report IQR reaches3.37%. Neither supports small
timing claims. Do not subtract fresh/warm medians to infer compilation cost.
**Compilation only:**
CSV **69.959–71.040µs vs54.995µs (1.27–1.29×)**, report **49.997–52.087µs vs37.831µs (1.32–1.38×)**; execution/I/O excluded. CSV control1.55%/maxIQR3.11%, report4.18%/3.15% limit precision. **Startup unmeasured.**

## Machine and included work

Dedicated Azure **Standard_F4as_v7**, Ubuntu 24.04.4 x86-64, four AMD EPYC 9V45
vCPUs, one thread/core, 16 GiB RAM/no swap. CPython is GCC 13.3 PGO/LTO, ordinary
GIL/GC, no JIT/debug, launched `-I -S -X utf8`. SDK/runtime:
**11.0.100-rc.3.26522.148 /11.0.0-rc.3.26522.148**. Engines run sequentially
in independent processes with ordinary worker GC/tiering, quiet gates and
independent unchanged controls.16jobs complete in440.59s, within480s target/540s owned bound.

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

The [owned native CSV input comparison](../benchmarks/results/2026-11-05-csv-owned-input/README.md)
measures c10ddd55 against qualified7dcc3b48: long-field CSV warm elapsed is
18.3–18.6% lower with0.33% identical-current spread. Ordinary CSV magnitudes
remain control-limited; unchanged text includes+3.46%/−1.18% conflicting roles.
Allocation and attributed CPU are unmeasured. This targeted source pair does
not replace the representative external baseline above.

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

The [current process-isolated checkpoint](../benchmarks/results/2026-11-05-current-cpython-checkpoint/README.md)
compares9c1d with CPython, an identical-current role and fully qualified7dcc/a4ce
before the CSV input changes. Native CSV fresh favors current by2.0–2.7%, ordinary
CSV warm by2.6–3.4%; fresh ordinary CSV favors current by3.2–6.0% but its control
spread limits magnitude. Long-field CSV warm favors current by17.1–18.4%, fresh
by13.2–13.8%. Text warm−2.28/+2.08% and freshJSON+1.66/−.56% remain inconclusive;
report/compilation dispersion limits small claims. These coupled observations
do not isolate individual changes or close earlier roughly1% text debt. All
earlier adverse points remain; percentages are not additive.

The [prepared-declaration cleanup](../benchmarks/results/2026-11-04-prepared-declarations/README.md)
removes retained metadata and duplicate construction paths. Its diagnostic source
pair does not establish a general CPU gain; report compilation is about0.7µs slower.

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves the other changes,
profiles, rejected designs and qualification details. [Existing comparison tools](../benchmarks/COMPARISON.md)
and [earlier measurements](../benchmarks/HISTORY.md) retain their own boundaries.
CPython is the primary external baseline; substantial text/report gaps remain.
