# Lython performance against CPython

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| JSON configuration read/edit/write [AF], fresh compile/run | 16 existing paths / 181 UTF-8 input bytes | 2.817-3.142 ms | 1.572 ms | — | Two identical Lython controls differ by 11.5%; one diagnostic; no qualified ratio |
| Complete CSV summary [BP], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 7.503 ms | 3.849 ms | — | Short diagnostic at27a61681; nominal8.3% below prior Lython,7.5%/6.4% spreads; inconclusive |
| Complete CSV summary with annotated helper [BU], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 8.581 ms | 3.718 ms | — | Dedicated-VM diagnostic at 463b7a7a; nominal 2.308×, unqualified; 4.36%/0.85% spreads |
| Complete CSV helper with annotation-only local [BT], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 19.745 ms | 17.014 ms | — | Local Windows, 3f799746 + one-line guard deletion; 48.9%/13.5% spreads, inconclusive |
| Complete text-summary file job [BR], fresh compile/run | 3,072 lines / 35,840 UTF-8 bytes | 14.994–16.539 ms | 11.188 ms | — | Local Windows diagnostic at 877f20e2; identical controls; 49%/107%/39% spreads, inconclusive |
| Complete JSON-lines aggregation [AU], fresh compile/run | 4,096 records / 110,592 UTF-8 bytes | 9.880–10.024 ms | 4.359 ms | — | Two identical Lython controls; unqualified (21.3%/8.7% spread); startup excluded |
| Complete copy/checksum/manifest job [AD], warm | Two files / 4,107 input bytes | 6.080 ms | 0.4382 ms | — | Historical warm diagnostic; elapsed change within spread |
| File search with helper and early exit [P], fresh compile/run | 2,049 lines / 42,010 UTF-8 bytes; stop after line 1,025 | 6.041 ms | 0.395 ms | — | Fresh code per job; inconclusive (42.4% spread); startup excluded |
| Complete uppercase-report file job [AB], warm | 1,024 lines / 23,552 UTF-8 bytes | 2.651 ms | 0.2444 ms | — | Historical warm diagnostic; elapsed change within spread |
| In-memory JSON parse/edit/dump [BQ], warm `Run` | 1,024 records / 55,723 UTF-8 input bytes | 3.503 ms | 1.016 ms | — | Local Windows diagnostic at c9990b38; 4.6% higher median than earlier Lython; no elapsed benefit established |

The rejected class prototype [AZ](benchmarks/results/2026-10-16-compiled-class-methods/README.md) remains in the experimental archive.

These representative snapshots come from the named revisions below; they are
not measurements of today's HEAD. Inconclusive cases remain visible. See
[all diagnostic snapshots](benchmarks/EXPERIMENTS.md#diagnostic-comparisons-by-revision)
for the detailed history and [Lython improvements](#improvements-against-earlier-lython-revisions)
for changes against prior Lython versions.

**Ratio convention:** Lython elapsed time divided by CPython elapsed time;
above 1 means Lython takes longer. A dash means unavailable or unqualified,
never zero. No workload ratio is qualified here. AF, AU, BO and BR ranges contain the medians
of two identical Lython controls, not confidence intervals. Other entries show
one diagnostic median. The jobs show performance gaps in those runs; their
magnitudes and changes remain uncertain as marked.

## Which code and machine were measured?

The VM snapshots (all leading rows except BQ/BR/BT, and historical Q/L campaigns) used the same dedicated **Azure Standard_F4as_v7**: Ubuntu 24.04.4 LTS
x86_64, four AMD EPYC 9V45 vCPUs, one thread per core, 16 GiB RAM, no swap.
Earlier VM snapshots used clean Release builds with **.NET SDK 10.0.401 / runtime 10.0.12**.
The primary external reference was **CPython 3.13.16**, built with GCC 13.3,
PGO/LTO, ordinary GIL and cyclic GC, without debug mode or JIT, launched with
`-I -S`. Workers kept ordinary GC/tiering and public Lython limits; optional
instruction fuel was unset. Both engines ran sequentially on that machine.
The linked receipts identify binaries, inputs, toolchains and sampling policies.

BU executes frozen Release package binaries built with SDK
10.0.300-preview.0.26177.108 on the VM's .NET 10.0.12 runtime. Its identical
adapter was built at a35ee62c; only the packaged Lython DLL differs between
roles. It observed low VM load under the shared lease, without milestone noise
qualification. The older local Windows measurement of this job, BS, remains
below; its timing cannot establish a change from BU.

BQ, BR, BS and BT use the active Windows 11 development workstation: Intel Core i7-14700KF,
28 logical processors, .NET SDK 10.0.300-preview.0.26177.108 / runtime 10.0.13
and CPython 3.13.2. They are unqualified local diagnostics and cannot be directly
compared with quiet-VM timings. BQ establishes an allocation reduction only.
BR fills the fresh text boundary; its very large spread leaves the ratio
unqualified and does not separate compilation, execution and file staging.
BS adds a complete CSV job with an annotated parsing helper; its large spread
also leaves the ratio unqualified. None of these local snapshots can be used
to infer a change from the quiet-VM measurements.

| Label | Date | Measured Lython revision | Evidence |
| --- | --- | --- | --- |
| BU | 2026-10-17 UTC | 463b7a7a; complete CSV helper job; dedicated VM fresh compile/run | [CPython comparison, frozen package/build identities, earlier Lython and cleanup](benchmarks/results/2026-10-17-csv-checkpoint/README.md) |
| BS | 2026-10-17 UTC | efccd974; complete CSV helper job; local Windows fresh compile/run | [Exact package roles, full outputs, earlier Lython and uncertainty](benchmarks/results/2026-10-17-csv-helper-fresh/README.md) |
| BR | 2026-10-17 UTC | 877f20e2; identical Lython controls; local Windows fresh compile/run of complete text job | [Compile-to-result boundary, exact goldens, identities and uncertainty](benchmarks/results/2026-10-17-text-fresh/README.md) |
| BQ | 2026-10-17 UTC | c9990b38; bounded short escaped JSON decoding; local Windows warm execution | [Earlier Lython, CPython, native allocation and uncertainty](benchmarks/results/2026-10-17-escaped-json-strings/README.md) |
| BP | 2026-10-17 UTC | 27a61681; bounded UTF-8 decimal parsing and no-op cache-hook cleanup; fresh compile/run of complete CSV job | [Unchanged workload/boundary, earlier Lython, exact goldens and separate allocation evidence](benchmarks/results/2026-10-17-utf8-small-integer/README.md) |
| BO | 2026-10-17 UTC | 0f1cff1f; identical Lython controls; fresh compile/run of complete CSV job | [Compile-to-result boundary, independent file goldens and separate setup allocations](benchmarks/results/2026-10-17-csv-fresh/README.md) |
| BN | 2026-10-17 UTC | b10b8e9e; shared bounded/unlimited UTF-8 replacement; both roles include copy-admission fix a03ef99e | [Separate allocation evidence and unlimited pipeline diagnostic](benchmarks/results/2026-10-17-string-replacement/README.md) |
| BM | 2026-10-16 UTC | 2e4d0a34; ordinary cursor pulls reuse synchronous core; user/async iterator tasks forwarded directly | [Cursor dispatch, exact fuel/protocol controls and short diagnostic](benchmarks/results/2026-10-16-cursor-sync-dispatch/README.md) |
| BL | 2026-10-16 UTC | 35d99a88; direct container forwarding and redundant prefix decoder removed; excludes later integer-admission correction | [JSON deletions, exact guards and short diagnostic](benchmarks/results/2026-10-16-json-container-forwarding/README.md) |
| BK | 2026-10-16 UTC | 4b930ca5; synchronous true/false/null dispatch; both roles include error correction 2a2ebada | [JSON transformation, exact guards and error correction](benchmarks/results/2026-10-16-json-literal-dispatch/README.md) |
| BJ | 2026-10-16 UTC | 527ff833; invocation-owned admitted local-capacity buffers | [Lifetime consolidation, earlier denial and short diagnostic](benchmarks/results/2026-10-16-owned-function-locals/README.md) |
| BI | 2026-10-16 | 60961196; ordinary Python functions bind existing operands and compiled names | [Keyword calls, live defaults and unchanged guard/owner contracts](benchmarks/results/2026-10-16-ordinary-function-transport/README.md) |
| BH | 2026-10-16 | d4ac39db; builtin len consumes existing operand storage | [Strip/length loop, unchanged guards and retained inputs](benchmarks/results/2026-10-16-direct-length-call/README.md) |
| BG | 2026-10-16 | f8d87288; ordinary control flow uses the existing synchronous handler under RunAsync | [Branch loop, unchanged guards and actual suspension](benchmarks/results/2026-10-16-async-control-flow/README.md) |
| BF | 2026-10-16 | c487cb70; bulk hashing of existing string/bytes storage | [Two loops removed, key/owner controls and noisy timing](benchmarks/results/2026-10-16-bulk-byte-hash/README.md) |
| BE | 2026-10-16 | ffdac05d; shared guarded entry for immediate strip/split/lower | [Source deletion, unchanged guards and inconclusive timing](benchmarks/results/2026-10-16-noarg-string-consolidation/README.md) |
| BD | 2026-10-16 | 18942340; direct immediate exact-string lower calls | [Complete text job, shared casing/ownership and inconclusive timing](benchmarks/results/2026-10-16-direct-string-lower/README.md) |
| BC | 2026-10-16 | d88d6440; direct positional transport, ordinary lowered method bodies | [Method job, unchanged guards and short diagnostic](benchmarks/results/2026-10-16-lowered-method-transport/README.md) |
| BB | 2026-10-16 | 5d5fd2e7; numeric and shared JSON callback forwarding | [Complete job, unchanged lifetimes and no established performance benefit](benchmarks/results/2026-10-16-json-number-forwarding/README.md) |
| BA | 2026-10-16 | 5ed2b203; direct synchronous JSON string-token dispatch | [Complete JSON job, guard matrices and inconclusive timing](benchmarks/results/2026-10-16-json-string-dispatch/README.md) |
| AZ | 2026-10-16 | c3db3ca2; rejected prepared-method prototype, fresh compile/run | [Slower diagnostic, actual input and setup erratum](benchmarks/results/2026-10-16-compiled-class-methods/README.md) |
| AY | 2026-10-16 | 8b0f32fb; direct zero-argument exact-string split | [Complete text job, lifecycle and inconclusive timing](benchmarks/results/2026-10-16-direct-string-split/README.md) |
| AX | 2026-10-16 | 44199dd3; direct synchronous executable-generator advancement | [Shared frame guards, lifecycle and short diagnostic](benchmarks/results/2026-10-16-generator-sync-advance/README.md) |
| AW | 2026-10-16 | fa2b72af; genuine executable generator-expression bodies | [Lifecycle, ownership and inconclusive timing](benchmarks/results/2026-10-16-executable-generators/README.md) |
| AV | 2026-10-16 | 569a653a; immediate generator bindings and captured-cell resumption | [Generator reduction, correctness and single diagnostic](benchmarks/results/2026-10-16-generator-bindings/README.md) |
| AF | 2026-10-15 | 654b4f5a; two identical Lython controls, freshly compiled configuration job | [Compile-to-result boundary, controls and independent file goldens](benchmarks/results/2026-10-15-config-fresh/README.md) |
| C | 2026-10-14 | f95fb734, governed runtime and worker ownership; parser candidate rejected | [Complete CSV diagnostic](benchmarks/results/2026-10-14-representative-csv/README.md), [loaded identities](benchmarks/results/2026-10-14-representative-csv/micro.json) |
| AN | 2026-10-16 | 7caf4833; three frozen compiler/runtime files on a5a96af2; both roles include the type-slot mutation prerequisite | [Executable subscript headers, correctness and inconclusive complete-job timing](benchmarks/results/2026-10-16-executable-subscript-stores/README.md) |
| AU | 2026-10-16 | 1ad18b79; identical Lython binary in both controls; fresh compile/run | [Fresh complete JSON job, boundary and equivalent controls](benchmarks/results/2026-10-16-json-fresh/README.md) |
| AT | 2026-10-16 | dc950cde on 09f44e94; two frozen numeric/preflight sources; unchanged JSON aggregation | [UTF-8 numeric parsing, shared admission and inconclusive timing](benchmarks/results/2026-10-16-json-utf8-numbers/README.md) |
| AD | 2026-10-15 | Working exact-bytes sum candidate on 64fecd53; one frozen source; unchanged script/tooling | [Complete manifest job, exact sums and ownership/preflight checks](benchmarks/results/2026-10-15-bytes-sum/README.md) |
| P | 2026-10-15 | Working protected-loop candidate on b2235259; two frontend source changes frozen by hashes | [Separate warm and compile/run diagnostics](benchmarks/results/2026-10-15-protected-loop-jumps/README.md), [source declaration](benchmarks/results/2026-10-15-protected-loop-jumps/comparison-declaration.json) |
| AB | 2026-10-15 | Working UTF-8 upper candidate on 29a1862e; one frozen source; identical new report script/catalog in both roles | [Complete report, noise and expansion/ownership checks](benchmarks/results/2026-10-15-utf8-upper/README.md) |
| AE | 2026-10-15 | Working exact-integer dispatch candidate on 60277f65; one frozen interpreter source; unchanged case/tooling | [Async integer operators, suspension and boundary checks](benchmarks/results/2026-10-15-async-integer-operators/README.md) |
| AR | 2026-10-16 | 473aa396 on cdf95961; six frozen compiler/runtime files; unchanged file search | [Direct prefix calls, compatibility fixes and inconclusive timing](benchmarks/results/2026-10-16-string-prefix-calls/README.md) |
| AQ | 2026-10-16 | 4846928c on a2103402; three frozen compiler/runtime files; unchanged configuration job | [Compiled subscript updates and inconclusive complete-job timing](benchmarks/results/2026-10-16-augmented-subscript-stores/README.md) |
| AP | 2026-10-16 | 58aece73 on 9d0902a3; three frozen compiler/runtime files; unchanged method job | [Compiled member updates and no established timing benefit](benchmarks/results/2026-10-16-augmented-member-stores/README.md) |

K, R, V, D, I, E, H and P used the same VM and SDK/runtime/CPython versions described below.
I measures warm completed-host `RunAsync` against ordinary warm CPython code.
E separates warm `Run` and completed-host `RunAsync`; CPython executes ordinary
warm code in both rows. H preserves both rejected candidates and their existing
runtime controls. P separates fresh compile/run from warm reuse and includes
complete file staging/output checks. Their candidate hashes identify measured
source, rather than an arbitrary later HEAD.

[Additional machine/control identities](benchmarks/EXPERIMENTS.md#additional-machine-and-control-identities)
and [all historical revision identities](benchmarks/EXPERIMENTS.md#archived-measurement-identities)
remain available behind the detailed diagnostic history.

## Execution, compilation and startup are separate

Rows marked **fresh compile/run** compile and invoke once per job in a warm
worker, including complete file staging and output checks where applicable.
Rows marked **warm** reuse compiled code: both engines create fresh invocation
state, execute the complete module, capture/project
output and perform the cheap golden check inside the worker timer. Compilation,
worker launch, transport and full output hashing are outside it. For complete
file-backed jobs, file reset/staging and exact output-file byte checks are
inside both worker timers, as defined in [COMPARISON.md](benchmarks/COMPARISON.md).
Module imports
retain their source positions and normal caches; invocation controls are never
subtracted. Historical Q/L cases have in-memory fixtures, not disk file I/O.

Lython's host boundary, memory governor and cooperative checks remain enabled.
CPython has no equivalent in-process containment. These are application costs
under the two ordinary execution contracts; neither a bare arithmetic test nor
a claim that the contracts provide identical protection.

| Boundary | Observation | Status |
| --- | --- | --- |
| Warm invocation | See workload table above | No qualified workload ratios |
| Compile and run | Compile source, then execute fresh state in persistent workers | AF/AU/BP/BR/BT/BU/P cover targeted configuration/JSON/CSV/text/search jobs above; broader historical Q collection remains incomplete ([retained results](benchmarks/results/2026-10-09-quick/compile-run.md)) |
| Compilation only | Lython engine compilation versus CPython `compile()` | Mostly incomplete after idle gates; [retained results](benchmarks/results/2026-10-09-quick/compile.md) |
| Fresh-process empty control | Lython 170.361–175.524 ms; CPython 29.628–29.947 ms | Startup control; [retained results](benchmarks/results/2026-10-09-quick/fresh-process.md) |

Fresh-process timing includes parent-observed launch, compilation, execution,
pipe drain and exit, with OS file caches retained. It must not be compared with
warm execution. Its controls qualified, but workloads failed the required
separation from startup; no workload ratio was published. Compilation samples
and startup costs must not be folded into the warm table.

## Other measured boundaries

Historical warm forms and core diagnostics remain visible below. They retain
their original revision, boundary and uncertainty; none describes today's HEAD.

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Complete CSV summary with annotated helper [BS], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 17.358 ms | 14.348 ms | — | Historical local Windows diagnostic at efccd974; 45.8%/29.8% spreads, inconclusive |
| Same JSON configuration job [AQ], warm `Run` | 16 existing paths / 181 UTF-8 input bytes | 3.309 ms | 1.695 ms | — | Short diagnostic at 4846928c; 0.9% change within spread; no improvement established |
| Complete CSV summary [C], warm | 4,096 records / 57,361 UTF-8 bytes | 7.530 ms | 3.483 ms | — | One warm diagnostic at f95fb734; inconclusive baseline spread (15.4%) |
| Complete text-summary file job [BF], warm | 3,072 lines / 35,840 UTF-8 bytes | 6.118 ms | 1.2864 ms | — | Short diagnostic at c487cb70; bulk hashing; 30.5% lower median alongside 30.0% baseline spread, inconclusive |
| Same JSON transform [BL], warm `RunAsync` | 1,024 records / 55,723 UTF-8 input bytes | 2.691 ms | 0.4439 ms | — | Quiet-VM diagnostic at 35d99a88; container/prefix forwarding removed; 2.5% higher median within 8–13% spread, inconclusive |
| Complete JSON-lines aggregation [BB], warm | 4,096 records / 110,592 UTF-8 bytes | 9.475 ms | 4.1751 ms | — | Short diagnostic at 5d5fd2e7; numeric forwarding; 6.1% higher median within 14.2% baseline spread; no benefit established |
| File search with helper and protected early exit [AR], warm | 2,049 lines / 42,010 UTF-8 bytes | 3.223 ms | 0.2845 ms | — | Short diagnostic at 473aa396; inconclusive (57%/64% Lython spread); no improvement established |
| Starred-list helper loop [BJ], warm `RunAsync` | 16,384 calls / 264 UTF-8 source bytes | 27.550 ms | 2.9739 ms | — | Short diagnostic at 527ff833; admitted local buffers; 3.6% higher median, unqualified; no benefit established |
| Keyword helper-call loop [BI], warm `RunAsync` | 16,384 calls / 134 UTF-8 source bytes | 6.680 ms | 1.0849 ms | — | Short diagnostic at 60961196; direct function transport; 1.8% lower median within 13.3% baseline spread, inconclusive |
| Constructed-text strip/length loop [BH], warm `RunAsync` | 32,768 lines / 425,984 UTF-8 bytes constructed in execution | 17.832 ms | 3.1868 ms | — | Short diagnostic at d4ac39db; direct len transport; 26.4% lower median with 18.8% baseline spread, unqualified |
| Branch/integer loop [BM], warm `RunAsync` | 16,384 iterations / 119 UTF-8 source bytes; modulo/comparison/add/subtract | 5.889 ms | 1.0203 ms | — | Short diagnostic at 2e4d0a34; synchronous cursor dispatch; 1.5% lower median within 4.7% baseline spread, inconclusive |
| Unicode replace/split/join pipeline [BN], warm `RunAsync` | 8,192 blocks / 139,264 UTF-8 input bytes | 2.981 ms | 0.5404 ms | — | Short diagnostic at b10b8e9e; shared replacement kernel; 17.7% lower median within 80.8% baseline spread, inconclusive |
| Instance-method accumulator [BC], warm `Run` | 2,048 calls / 207 UTF-8 source bytes | 1.060 ms | 0.11428 ms | — | Short diagnostic at d88d6440; direct lowered-method transport; 7.4% lower median, unqualified |
| Generator-expression numeric reduction [AX], warm `Run` | 16,384 float terms / 73 UTF-8 source bytes | 3.912 ms | 0.5586 ms | — | Short diagnostic at 44199dd3; direct synchronous advancement; 5.0% lower Lython median, unqualified |

## Improvements against earlier Lython revisions

The [list admission / CSV-copy diagnostic](benchmarks/results/2026-10-17-list-capacity-admission/README.md)
has separate native allocation evidence: 128 conversions of pre-read rows with
nine excess fields allocate 74,752 → 61,440 bytes; the normal-width control stays
at 30,720 bytes. It measures two frozen runtime files on 97273b98, excludes
parsing/public execution, and establishes no whole-script or CPython speedup.
The admission fix also funds replacement overlap before backing allocation.

The complete CSV helper checkpoint
[BU](benchmarks/results/2026-10-17-csv-checkpoint/README.md) separately measures
a35ee62c → 463b7a7a: 8.432 → 8.581 ms, with 5.86%/4.36% spreads. The 1.77%
median difference establishes no improvement or regression. It does not isolate
the intervening byte-truth change, which the CSV script does not exercise.

The annotation-only CSV helper [BT](benchmarks/results/2026-10-17-annotation-only-locals/README.md)
compares 3f799746 with its one-line fallback-guard deletion: 21.677 → 19.745 ms,
38.0%/48.9% spread. This single fresh-job diagnostic includes compilation/file
staging and establishes no elapsed benefit. It measures the archived source patch;
no current-HEAD, allocation or qualified CPython multiplier is implied.

The [native dictionary-read guard diagnostic](benchmarks/results/2026-10-17-native-dictionary-dispatch/README.md)
records unchanged allocation: 320 bytes per 128 reads through both instruction
runners, with unchanged list controls. Timing is unqualified and predates the
related stored-key async correctness fix. Complete-job and CPython performance
remain unmeasured for this change.

The complete CSV helper job [BS](benchmarks/results/2026-10-17-csv-helper-fresh/README.md)
measures a5a2fc0f → efccd974 separately: 14.818 → 17.358 ms, with 48.7%/45.8%
spreads. No elapsed improvement or regression is established. This fresh-job
comparison includes compilation and file staging; it does not isolate the helper
implementation or qualify a CPython multiplier.

Fixed builtin setup at 9e9135bb → 0f1cff1f has separate
[native allocation evidence](benchmarks/results/2026-10-17-csv-fresh/README.md):
27,568 → 26,088 bytes per warm context construction across 32 fresh owners.
The CSV snapshot uses identical Lython controls; it does not measure a speedup.

General-method receiver binding at 90be9683 has separate
[allocation-only evidence](benchmarks/results/2026-10-16-general-method-binding/README.md):
72 excess bytes per entry with one argument become zero against a prefixed control.
Elapsed time and CPython performance for that change remain unmeasured.

Ordinary native integer-text calls have separate
[allocation-only evidence](benchmarks/results/2026-10-17-integer-text-calls/README.md)
at8505c40d→dada7c9c:128calls allocate20,792→11,576bytes synchronously and
32,056→11,576bytes with asynchronous hosting. Text and bytes agree. Complete
script timing and CPython performance for this change remain unmeasured.

The [bounded integer parser follow-up](benchmarks/results/2026-10-17-utf8-small-integer/README.md)
at57275bf2→27a61681 reduces that same allocation diagnostic11,576→4,408bytes.
Its complete fresh CSV timing is separate and inconclusive; neither allocation
count establishes a current CPython multiplier or embedding latency.

These compare Lython with earlier Lython, **not with CPython**. Each uses short
diagnostic collections, ordinary protection/GC/tiering and unchanged worker
boundaries. They have no milestone qualification or claimed confidence interval.
The percentages must not be added or multiplied into a current CPython ratio.

| Change and date | Baseline → candidate revision | Observed Lython time reduction | Evidence |
| --- | --- | ---: | --- |
| Direct str.upper calls, 2026-10-17 UTC | 45fb2892 → frozen two-file call-transport patch; warm128-call native loop | Allocation only: sync48,640 →32,256 bytes; RunAsync60,904 →32,232 bytes. Exposed controls unchanged; whole-script time unmeasured | [Exact boundaries,sources,samples and temporary recipe](benchmarks/results/2026-10-17-direct-string-upper/README.md) |
| Counted str.replace calls, 2026-10-17 UTC | 61c96200 → frozen four-file direct-call patch; warm 128-call native loop | Allocation only: sync 73,664 → 40,896 bytes; RunAsync 80,808 → 40,872 bytes. Exposed controls unchanged; no elapsed claim | [Source identities, samples, boundaries and temporary recipe](benchmarks/results/2026-10-17-counted-replace/README.md) |
| Bounded str.startswith calls, 2026-10-17 UTC | 093bba6b → frozen five-file borrowed-operand patch; warm 128-call native loop | Allocation only: sync 48,640 → 7,680 bytes; RunAsync 55,784 → 7,656 bytes. Exposed controls unchanged; no elapsed claim | [Source identities, samples, boundaries and temporary recipe](benchmarks/results/2026-10-17-bounded-startswith/README.md) |
| Direct float text calls, 2026-10-17 UTC | f0d3b700 → frozen three-file shared-parser/operand patch; warm128-conversion native-string loop | Allocation only: sync18,912 →14,816 bytes; RunAsync30,152 →14,792 bytes. Expanded-call controls unchanged; no elapsed claim | [Source identities, samples, boundaries and temporary recipe](benchmarks/results/2026-10-17-direct-float-text/README.md) |
| Native truth entry, 2026-10-17 UTC | 9133e9ed → frozen one-file native-entry patch; warm all of4,096 native values, RunAsync | No speedup established:0.4489 →0.4141 ms,7.8% lower median with19.3% candidate spread; allocations680 bytes unchanged | [All samples, controls, source identities and temporary recipe](benchmarks/results/2026-10-17-native-truth-entry/README.md) |
| Native any/all routing, 2026-10-17 UTC | c62b5e19 → frozen one-file patch; warm all of4,096 bytes/range values under RunAsync | Bytes0.89825 →0.27925ms,68.9% lower in one unqualified capture; range timing inconclusive (250.4% baseline spread). Native allocations remain about128KiB | [Samples, controls, boundaries and source identities](benchmarks/results/2026-10-17-native-any-all/README.md) |
| Exact-byte any/all reduction, 2026-10-17 UTC | a35ee62c → frozen one-file predicate patch; warm all of4,096 bytes | Allocations sync131,600 →368 bytes; RunAsync131,688 →456. Local medians0.3256 →0.00965ms /0.32095 →0.0124ms, unqualified with high relative candidate spread | [Samples, unchanged controls, boundaries and source identities](benchmarks/results/2026-10-17-byte-truth-reduction/README.md) |
| Short escaped JSON strings, 2026-10-17 UTC | 92806689 → c9990b38; unchanged in-memory JSON transform | No elapsed benefit: 3.350 → 3.503 ms in one local Windows diagnostic. Separate native allocation 80,896 → 76,800 bytes per 128 calls | [Boundaries, controls and one capture](benchmarks/results/2026-10-17-escaped-json-strings/README.md) |
| Bounded UTF-8 integer parsing, 2026-10-17 UTC | e01d45c6 → 27a61681; unchanged fresh compile/run CSV job | No qualified gain:8.181→7.503ms, nominal8.3% lower with7.5%/6.4% spreads. Separate native allocation11,576→4,408bytes per128calls | [Exact inputs, ownership contract and one short capture](benchmarks/results/2026-10-17-utf8-small-integer/README.md) |
| Shared UTF-8 replacement, 2026-10-17 UTC | a03ef99e → b10b8e9e; unchanged unlimited Unicode pipeline | No elapsed benefit established: 3.621 → 2.981 ms; 17.7% lower median within 80.8% baseline spread. Separate bounded allocation witness: 115,032 → 4,392 bytes | [Admission, retained aliases and exact boundaries](benchmarks/results/2026-10-17-string-replacement/README.md) |
| Synchronous cursor pulls under RunAsync, 2026-10-16 UTC | 86ef68e9 → 2e4d0a34; unchanged branch loop | No benefit established: 5.979 → 5.889 ms; 1.5% lower median within 4.7% baseline spread | [Shared core and genuine suspension preserved](benchmarks/results/2026-10-16-cursor-sync-dispatch/README.md) |
| JSON container/prefix forwarding, 2026-10-16 UTC | 73426010 → 35d99a88; unchanged in-memory JSON transform | No benefit established: 2.626 → 2.691 ms; 2.5% higher median within 8–13% spread | [Removed wrappers and preserved checkpoints](benchmarks/results/2026-10-16-json-container-forwarding/README.md) |
| Synchronous JSON literals, 2026-10-16 UTC | 2a2ebada → 4b930ca5; unchanged in-memory JSON transform | No benefit established: 2.365 → 2.380 ms; 0.6% higher median within 17–22% spread | [Shared scalar admission and unchanged hooks](benchmarks/results/2026-10-16-json-literal-dispatch/README.md) |
| Invocation-owned function locals, 2026-10-16 UTC | cd32a964 → 527ff833; unchanged starred-list helper | No benefit established: 26.602 → 27.550 ms; 3.6% higher median, one unqualified diagnostic | [Capacity admission, cleanup and semantic controls](benchmarks/results/2026-10-16-owned-function-locals/README.md) |
| Ordinary function operand transport, 2026-10-16 | e9dbb7ac → 60961196; unchanged keyword helper loop | No speedup established: 6.800 → 6.680 ms; 1.8% change within 13.3% baseline spread | [Shared binder, live defaults and suspension](benchmarks/results/2026-10-16-ordinary-function-transport/README.md) |
| Direct ordinary builtin len, 2026-10-16 | 17eac376 → d4ac39db; unchanged constructed-text strip/length loop | 24.213 → 17.832 ms; 26.4% lower median in one noisy, unqualified diagnostic | [Argument transport removal and shared guard](benchmarks/results/2026-10-16-direct-length-call/README.md) |
| Ordinary async-host control flow, 2026-10-16 | 6a4a236b → f8d87288; unchanged branch/integer loop | 6.514 → 5.980 ms; 8.2% lower median in one unqualified diagnostic | [Shared handlers, awaited protocols and cleanup](benchmarks/results/2026-10-16-async-control-flow/README.md) |
| Bulk string/bytes hashing, 2026-10-16 | cd4c8b27 → c487cb70; unchanged complete text-summary job | No speedup established: 8.807 → 6.118 ms; 30.5% lower median alongside 30.0% baseline spread | [Safe bulk API, equal-key consistency and ownership](benchmarks/results/2026-10-16-bulk-byte-hash/README.md) |
| Shared zero-argument string entry, 2026-10-16 | df9093cf → ffdac05d; unchanged complete text-summary job | Source simplification; no elapsed improvement established: 6.193 → 6.293 ms, within spread | [Deleted duplicate entries and preserved boundaries](benchmarks/results/2026-10-16-noarg-string-consolidation/README.md) |
| Direct immediate string lower, 2026-10-16 | b41a0345 → 18942340; unchanged complete text-summary job | No speedup established: 6.168 → 5.699 ms; 7.6% lower median within 10.5% baseline spread | [Shared operation, guards and existing ownership](benchmarks/results/2026-10-16-direct-string-lower/README.md) |
| Direct ordinary method transport, 2026-10-16 | ba6086f3 → d88d6440; unchanged 2,048-call method job | 1.145 → 1.060 ms; 7.4% lower median, one unqualified diagnostic | [Argument packaging removed, lowered bodies and guards preserved](benchmarks/results/2026-10-16-lowered-method-transport/README.md) |
| JSON numeric/callback forwarding, 2026-10-16 | 537f8903 → 5d5fd2e7; unchanged complete JSON job | No benefit established: 8.932 → 9.475 ms; 6.1% higher median within 14.2% baseline spread | [Two forwarding state machines removed, guards/lifetimes preserved](benchmarks/results/2026-10-16-json-number-forwarding/README.md) |
| Direct JSON string-token dispatch, 2026-10-16 | b7fd485a → 5ed2b203; unchanged complete JSON job | No improvement established: 9.806 → 9.010 ms; 8.1% change within 10.2% baseline spread | [Shared decoder, admission and checkpoint controls](benchmarks/results/2026-10-16-json-string-dispatch/README.md) |
| Prepared ordinary class methods, 2026-10-16 | 4484f64a → c3db3ca2; fresh compile/run with 16,384 calls | Rejected for Linux containment failure: 9.325 → 11.494 ms; 23.3% higher median, unqualified | [Executable coverage, conservative stack policy and setup erratum](benchmarks/results/2026-10-16-compiled-class-methods/README.md) |
| Direct zero-argument string split, 2026-10-16 | 4717904d → 8b0f32fb; unchanged complete text-summary job | No speedup established: 7.018 → 6.405 ms; 8.7% change within 10.7% baseline spread | [Direct calls, shared kernel and ownership checks](benchmarks/results/2026-10-16-direct-string-split/README.md) |
| Direct synchronous generator advancement, 2026-10-16 | b52be7e4 → 44199dd3; unchanged numeric reduction | 4.118 → 3.912 ms; 5.0% lower median, one unqualified diagnostic | [Direct pull, shared frame guards and lifecycle controls](benchmarks/results/2026-10-16-generator-sync-advance/README.md) |
| Executable generator-expression bodies, 2026-10-16 | c74f53c9 → fa2b72af; unchanged numeric reduction | No speedup established: 4.330 → 4.252 ms; 1.8% change within 3.8% candidate spread | [Real instructions, shared frames and lifecycle checks](benchmarks/results/2026-10-16-executable-generators/README.md) |
| Immediate generator bindings, 2026-10-16 | b387be6d → 569a653a; unchanged numeric reduction | 17.1% lower median: 4.919 → 4.077 ms in one short diagnostic | [Per-yield scan removal and preserved semantics](benchmarks/results/2026-10-16-generator-bindings/README.md) |
| Terminal admission, 2026-10-16 | 159cf7d4 → ae37d1f9; unchanged complete JSON aggregation | Correctness change; no speed change established: 9.713 → 9.492 ms, within baseline spread | [Command failure, ownership cleanup and short diagnostic](benchmarks/results/2026-10-16-terminal-admission/README.md) |
| Fixed plain builtin definitions, 2026-10-16 | 66420bd6 → a2653a61; empty warm invocation control | No improvement established: 23.318 → 23.445 µs; 0.5% higher median within spread | [Bounded metadata reuse, isolation and control limits](benchmarks/results/2026-10-16-builtin-setup/README.md) |
| UTF-8 JSON numeric tokens, 2026-10-16 | 09f44e94 → dc950cde; unchanged complete aggregation | No improvement established: 9.814 → 9.587 ms; 2.3% change within baseline spread | [Shared conversion/preflight and retained hook text](benchmarks/results/2026-10-16-json-utf8-numbers/README.md) |
| Direct UTF-8 JSON tokens, 2026-10-16 | f3e05ad9 → b85fdde9; unchanged complete aggregation job | No improvement established: 10.250 → 10.548 ms; 2.9% higher median within baseline spread | [Intermediate removal and storage/decoding controls](benchmarks/results/2026-10-16-json-utf8-tokens/README.md) |
| Direct string prefix calls, 2026-10-16 | cdf95961 → 473aa396; unchanged complete file search | No improvement established: 5.667 → 3.223 ms; 43.1% change below 57%/64% spreads | [Shared call storage and preserved diagnostics](benchmarks/results/2026-10-16-string-prefix-calls/README.md) |
| Executable subscript augmented stores, 2026-10-16 | a2103402 → 4846928c; unchanged warm configuration job | No improvement established: 3.338 → 3.309 ms; 0.9% change within spread | [Shared operands, deletion and one complete-job diagnostic](benchmarks/results/2026-10-16-augmented-subscript-stores/README.md) |
| Executable member augmented stores, 2026-10-16 | 9d0902a3 → 58aece73; unchanged method job | No improvement established: 1.113 → 1.144 ms; candidate median 2.8% higher | [Existing instructions, deletion and inconclusive timing](benchmarks/results/2026-10-16-augmented-member-stores/README.md) |
| Executable loop/comprehension target stores, 2026-10-16 | 722f4bab → 155843d0; unchanged stable-sort job | Inconclusive: 1.608 → 1.550 ms; baseline spread 3.5% | [Scoped stores, admission and unchanged workload](benchmarks/results/2026-10-16-loop-target-stores/README.md) |
| Executable ordinary subscript stores, 2026-10-16 | a5a96af2 → 7caf4833; unchanged complete text job | Inconclusive: 6.795 →6.414 ms; baseline spread12.4% | [Executable coverage, type slots and operand cleanup](benchmarks/results/2026-10-16-executable-subscript-stores/README.md) |
| Async native integer-index reads, 2026-10-16 | 06e7f2df → 917d8bb9; identical existing ASCII text job | 10.7% lower median, one 512-read diagnostic | [Shared index correctness and guarded dispatch](benchmarks/results/2026-10-16-async-native-index/README.md) |
| Async exact-integer unary dispatch, 2026-10-16 | 66addead → f2fb4e07; identical fixture 5d298b6f | 23.6% lower median, one 2,048-value dynamic unary loop | [One diagnostic and protocol/ownership controls](benchmarks/results/2026-10-16-async-integer-unary/README.md) |
| Eager helper-call comprehension, 2026-10-16 | 3bc463a8 → frozen inlined candidate | 4.2% lower median in one short run; external CPython timing inconclusive (38.3% spread) | [Caller-frame scope/transport evidence](benchmarks/results/2026-10-16-inline-comprehensions/README.md) |
| Exact bytes subtotal, 2026-10-15 | 64fecd53 → frozen candidate | Inconclusive: 6.394 →6.080 ms complete manifest job; spread6.7%/11.7% | [One diagnostic and allocation/preflight removal](benchmarks/results/2026-10-15-bytes-sum/README.md) |
| JSON value forwarding, 2026-10-15 | 50583327 → frozen candidate | Inconclusive: 9.425 →9.692 ms complete JSON job; spread14.3%/7.3% | [One diagnostic and wrapper deletion](benchmarks/results/2026-10-15-json-value-dispatch/README.md) |
| Direct UTF-8 uppercase output, 2026-10-15 | 29a1862e → frozen candidate | Inconclusive: 2.610 →2.651 ms report job; spread9.5%/10.8% | [One diagnostic and allocation removal](benchmarks/results/2026-10-15-utf8-upper/README.md) |
| Direct UTF-8 lowercase output, 2026-10-15 | d083c8cf → frozen candidate | Inconclusive: 7.107 →6.553 ms complete text job; baseline spread7.5% | [One diagnostic and allocation removal](benchmarks/results/2026-10-15-utf8-lower/README.md) |
| Async startup worker reuse, 2026-10-15 | 05eb4efc → frozen prototype, before ambient correctness fix | Setup control only: 110.505 →14.339 us; CPython1.661 us; final delivered source unmeasured | [Noise, source boundary and correctness prerequisite](benchmarks/results/2026-10-15-async-worker-reuse/README.md) |
| Positional operand transport, 2026-10-15 | 1f2ef306 → frozen candidate | 13.7%, 16,384 helper calls through `RunAsync`; baseline spread 9.4% | [One diagnostic and unchanged-tooling proof](benchmarks/results/2026-10-15-positional-calls/README.md) |
| Async integer addition, 2026-10-15 | 88f78106 → frozen candidate | 14.4%, 16,384 loop iterations through `RunAsync`; candidate spread 6.0% | [One diagnostic and protocol regressions](benchmarks/results/2026-10-15-async-integer-dispatch/README.md) |
| Async compiled functions, 2026-10-15 | 015184e6 → frozen candidate | 40.3%, 16,384 helper calls through `RunAsync`; sync inconclusive | [Both API boundaries and failed setup](benchmarks/results/2026-10-15-async-compiled-functions/README.md) |
| Synchronous instruction dispatch, 2026-10-09 | `56d4346d` → `891798a6` | 13.4–14.0%, 16,384-iteration loop | [Both collections](benchmarks/results/2026-10-09-sync-dispatch/README.md) |
| Exact integer addition, 2026-10-09 | `891798a6` → `e97cd4da` | 10.7–14.1%, same loop | [Both collections and rejected alternative](benchmarks/results/2026-10-09-integer-addition/README.md) |
| Function-scoped dispatch, 2026-10-11 | `1bf1e241` → `d10564e7` | 4.44% / 10.10%, complete method job | [Corrected producers, controls and safety evidence](benchmarks/results/2026-10-11-scoped-dispatch-corrected/README.md) |
| Proved impossible string admissions, 2026-10-13 | `7f5fbf70` → `db040f52` | 320.486 → 1.856 ms/job, 128 caught bound errors | [One short comparison, CPU/GC evidence and invalid setup](benchmarks/results/2026-10-13-proved-string-admission/README.md) |

## Remaining uncertainty and maintenance

The complete CSV, text, JSON and file jobs have independent stdout/file goldens
and matched efficient host operations; the leading table records their named
revision snapshots. Fresh compile-to-result coverage beyond configuration,
search and JSON aggregation, current-HEAD performance and host tail latency remain incomplete.
Use targeted comparisons to fill material gaps, rather than a new campaign.

The runtime now uses [cooperative admission and explicit owners](MEMORY.md),
with bounded host-driven executions for long streams. Weak reclamation and
forced collection are removed. This establishes an accounting contract, not a
measurement of host latency or physical CLR memory. The older embedding-pressure
campaigns ran the retired collector-based runtime; their tails and collection
counts must not be read as current behavior. Their complete evidence and limits
remain in [historical embedding qualification](benchmarks/EXPERIMENTS.md#embedding-impact-qualification-before-governed-ownership).
No new host-latency qualification is claimed here.
Regex performance waits
for [managed-only backend qualification](benchmarks/results/2026-10-13-regex-scalar-44/README.md).

For an optimization round, use the existing seconds-long diagnostic command;
reserve ten-minute lanes for declared milestones. Keep failed/incomplete samples,
ordinary limits and frozen acceptance rules. A new result must state its exact
revision, reference version, date, machine, boundary and qualification status.
Update this page's primary table only from that evidence, and retain the prior
identity. Do not relabel historical receipts as current results.

The [list and enumeration checkpoint](benchmarks/results/2026-10-14-list-and-enumeration/README.md)
preserves two additional short comparisons and their preceding-source identities.
Both are inconclusive; neither supplies a qualified speed change or CPython
multiplier. That checkpoint predates governed ownership and does not qualify
current accounting or host latency.

The [zip containment diagnostic](benchmarks/results/2026-10-14-zip-state/README.md)
retains a higher candidate median with substantial noise. Its status is
inconclusive; it does not establish today's HEAD performance or async pull cost.

The [complete JSON decoder experiment](benchmarks/results/2026-10-14-representative-json/README.md)
records 28.0% less managed allocation against earlier Lython. Its nominal 3.7%
lower time remains inconclusive at 11.8% candidate spread. CPython remains the
external timing baseline; allocation totals establish neither RSS nor latency.

The [complete CSV profile and comparison](benchmarks/results/2026-10-14-representative-csv/README.md)
uses the dedicated four-vCPU Ubuntu VM, .NET 10.0.12 and isolated UTF-8 CPython
3.13.16. Its parser candidate is removed: a nominal 2.2% difference is smaller than
15.4% baseline spread. The source and single capture remain historical evidence;
no repeat, qualified ratio or embedding-latency claim follows.

The [governed-memory CSV diagnostic](benchmarks/results/2026-10-14-governed-memory/README.md)
keeps the earlier Lython comparison separate from the primary CPython baseline.
It establishes neither a current complete-script multiplier nor host tail latency.

- [Measurement methods, commands and qualification rules](benchmarks/COMPARISON.md)
- [Experimental history, profiles and rejected optimizations](benchmarks/EXPERIMENTS.md)
- [Historical Lython-only BenchmarkDotNet scan baselines](benchmarks/Baselines.md)

Documentation consolidated 2026-10-12; this date is not a new timing campaign.
