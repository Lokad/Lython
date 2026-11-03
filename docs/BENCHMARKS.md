# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records /57,361 B|5.545 ms|3.551 ms|1.56×|Earlier bd3;ordinary FS;Diagnostic;1.1% Lython IQR|
| JSON-lines/helpers |4,096 records /110,592 B|5.829 ms|4.271 ms|1.36×|Earlier bd3;ordinary FS;Diagnostic;1.9% Lython IQR|
| Text summary/helpers |3,072 lines /35,840 B|3.796 ms|1.297 ms|2.93×|Earlier bd3;ordinary FS;Variable;5.5% control difference|
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Earlier bd3;ordinary FS;Diagnostic;2.7% Lython IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|5.758 ms|2.506 ms|2.30×|RAM;873ae625;3.2% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Earlier bd3;ordinary FS;Variable;3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Earlier bd3;ordinary FS;Diagnostic;1.5% Lython IQR|
| Config read/edit/write |7,177 B|0.290 ms|0.251 ms|1.16×|RAM;b3f1e9c3;0.9% Lython IQR|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Earlier bd3;ordinary FS;1.1% Lython IQR|

Measured **2026-11-03**, against **CPython3.13.16** on **.NET11RC3**. These are
**fresh compilation plus one complete synchronous execution in an already-running
host**, including I/O and checks, excluding fixture reset (protocol2). Ratio is
Lython elapsed / CPython elapsed; above one means slower. IQR is interquartile
range / median. **Rows have different storage conditions and revisions.** Latest
report/config points use shared tmpfs; other rows are earlier **bd3f9dee** on the
ordinary VM filesystem. They do not describe today's HEAD. Filename scripts read
names, filter and write/check256 selected names, rather than traverse768 files.

Text/report gaps remain substantial. Current RAM-backed config editing is much
closer to CPython; its earlier ordinary-filesystem fresh/warm ratios were6.29/8.18×.
A [file-boundary trace](../benchmarks/results/2026-11-03-config-file-boundary/README.md)
observes separate managed-host truncation versus CPython's O_TRUNC. Its untraced
share is unquantified, and RAM results do not establish ordinary-filesystem gains.
Source-pair improvements and adverse controls are separate below. The older
[complete ordinary-filesystem checkpoint](../benchmarks/results/2026-11-03-runtime-cpython-checkpoint/README.md)
preserves all nine scripts, original config/report points and timing variability.

Earlier config/copy082179c4/v1 rows are replaced here by current measurements;
their [historical evidence](../benchmarks/results/2026-11-02-current-cpython-checkpoint/README.md)
remains intact. The [clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
excludes staging symmetrically and is not a runtime optimization. The earlier
[90785737 search](../benchmarks/results/2026-11-02-current-cpython-checkpoint/README.md)
remains **inconclusive** (fresh270% IQR/218% control drift; warm138%/77%). Steadier
later results do not explain that waiting. All adverse observations are preserved.

## Warm execution, compilation and startup

Earlier **bd3f9dee**, ordinary filesystem, reused compiled code with the same
invocation/file/check boundary. Latest report/config warm points are in the RAM
table below:

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

## RAM-backed phases and measured revisions

Supplemental measurements use the same actual file APIs and checks, with fixtures
on shared tmpfs instead of the VM filesystem. Measured2026-11-03; exact runtime revisions are shown:

| RAM-backed workload | Runtime | Lython | CPython | Ratio |
| --- | --- | ---: | ---: | ---: |
| Report compile-run |873ae625|5.758 ms|2.506 ms|2.30×|
| Report warm |873ae625|5.541 ms|2.356 ms|2.35×|
| Config compile-run |b3f1e9c3|0.290 ms|0.251 ms|1.16×|
| Config warm |b3f1e9c3|0.207 ms|0.181 ms|1.14×|
| Copy/manifest compile-run |0f05d7c0|0.400 ms|0.238 ms|1.68×|
| Copy/manifest warm |0f05d7c0|0.227 ms|0.150 ms|1.51×|

The earlier bd3 runtime measures2.05×warm on RAM versus8.18×on the ordinary filesystem:
storage condition materially affects config's ratio. The current UTF-8 handoff
improves config warm/fresh19.6%/16.3%, controls+0.1%/+0.6%, against its preceding
runtime. This supports a gain under this storage condition; ordinary-filesystem
elapsed remains unproven. Copy fresh is1.4%adverse with2.1%IQR. These results
complement the primary table and do not replace its included-I/O boundary.
[Current config conditions/controls](../benchmarks/results/2026-11-03-native-utf8-handoff/README.md),
[earlier native writes](../benchmarks/results/2026-11-03-native-stream-transport/README.md),
[earlier copy/storage observations](../benchmarks/results/2026-11-03-ram-file-checkpoint/README.md).
Report format-layout preparation reduces allocation10.47%, warm/fresh source-pair
elapsed6.4/4.9%. Compilation alone is59.14µs versus37.69µs (**1.57×**), effectively
flat against its preceding source. One initial CSV control is adverse and remains
unexplained; a bounded shared-hash diagnostic does not establish a consistent
regression. [All report observations and the CSV uncertainty](../benchmarks/results/2026-11-03-prepared-format-layouts/README.md).


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
| Compiled constant format layouts |Report allocation−10.47%; warm/fresh elapsed−6.4/−4.9%|[Compilation flat; initial adverse CSV control retained](../benchmarks/results/2026-11-03-prepared-format-layouts/README.md)|
| Native producer UTF-8 handoff |Config warm/fresh−19.6/−16.3%; allocation−37.8%, CSV rewrite allocation−10.2%|[CSV elapsed flat; text/report control drift retained](../benchmarks/results/2026-11-03-native-utf8-handoff/README.md)|
| Native helper returns |CSV/JSON allocation−19.9%/−19.3%|[Elapsed diagnostic](../benchmarks/results/2026-11-02-native-function-returns/README.md)|
| Bulk CSV construction |Allocation−13.7–15.1%, CPU−7.2–11.1%|[Controlled comparison](../benchmarks/results/2026-11-02-native-csv-records/README.md)|
| Direct UTF-8 formatting |Allocation−8.67%; confirmation elapsed/CPU−5–8%|[Original adverse timing preserved](../benchmarks/results/2026-11-02-formatting-confirmation/README.md)|
| Native local forwarding |Named CSV/text allocation−30.5%/−34.7%; warm elapsed−5–8%/−8–10%|[Complete scripts and controls](../benchmarks/results/2026-11-02-native-local-forwarding/README.md)|
| Shared JSON names |Report allocation−13.8%, warm elapsed−9–13%|[Unique-name regression remains open](../benchmarks/results/2026-11-03-shared-json-property-names/README.md)|
| Native wildcard cursors |Unicode/ASCII file-filter elapsed−48%/−11%, allocation−85%/−44%|[Pre-translation source pair](../benchmarks/results/2026-11-03-native-wildcard-cursors/README.md)|
| Native producer writes |Config allocation−43.6%/RAM warm−24.1%; complete CSV rewrite allocation/time about−5%|[Controlled scripts; initial unstable fresh control preserved](../benchmarks/results/2026-11-03-native-stream-transport/README.md)|
| JSON iterator working storage |Config allocation−10.5%/−4.4%; RAM warm/fresh elapsed−13%/−10%|[Ordinary-filesystem elapsed unproven; scratch retention](../benchmarks/results/2026-11-03-json-iterator-working-storage/README.md)|
| Bulk ASCII casing |Canonical text warm elapsed−3.0%, control−0.1%; allocation unchanged|[Long-line gain; both scanner prototypes withdrawn](../benchmarks/results/2026-11-03-bulk-text-operations/README.md)|
| CSV record admission |Allocation−4.5–13.6%; variable-width CSV elapsed/CPU about−10%|[Canonical timings vary; wide-record working peak grows](../benchmarks/results/2026-11-03-csv-record-admission/README.md)|

[Existing tools](../benchmarks/COMPARISON.md), [earlier-Lython evidence](../benchmarks/HISTORY.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve other improvements,
profiles, rejected designs and detailed qualification. CPython is the primary
external baseline. Whole-script performance gaps remain substantial.
