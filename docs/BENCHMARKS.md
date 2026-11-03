# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records /57,361 B|5.545 ms|3.551 ms|1.56×|Diagnostic;1.1% Lython IQR|
| JSON-lines/helpers |4,096 records /110,592 B|5.829 ms|4.271 ms|1.36×|Diagnostic;1.9% Lython IQR|
| Text summary/helpers |3,072 lines /35,840 B|3.796 ms|1.297 ms|2.93×|Variable;5.5% control difference|
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Diagnostic;2.7% Lython IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|5.923 ms|2.557 ms|2.32×|Variable;3.1% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Variable;3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Diagnostic;1.5% Lython IQR|
| Config read/edit/write |7,177 B|2.101 ms|0.334 ms|6.29×|Host truncate asymmetry; variable|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Diagnostic;1.1% Lython IQR|

Measured **2026-11-03**, exact runtime/worker revision **bd3f9dee**, against
**CPython3.13.16** on **.NET11RC3**. These are **fresh compilation plus one complete
synchronous execution in an already-running host**, with fixture reset excluded
(protocol2). Ratio is Lython elapsed divided by CPython elapsed; above one means
slower. IQR is interquartile range divided by median. The filename scripts read
names, filter and write/check256 selected names; they do not traverse768 files.
Later JSON-iterator changes are excluded; their source-pair results are listed below.

The gap remains substantial. Config editing is particularly slow even warm;
compilation alone is about58µs and does not explain its millisecond execution.
These diagnostic points include host work and checks, not guest CPU alone.
Identical-source controls differ up to5.7%; text/config results are variable.
Independent phase/revision medians do not establish a source speedup.
[Exact observations, controls and boundaries](../benchmarks/results/2026-11-03-runtime-cpython-checkpoint/README.md).

Earlier config/copy082179c4/v1 rows are replaced here by current measurements;
their [historical evidence](../benchmarks/results/2026-11-02-current-cpython-checkpoint/README.md)
remains intact. The [clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
excludes staging symmetrically and is not a runtime optimization. The earlier
[90785737 search](../benchmarks/results/2026-11-02-current-cpython-checkpoint/README.md)
remains **inconclusive** (fresh270% IQR/218% control drift; warm138%/77%). Steadier
later results do not explain that waiting. All adverse observations are preserved.

## Warm execution, compilation and startup

Reused compiled code, with the same invocation/file/check boundary:

| Warm workload | Lython | CPython | Ratio | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV summary/helpers |5.235 ms|3.305 ms|1.58×|1.1% /0.5%|
| JSON-lines/helpers |5.820 ms|4.126 ms|1.41×|0.8% /0.5%|
| Text summary/helpers |3.717 ms|1.188 ms|3.13×|0.5% /1.0%|
| Search with early exit |0.386 ms|0.149 ms|2.60×|2.9% /0.7%|
| JSON-to-Markdown report |5.809 ms|2.383 ms|2.44×|3.1% /2.5%|
| Unicode filename filter |0.808 ms|0.340 ms|2.38×|3.9% /1.2%|
| ASCII filename filter |0.675 ms|0.211 ms|3.20×|1.2% /0.5%|
| Config read/edit/write |1.974 ms|0.241 ms|8.18×|6.8% /1.8%|
| Copy/checksum/manifest |0.344 ms|0.202 ms|1.70×|0.8% /1.1%|

Warm text/config identical controls differ5.7%/4.7%; JSON4.1%. Config warm Lython
IQR6.8%; its control IQR11.8%. These limit precision without explaining away the gap.
**Compilation only:** CSV86.55µs versus54.94µs (**1.58×**), report60.77µs versus
37.79µs (**1.61×**), config57.84µs versus34.68µs (**1.67×**). Identical controls
differ+2.4/−2.2/+0.6%; no guest execution is included. **Startup is unmeasured.**
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
| JSON iterator working storage |Large/small config allocation−10.5%/−4.4%; copy−1.6%|[Elapsed remains unproven; scratch retention](../benchmarks/results/2026-11-03-json-iterator-working-storage/README.md)|
| Bulk ASCII casing |Canonical text warm elapsed−3.0%, control−0.1%; allocation unchanged|[Long-line gain; both scanner prototypes withdrawn](../benchmarks/results/2026-11-03-bulk-text-operations/README.md)|
| CSV record admission |Allocation−4.5–13.6%; variable-width CSV elapsed/CPU about−10%|[Canonical timings vary; wide-record working peak grows](../benchmarks/results/2026-11-03-csv-record-admission/README.md)|

[Existing tools](../benchmarks/COMPARISON.md), [earlier-Lython evidence](../benchmarks/HISTORY.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve other improvements,
profiles, rejected designs and detailed qualification. CPython is the primary
external baseline. Whole-script performance gaps remain substantial.
