# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/helpers |4,096 records /57,361 B|5.516 ms|3.780 ms|1.46×|a00fe76d;RAM;Diagnostic;1.37% control /1.93% IQR|
| JSON-lines/helpers |4,096 records /110,592 B|5.290 ms|4.219 ms|1.25×|a00fe76d;RAM;Diagnostic;1.24% control /0.64% IQR|
| Text summary/helpers |3,072 lines /35,840 B|3.319 ms|1.353 ms|2.45×|a00fe76d;RAM;Diagnostic;1.22% control /0.89% IQR|
| JSON-to-Markdown report |3,072 records /189,441 B|4.647 ms|2.735 ms|1.70×|a00fe76d;RAM;Variable;3.07% Lython IQR|
| Config read/edit/write |1,024 paths /7,177 B|0.281 ms|0.258 ms|1.09×|a00fe76d;RAM;Diagnostic;0.62% control /1.85% IQR|
| Search with early exit |42,010 B file|0.508 ms|0.243 ms|2.09×|Earlier bd3;ordinary FS;Diagnostic;2.7% Lython IQR|
| Unicode filename filter |768 names /124,050 B|0.897 ms|0.419 ms|2.14×|Earlier bd3;ordinary FS;Variable;3.5% Lython IQR|
| ASCII filename filter |768 names /74,898 B|0.789 ms|0.292 ms|2.70×|Earlier bd3;ordinary FS;Diagnostic;1.5% Lython IQR|
| Copy/checksum/manifest |Two files /4,107 B|0.526 ms|0.312 ms|1.69×|Earlier bd3;ordinary FS;1.1% Lython IQR|

First five rows measured **2026-11-04**, committed runtime source **a00fe76d**,
against **CPython3.13.16** on **.NET11RC3**. These are **fresh compilation plus one
complete synchronous execution in an already-running host**, including mediated
RAM-file I/O, full checks and prior-result cleanup, excluding fixture staging/reset.
Ratio=Lython elapsed/CPython elapsed; above one means slower. IQR=interquartile
range/median; control is identical-source Lython-role spread. Exact measured
precommit core**1dcca6f2** and source snapshots distinguish source from build metadata.
Four explicitly earlier file rows use2026-11-03 bd3f9dee and the ordinary filesystem;
filename scripts filter768 names/write256 selections, not traverse768 files.

The text/report gaps remain. Current report has3.07%IQR; warm report/control have
3.65%/6.14%IQR. Startup, genuinely suspended I/O, equal heap/RSS and embedding
latency are unmeasured. Runtime correctness qualification is independent;
full platform/Stress/package checks on a00fe76d are pending. [Current observations,
controls and exact identities](../benchmarks/results/2026-11-04-utf8-runtime-cpython-checkpoint/README.md).

[Previous0ffc checkpoint](../benchmarks/results/2026-11-04-executable-cpython-checkpoint/README.md)
and [earlier experimental history](../benchmarks/EXPERIMENTS.md) preserve original
values, conditions and adverse results. Earlier90785737 search remains inconclusive
(fresh270%IQR/218%control drift; warm138%/77%). The [clock correction](../benchmarks/results/2026-11-03-comparison-boundary-diagnostic/README.md)
excludes staging symmetrically; it is not a runtime optimization.

## Warm execution, compilation and startup

Latest external source **a00fe76d**, RAM files, reused compiled code with the same
invocation/I/O/check/cleanup boundary:

| Warm workload | Lython | CPython | Ratio | Lython / CPython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV summary/helpers |5.243 ms|3.523 ms|1.49×|0.94% /1.45%|
| JSON-lines/helpers |5.263 ms|4.103 ms|1.28×|0.41% /0.98%|
| Text summary/helpers |3.164 ms|1.257 ms|2.52×|1.35% /1.70%|
| JSON-to-Markdown report |4.465 ms|2.631 ms|1.70×|3.65% /1.76%; variable|
| Config read/edit/write |0.195 ms|0.185 ms|1.05×|1.29% /0.34%|

Warm identical-source spreads0.48–1.53% limit small claims. Do not infer compilation
cost by subtracting independent fresh/warm medians. **Compilation only:**
CSV80.08µs versus54.23µs (**1.48×**), report56.11µs versus37.95µs (**1.48×**).
Controls differ0.35%/1.27%; LythonIQR1.81%/2.55%, execution/I/O excluded.
**Startup unmeasured.** [Earlier RAM/filesystem phases and measured revisions](../benchmarks/EXPERIMENTS.md)
retain their own conditions; older measurements do not describe this source.

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

Source-pair improvements below retain their own measured revisions. The current coupled
checkpoint comparesa00fe76d with CPython, an identical-source control and qualified97bc43b2 reference. The earlier
2d46dc8e checkpoint also compares pre-loop B166; individual
historical regressions remain preserved even where later coupled work offsets them.
These source-pair improvements are separate from the CPython ratios above;
original elapsed observations retain their own recorded timing boundaries:

| Change | Established result | Evidence and limitations |
| --- | --- | --- |
| Shared UTF8 search windows |Complete long-line prefix/count−22–23%; allocation unchanged|[59 net lines removed; possibleCSVwarm+0.6–3.6% debt retained, full delivery pending](../benchmarks/results/2026-11-04-utf8-search-window/README.md)|
| Native UTF8 existence / EOF |Complete1MiB chunk scans−49–53%, line filter−32–34%; allocation unchanged|[Controlled source pair; small text-helper+0.9–1.4% retained, full delivery pending](../benchmarks/results/2026-11-04-utf8-existence/README.md)|
| Module lexical preparation |Compilation allocation−4.25–4.83%; text/report compile elapsed−4–7%|[Shared module facts/one preparation path; CSV control-limited, fresh scripts near unchanged; no broad CPU claim](../benchmarks/results/2026-11-04-module-lexical-facts/README.md)|
| Compiler resolution cache |Compilation elapsed−3.9–6.5% CSV,−8.4% text,−3.1% report|[Current-source pair; direct older454 comparison: CSV near unchanged, text/report faster, compiler allocation+3.3–5.0%; no broad CPU gain](../benchmarks/results/2026-11-04-resolution-cache/README.md)|
| Executable storage |Duplicate transport, native graph and frame arrays removed; warm allocation flat; possible compile cost remains|[B207/B208; corrected prototype report cost, modest/control-limited source pairs, no broad CPU gain](../benchmarks/results/2026-11-04-executable-cpython-checkpoint/README.md)|
| String cache accounting |Whole-script allocation−1.5–4.1%; sync text elapsed+3.5–5.8%, async−2.9–5.9%|[B205 versusae9b82b7; sync regression accepted, original drifting controls and withdrawn windows preserved; no broad CPU gain](../benchmarks/results/2026-11-04-string-storage-assessment/README.md)|
| Native formatted helpers |Report allocation-26.12% both APIs; warm elapsed-7..12%, fresh-8..10%|[B201 versus B197/B200; compile+2% accepted, admitted peaks unchanged, unrelated text uncertainty preserved](../benchmarks/results/2026-11-04-native-formatted-returns/README.md)|
| Fixed UTF-8 construction |Whole-script allocation−3–12%; controlled sync text elapsed−7–8%|[B194 versus9a4af031; async text possible1–2% debt, async JSON uncertain; all original conditions retained](../benchmarks/results/2026-11-04-fixed-text-construction/README.md)|
| Borrowed native helper arguments |Helper elapsed−6.6–10%; async text≈−7%; allocation flat|[B192 versus334c7cc7; sync text uncertain, refined report within control variation; original adverse report retained](../benchmarks/results/2026-11-04-borrowed-native-arguments/README.md)|
| Shared numeric layout prototype |General/Decimal allocation−11.3%, complex−16.9%; affected elapsed mixed/adverse|[B190 prototypes versus9ce21533; final narrow-width correctness correction was not separately timed; no general CPU gain](../benchmarks/results/2026-11-04-shared-numeric-layout/README.md)|
| Admitted numeric UTF-8 output |Report allocation−6.1%; float control elapsed/CPU−6–8%; large-base control elapsed−85–88%|[B188 versus978b51b5; report sync timing inconclusive, async elapsed about−3% with near-flat CPU; original adverse float profile retained](../benchmarks/results/2026-11-04-admitted-numeric-formatting/README.md)|
| Native dictionary ownership |CSV/JSON/report allocation−1.7–2.4% /−1.1%; confirmed report elapsed−4–5% Run/−1.6–2.1% RunAsync|[B186 versus7a3a8afe; compilation near/slightly above controls, config async variable; original adverse points retained](../benchmarks/results/2026-11-03-native-dictionary-ownership/README.md)|
| Combined loop/string/casing/reader storage |Report elapsed−2.7–4.5% warm /−3.7–4.3% fresh versus pre-loop B166|[Direct coupled pair; sync report debt offset; CSV+1–2% and fresh JSON+1.8–1.9% accepted, fresh text uncertain](../benchmarks/results/2026-11-03-combined-storage-cpython-checkpoint/README.md)|
| Reader-owned UTF-8 windows |Complete-script allocation−1–2.75%; block reads−10–15%|[Fewer copies/cache storage; mixed elapsed, unstable async controls and provisional CSV/JSON debt](../benchmarks/results/2026-11-03-reader-owned-windows/README.md)|
| Direct casing storage |Text allocation−5.92%; warm elapsed−3.7% Run/−9.8% RunAsync against old roles|[Conservative capacity; moving controls, Unicode near unchanged, long ASCII inconclusive](../benchmarks/results/2026-11-03-direct-casing-storage/README.md)|
| Terminal UTF-8 storage |Report allocation−5.68%; elapsed−4.1–6.6%, controls+1.2/−1.7%|[Both APIs; confirmed CSV elapsed+1–4% and higher retained capacity accepted as explicit debt](../benchmarks/results/2026-11-03-terminal-utf8-storage/README.md)|
| Native sequence-loop operands |Text allocation−12.58%; sync CPU−4.05%, control+0.36%|[Both APIs; confirmed sync report +1.35% elapsed /+0.33% allocation is an accepted tradeoff](../benchmarks/results/2026-11-03-native-sequence-operands/README.md)|
| Previous-record JSON keys |Unique-name allocation−14.64%; warm elapsed−31.7–35.1%; accounted peak−834,737 B|[Both APIs; unknown/reordered schemas near controls; config timing variable](../benchmarks/results/2026-11-03-json-record-key-snapshots/README.md)|
| Complete call transport |Async elapsed−1.7–5.7%versus shared-driver source; sync near controls|[Four-image comparison; allocation/peaks unchanged; prior sync blocker not reproduced](../benchmarks/results/2026-11-03-ordinary-call-transport/README.md)|
| Compiled constant format layouts |Report allocation−10.47%; warm/fresh elapsed−6.4/−4.9%|[Compilation flat; initial adverse CSV control retained](../benchmarks/results/2026-11-03-prepared-format-layouts/README.md)|
| Native producer UTF-8 handoff |Config warm/fresh−19.6/−16.3%; allocation−37.8%, CSV rewrite allocation−10.2%|[CSV elapsed flat; text/report control drift retained](../benchmarks/results/2026-11-03-native-utf8-handoff/README.md)|
| Native helper returns |CSV/JSON allocation−19.9%/−19.3%|[Elapsed diagnostic](../benchmarks/results/2026-11-02-native-function-returns/README.md)|
| Bulk CSV construction |Allocation−13.7–15.1%, CPU−7.2–11.1%|[Controlled comparison](../benchmarks/results/2026-11-02-native-csv-records/README.md)|
| Direct UTF-8 formatting |Allocation−8.67%; confirmation elapsed/CPU−5–8%|[Original adverse timing preserved](../benchmarks/results/2026-11-02-formatting-confirmation/README.md)|
| Native local forwarding |Named CSV/text allocation−30.5%/−34.7%; warm elapsed−5–8%/−8–10%|[Complete scripts and controls](../benchmarks/results/2026-11-02-native-local-forwarding/README.md)|
| Historical shared JSON name index |Report allocation−13.8%, warm elapsed−9–13%|[Original unique-name regression retained; index replaced by the snapshot change above](../benchmarks/results/2026-11-03-shared-json-property-names/README.md)|
| Native wildcard cursors |Unicode/ASCII file-filter elapsed−48%/−11%, allocation−85%/−44%|[Pre-translation source pair](../benchmarks/results/2026-11-03-native-wildcard-cursors/README.md)|
| Native producer writes |Config allocation−43.6%/RAM warm−24.1%; complete CSV rewrite allocation/time about−5%|[Controlled scripts; initial unstable fresh control preserved](../benchmarks/results/2026-11-03-native-stream-transport/README.md)|
| JSON iterator working storage |Config allocation−10.5%/−4.4%; RAM warm/fresh elapsed−13%/−10%|[Ordinary-filesystem elapsed unproven; scratch retention](../benchmarks/results/2026-11-03-json-iterator-working-storage/README.md)|
| Bulk ASCII casing |Canonical text warm elapsed−3.0%, control−0.1%; allocation unchanged|[Long-line gain; both scanner prototypes withdrawn](../benchmarks/results/2026-11-03-bulk-text-operations/README.md)|
| CSV record admission |Allocation−4.5–13.6%; variable-width CSV elapsed/CPU about−10%|[Canonical timings vary; wide-record working peak grows](../benchmarks/results/2026-11-03-csv-record-admission/README.md)|

[Existing tools](../benchmarks/COMPARISON.md), [earlier-Lython evidence](../benchmarks/HISTORY.md)
and [experimental history](../benchmarks/EXPERIMENTS.md) preserve other improvements,
profiles, rejected designs and detailed qualification. CPython is the primary
external baseline. Whole-script performance gaps remain substantial.
