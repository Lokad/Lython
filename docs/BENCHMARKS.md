# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records /57,361 B|5.681 ms|3.546 ms|1.60×|Diagnostic;1.2% Lython IQR|
| JSON-lines/helpers |4,096 records /110,592 B|5.897 ms|4.251 ms|1.39×|Diagnostic;0.8% Lython IQR|
| Text summary/helpers |3,072 lines /35,840 B|3.724 ms|1.297 ms|2.87×|Diagnostic;0.8% Lython IQR|
| Search with early exit |42,010 B file|0.491 ms|0.245 ms|2.01×|Diagnostic;1.0% Lython IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|5.815 ms|2.542 ms|2.29×|Variable;4.5% control drift|
| Unicode filename filter |768 names /124,050 B|0.874 ms|0.422 ms|2.07×|Diagnostic;0.4% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.774 ms|0.292 ms|2.65×|Diagnostic;1.6% Lython IQR|
| Config read/edit/write |7,177 B|3.668 ms|1.960 ms|1.87×|Historical082179c4; v1 clock|
| Copy/checksum/manifest |Two files /4,107 B|2.164 ms|0.569 ms|3.80×|Historical082179c4; v1 clock|

Seven current rows measured **2026-11-03**, runtime **f7dd0d3a**, comparison worker
**02efd797**, against **CPython3.13.16** on **.NET11RC3**. They use **protocol2**:
**fresh compilation plus one complete synchronous execution in an already-running
host**, with fixture reset excluded. Ratio is Lython elapsed divided by CPython
elapsed; above one means slower. IQR is interquartile range divided by median.
The filename scripts read names, filter them and write/check256 selected names;
they do not traverse768 files. Config/copy retain their2026-11-02 historical revision
and included-staging clock; their Lython IQR is6.1%/10.2%. They are not current rows.

These short diagnostics are not portable release multipliers. Fresh controls drift
up to4.5%. The [clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
found fixture reset alone cost1.4–1.6ms for Lython versus0.11–0.18ms for CPython.
The [earlier included-staging ratios](../benchmarks/results/2026-11-03-wildcard-cpython-checkpoint/README.md)
remain valid for their original boundary; the corrected table is **not a runtime
speedup**. The [earlier90785737 search](../benchmarks/results/2026-11-02-current-cpython-checkpoint/README.md)
remains **inconclusive**: fresh270% IQR/218% control drift, warm138%/77%.
Steadier later results do not explain that waiting. All adverse data are retained.
[Current observations and controls](../benchmarks/results/2026-11-03-corrected-cpython-checkpoint/README.md).

## Warm execution, compilation and startup

Reused compiled code, with the same protocol2 invocation/file/check boundary:

| Warm workload | Lython | CPython | Ratio | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV summary/helpers |5.492 ms|3.391 ms|1.62×|0.6% /0.4%|
| JSON-lines/helpers |5.523 ms|4.147 ms|1.33×|0.7% /0.3%|
| Text summary/helpers |3.592 ms|1.186 ms|3.03×|0.6% /1.0%|
| Search with early exit |0.358 ms|0.151 ms|2.38×|0.3% /1.6%|
| JSON-to-Markdown report |5.735 ms|2.392 ms|2.40×|2.1% /1.2%|
| Unicode filename filter |0.789 ms|0.332 ms|2.37×|0.6% /1.0%|
| ASCII filename filter |0.638 ms|0.211 ms|3.03×|0.3% /0.6%|
| Config edit (historical082179c4, v1) |3.792 ms|2.033 ms|1.87×|3.4% /5.2%|
| Copy/manifest (historical082179c4, v1) |2.202 ms|0.441 ms|4.99×|7.5% /1.2%|

Warm JSON/search controls differ3.6%/4.9%; other controls0.1–2.9%. Independent
phase or revision medians are not additive and do not establish a source speedup.
**Compilation only:** CSV87.17µs versus54.70µs (**1.59×**), report61.40µs versus
37.74µs (**1.63×**). Identical controls differ1.3%/3.0%; no guest execution included.
**Startup is unmeasured here.**
[Historical startup observations](../benchmarks/results/2026-10-29-foundation/README.md)
describe their original revisions.

## Machine and included work

Dedicated Azure **Standard_F4as_v7**, Ubuntu24.04.4 x86-64, four AMD EPYC9V45
vCPUs, one thread/core,16GiB RAM/no swap. CPython is GCC13.3 PGO/LTO, ordinary
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

These source-pair improvements are separate from the CPython ratios above;
original elapsed observations retain their own recorded timing boundaries:

| Change | Established result | Evidence and limitations |
| --- | --- | --- |
| Native helper returns |CSV/JSON allocation−19.9%/−19.3%|[Elapsed diagnostic](../benchmarks/results/2026-11-02-native-function-returns/README.md)|
| Bulk CSV construction |Allocation−13.7–15.1%, CPU−7.2–11.1%|[Controlled comparison](../benchmarks/results/2026-11-02-native-csv-records/README.md)|
| Direct UTF-8 formatting |Allocation−8.67%; confirmation elapsed/CPU−5–8%|[Original adverse timing preserved](../benchmarks/results/2026-11-02-formatting-confirmation/README.md)|
| Native local forwarding |Named CSV/text allocation−30.5%/−34.7%; warm elapsed−5–8%/−8–10%|[Complete scripts and controls](../benchmarks/results/2026-11-02-native-local-forwarding/README.md)|
| Shared JSON names |Report allocation−13.8%, warm elapsed−9–13%|[Unique-name regression remains open](../benchmarks/results/2026-11-03-shared-json-property-names/README.md)|
| Native wildcard cursors |Unicode/ASCII file-filter elapsed−48%/−11%, allocation−85%/−44%|[Pre-translation source pair](../benchmarks/results/2026-11-03-native-wildcard-cursors/README.md)|
| CSV record admission |Allocation−4.5–13.6%; variable-width CSV elapsed/CPU about−10%|[Canonical timings vary; wide-record working peak grows](../benchmarks/results/2026-11-03-csv-record-admission/README.md)|

[Existing tools](../benchmarks/COMPARISON.md), [earlier-Lython evidence](../benchmarks/HISTORY.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve other improvements,
profiles, rejected designs and detailed qualification. CPython is the primary
external baseline. Whole-script performance gaps remain substantial.
