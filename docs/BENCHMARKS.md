# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records /57,361 B|7.362 ms|3.700 ms|1.99×|Diagnostic;4.4% Lython IQR|
| JSON-lines/helpers |4,096 records /110,592 B|7.316 ms|4.464 ms|1.64×|Diagnostic;1.6% IQR|
| Text summary/helpers |3,072 lines /35,840 B|5.069 ms|1.467 ms|3.46×|Diagnostic;0.8% IQR|
| Search with early exit |42,010 B file|1.912 ms|0.380 ms|5.04×|Diagnostic;0.9% IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|7.581 ms|2.717 ms|2.79×|Diagnostic;1.8% IQR|
| Unicode filename filter |768 names /124,050 B|2.753 ms|0.583 ms|4.72×|Diagnostic;1.7% IQR|
| ASCII filename filter |768 names /74,898 B|2.308 ms|0.431 ms|5.36×|Diagnostic;4.6% IQR|
| Config read/edit/write |7,177 B|3.668 ms|1.960 ms|1.87×|Historical082179c4;6.1% IQR|
| Copy/checksum/manifest |Two files /4,107 B|2.164 ms|0.569 ms|3.80×|Historical082179c4;10.2% IQR|

Measured **2026-11-03**, **f7dd0d3a**, against **CPython3.13.16**, using
**.NET11RC3**. Config/copy retain their historical revision and date2026-11-02.
These are **fresh compilation plus one complete synchronous execution in an
already-running host**. Identical source/inputs and full stdout/files are checked.
Ratio is Lython elapsed divided by CPython elapsed; above one means slower.
IQR is interquartile range divided by median. The filename scripts read names,
filter them and write/check256 selected names; they do not traverse768 files.

These short diagnostic observations are not portable release multipliers.
Identical fresh controls differ0.2–2.1%. The
[earlier90785737 search](../benchmarks/results/2026-11-02-current-cpython-checkpoint/README.md)
remains **inconclusive**: fresh270% IQR/218% control drift, warm138%/77%.
Steadier later search does not explain that waiting. The
[previousfd7bc7e4 checkpoint](../benchmarks/results/2026-11-02-linear-engine-cpython-checkpoint/README.md)
retains adverse text/report drift. No adverse observations are discarded.
[Current observations and uncertainty](../benchmarks/results/2026-11-03-wildcard-cpython-checkpoint/README.md).

## Warm execution, compilation and startup

Reused compiled code, with the same invocation/file/check boundary:

| Warm workload | Lython | CPython | Ratio | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV helpers |6.932 ms|3.481 ms|1.99×|1.2% /0.3%|
| JSON-lines helpers |7.118 ms|4.194 ms|1.70×|0.8% /0.3%|
| Text helpers |4.975 ms|1.298 ms|3.83×|2.2% /0.4%|
| Search/early exit |1.890 ms|0.267 ms|7.07×|1.2% /1.2%|
| Report rendering |7.761 ms|2.579 ms|3.01×|3.1% /0.4%|
| Unicode filename filter |2.841 ms|0.492 ms|5.78×|3.8% /1.4%|
| ASCII filename filter |2.271 ms|0.334 ms|6.81×|3.3% /0.3%|
| Config edit (historical082179c4) |3.792 ms|2.033 ms|1.87×|3.4% /5.2%|
| Copy/manifest (historical082179c4) |2.202 ms|0.441 ms|4.99×|7.5% /1.2%|

Warm text's identical control differs4.5%; others0.4–2.0%. Independent phase or
revision medians are not additive and do not establish a source speedup.
**Compilation only:** CSV87.61µs versus54.93µs (**1.59×**), report59.93µs versus
37.74µs (**1.59×**). Identical controls differ1.6%/2.2%; no guest execution included.
**Startup is unmeasured here.**
[Historical startup observations](../benchmarks/results/2026-10-29-foundation/README.md)
describe their original revisions.

## Machine and included work

Dedicated Azure **Standard_F4as_v7**, Ubuntu24.04.4 x86-64, four AMD EPYC9V45
vCPUs, one thread/core,16GiB RAM/no swap. CPython is GCC13.3 PGO/LTO, ordinary
GIL/GC, no JIT/debug, launched `-I -S -X utf8`. SDK/runtime:
**11.0.100-rc.3.26522.148 /11.0.0-rc.3.26522.148**. Engines run sequentially
with ordinary worker GC/tiering, quiet gates and independent unchanged controls.

Both clocks include fixture reset/staging, efficient mediated file I/O, complete
stdout/file reads/checks and prior-result disposal within each batch. Fresh Lython
also disposes each per-invocation compilation inside the timer. The final result
stays funded through response delivery, then disposes outside that batch timer.
Compile-only retains its last compilation; cached programs dispose at worker exit.
Startup, transport/response hashing and final cached-owner/process/fixture teardown
are excluded. Every worker is joined after cleanup. Fresh script globals are used;
CPython process-level module/library caches persist, while Lython execution caches
are isolated. Actual suspended-I/O semantics are tested separately; these use `Run`.

The comparison host supplies efficient ranged reads/appends. Containment, Python
conversion and host-boundary work are included; these results do not isolate the
cause of any gap. Exact worker procedures and limitations are linked below.

## Improvements against earlier Lython

These source-pair improvements are separate from the CPython ratios above:

| Change | Established result | Evidence and limitations |
| --- | --- | --- |
| Native helper returns |CSV/JSON allocation−19.9%/−19.3%|[Elapsed diagnostic](../benchmarks/results/2026-11-02-native-function-returns/README.md)|
| Bulk CSV construction |Allocation−13.7–15.1%, CPU−7.2–11.1%|[Controlled comparison](../benchmarks/results/2026-11-02-native-csv-records/README.md)|
| Direct UTF-8 formatting |Allocation−8.67%; confirmation elapsed/CPU−5–8%|[Original adverse timing preserved](../benchmarks/results/2026-11-02-formatting-confirmation/README.md)|
| Native local forwarding |Named CSV/text allocation−30.5%/−34.7%; warm elapsed−5–8%/−8–10%|[Complete scripts and controls](../benchmarks/results/2026-11-02-native-local-forwarding/README.md)|
| Shared JSON names |Report allocation−13.8%, warm elapsed−9–13%|[Unique-name regression remains open](../benchmarks/results/2026-11-03-shared-json-property-names/README.md)|
| Native wildcard cursors |Unicode/ASCII file-filter elapsed−48%/−11%, allocation−85%/−44%|[Pre-translation source pair](../benchmarks/results/2026-11-03-native-wildcard-cursors/README.md)|

[Existing tools](../benchmarks/COMPARISON.md), [earlier-Lython evidence](../benchmarks/HISTORY.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve other improvements,
profiles, rejected designs and detailed qualification. CPython is the primary
external baseline. Whole-script performance gaps remain substantial.
