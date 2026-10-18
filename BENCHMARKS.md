# Lython performance against CPython

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Complete text summary with annotated helper [CG], fresh compile/run | 3,072 lines / 35,840 UTF-8 bytes | 7.354–7.451 ms | 1.527 ms | 4.818–4.881× diagnostic | Immutable `4acd532f`; identical controls; 16.27%/3.41% Lython spreads, 1.96% CPython; unqualified |
| Complete CSV summary with annotated helper [CE], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 8.549 ms | 3.962 ms | 2.158× diagnostic | Immutable `34d4da35`; one capture, unqualified; 6.72%/0.63% spreads |
| Complete CSV summary with annotated helper [CF], warm `RunAsync` | 4,096 records / 57,361 UTF-8 bytes | 9.321 ms | 3.759 ms | 2.480× diagnostic | Immutable `34d4da35`; completed host operations, one capture, unqualified; 3.84%/0.59% spreads |
| JSON file round-trip [CD], warm `RunAsync` | One file / 42 UTF-8 input bytes | 1.571 ms | 0.16484 ms | — | Four frozen runtime files on 22648c5a; nominal 9.533×, unqualified; 7.24%/1.07% spreads |
| JSON configuration read/edit/write [CH], fresh compile/run | 16 existing paths / 181 UTF-8 input bytes | 2.584–2.769 ms | 1.399 ms | 1.847–1.980× diagnostic | Immutable `348998dc`; identical controls; 16.63%/9.79% Lython spreads, 8.93% CPython; unqualified |
| JSON configuration read/edit/write [AF], fresh compile/run | 16 existing paths / 181 UTF-8 input bytes | 2.817-3.142 ms | 1.572 ms | — | Two identical Lython controls differ by 11.5%; one diagnostic; no qualified ratio |
| Complete CSV summary [BP], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 7.503 ms | 3.849 ms | — | Short diagnostic at27a61681; nominal8.3% below prior Lython,7.5%/6.4% spreads; inconclusive |
| Complete CSV helper with annotation-only local [BT], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 19.745 ms | 17.014 ms | — | Local Windows, 3f799746 + one-line guard deletion; 48.9%/13.5% spreads, inconclusive |
| Complete text-summary file job [BR], fresh compile/run | 3,072 lines / 35,840 UTF-8 bytes | 14.994–16.539 ms | 11.188 ms | — | Local Windows diagnostic at 877f20e2; identical controls; 49%/107%/39% spreads, inconclusive |
| Complete JSON-lines helper [CI], fresh compile/run | 4,096 records / 110,592 UTF-8 input bytes | 9.466–9.607 ms | 4.441 ms | 2.132–2.163× diagnostic | Immutable `6584ddd7`; identical controls; 13.11%/3.52% Lython spreads, 0.36% CPython; unqualified |
| Complete JSON-lines aggregation [AU], fresh compile/run | 4,096 records / 110,592 UTF-8 bytes | 9.880–10.024 ms | 4.359 ms | — | Two identical Lython controls; unqualified (21.3%/8.7% spread); startup excluded |
| Complete JSON-lines aggregation with helper [CC], warm `RunAsync` | 4,096 records / 110,592 UTF-8 bytes | 10.854 ms | 4.283 ms | — | One frozen text-reader file on 56526237; nominal 2.534×, unqualified; 1.77%/1.06% spreads |
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
above 1 means Lython takes longer. Ratios marked diagnostic are unqualified point
estimates. A dash means unavailable or not reported, never zero; historical
unqualified observations retain their original notation. No workload ratio has
completed milestone qualification here. AF, AU, BO, BR, BX, CG, CH and CI ranges contain the medians
of two identical Lython controls, not confidence intervals. Other entries show
one diagnostic median. The jobs show performance gaps in those runs; their
magnitudes and changes remain uncertain as marked.

## Which code and machine were measured?

CI measures immutable `6584ddd7` on 2026-10-18 UTC with the existing complete
JSON-lines helper job, two identical Lython controls and CPython 3.13.16. Each
job compiles afresh in an already-running host and includes full file work/reset/
checks; startup/transport are excluded. The sole 8.659-second capture gives
9.466–9.607 ms versus 4.441 ms, a nominal 2.132–2.163× ratio. Controls differ by
1.49%, but one Lython spread is 13.11%; this remains diagnostic/unqualified.
Historical fresh AU and warm-async CC stay separate. No individual-fix gain,
compilation-only timing, finite-budget or embedding-latency claim is inferred.

CH measures immutable `348998dc` on 2026-10-18 UTC, using the existing complete
configuration read/edit/write script, two identical Lython controls and CPython
3.13.16. Every job compiles afresh in an already-running host, including file
setup/reset and full output checks; startup/transport are excluded. The sole
8.074-second collection gives 2.584–2.769 ms versus 1.399 ms, a diagnostic 1.847–1.980×
gap. The identical controls differ by 6.69% and one Lython spread is 16.63%; the
ratio remains unqualified. It is not a measured gain from an individual setup
change, nor a trend against historical AF. [Source, boundaries, samples and
cleanup](benchmarks/results/2026-10-18-config-checkpoint/README.md) are retained.

CG measures immutable `4acd532f` on 2026-10-18 UTC, whose runtime source is
identical to qualified `73deb8dc`. Two distinct workers load identical Lython
binaries. This complete text helper job compiles afresh and calls public Run
inside an already-running host; file setup/reset and full output checks are
included, startup/transport excluded. CPython is 3.13.16 with the VM toolchains
below. The one 7.725-second collection gives a diagnostic 4.818–4.881× gap;
16.27% spread in one Lython control leaves its magnitude uncertain. It is not
an earlier-Lython improvement or a measurement of the rejected casing prototype.
[Source, boundary, raw samples and cleanup](benchmarks/results/2026-10-18-text-milestone/README.md)
are retained. Historical Windows text snapshot BR remains separate.

CE/CF measure immutable `34d4da35` on 2026-10-18 UTC, using the existing complete
CSV helper script and pinned toolchains below. CE compiles afresh and calls
public Run; CF reuses compilation and calls public RunAsync with completed host
operations. Both include fresh invocation, result cleanup, file staging/reset and
complete output checks; startup/transport are excluded. The lanes cannot be
subtracted to estimate compile cost or compared to attribute async overhead.
One short capture per lane leaves ratios diagnostic. Full provenance, samples
and cleanup are [linked here](benchmarks/results/2026-10-18-csv-milestone/README.md).
Earlier CSV snapshots BU/BW remain in the historical revision table below.

The VM snapshots (all leading rows except BQ/BR/BT, and historical Q/L campaigns) used the same dedicated **Azure Standard_F4as_v7**: Ubuntu 24.04.4 LTS
x86_64, four AMD EPYC 9V45 vCPUs, one thread per core, 16 GiB RAM, no swap.
Earlier VM snapshots used clean Release builds with **.NET SDK 10.0.401 / runtime 10.0.12**.
The primary external reference was **CPython 3.13.16**, built with GCC 13.3,
PGO/LTO, ordinary GIL and cyclic GC, without debug mode or JIT, launched with
`-I -S`. Workers kept ordinary GC/tiering and public Lython limits; optional
instruction fuel was unset. Both engines ran sequentially on that machine.
The linked receipts identify binaries, inputs, toolchains and sampling policies.

BV uses clean source builds with SDK 10.0.401 and the same VM/reference versions.
It measures warm public `RunAsync` with completed host operations, including
invocation setup, file staging/checks and prior-result cleanup. It does not measure
suspended I/O. One short capture leaves the ratio unqualified; its 0.31% median
change from earlier Lython is within sample spread. Compilation and startup are
excluded, so BV cannot establish a change from the fresh/synchronous BU boundary.

BW uses the same boundary and toolchains with one frozen CSV runtime file on
1b6a1531. The extra async pull forwarding method is removed; its 1.87% higher
median than earlier Lython is within spread. No whole-job gain or regression is
established. The measured source hash is recorded; it is not an immutable HEAD
build. BV remains visible below as the preceding snapshot.

BX uses two identical clean f728636a workers for the complete JSON helper job.
BY uses the same boundary/toolchains with two frozen JSON runtime files on that
base. It removes default option-record allocations; the paired elapsed change
is only 0.11%, within spread. External ratios remain unqualified. These separate
captures cannot establish a trend between captures.

BZ uses the same boundary/toolchains with one frozen JSON runtime file on
e470ccc2. It limits token-sized scratch to the allocating integer fallback.
The native allocation reduction is separate from elapsed time: its 1.26% higher
complete-job median is within spread. BY remains visible below; these captures
must not be pooled or treated as measurements of later HEADs.

CA uses the same boundary/toolchains with one frozen JSON module file on
6c9a51df. Ordinary single-positional `json.loads` avoids general binding storage.
Its separate native diagnostic saves 88 bytes per eligible call through each
API. The sole complete-job capture records a nominal 2.2% lower median; this
short diagnostic does not qualify a speedup or an embedding-latency improvement.

CB uses the same boundary/toolchains with one frozen key-comparer file on
aa9fc0ea. It accepts key identity (fixing NaN lookup) and compares builtin strings
without general equality/suppression. The final-source diagnostic records a
nominal 6.4% lower median, unqualified. A preceding string-only prototype and its
failed independent NaN probe remain separate evidence; no captures are pooled.

CC uses the same boundary/toolchains with one frozen text-reader file on
56526237. It governs accumulated reads and removes duplicate text storage and
empty ownership hooks. The 1.71% lower median is within sample spread; no elapsed
benefit or embedding-latency improvement is established. The unchanged JSON-lines
job exercises ranged text reads; dedicated regressions cover long accumulation.

CD reuses the existing bridge JSON file job, with relative fixture paths and
four frozen runtime files on 22648c5a. It exercises the ordinary default dumps
shape; other JSON rows often explicitly supply options. The separate scalar-call
allocation diagnostic saves 1,840 bytes per eligible call. Whole-job time is
3.80% higher, within 7–9% Lython spread; no gain or regression is established.

A [follow-up CPU profile](benchmarks/results/2026-10-18-bridge-profile/README.md)
of the same bridge job/source is closed without a performance change. JIT,
fixture reset and protocol/scheduling are material, with protocol outside timed
batches included. Its overlapping CPU fractions cannot explain CD's elapsed
ratio or establish fixed builtin/setup costs. No repeat trace was run.

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
| CI | 2026-10-18 UTC | Immutable `6584ddd7`; complete JSON helper job, fresh compile/run, identical controls | [CPython comparison, full goldens, raw samples and cleanup](benchmarks/results/2026-10-18-json-checkpoint/README.md) |
| CH | 2026-10-18 UTC | Immutable `348998dc`; complete configuration job, fresh compile/run, identical controls | [CPython comparison, independent complete goldens, raw samples and cleanup](benchmarks/results/2026-10-18-config-checkpoint/README.md) |
| CG | 2026-10-18 UTC | Immutable `4acd532f`; same runtime sources as `73deb8dc`; complete text helper, fresh compile/run, identical controls | [CPython comparison, full goldens, raw samples and cleanup](benchmarks/results/2026-10-18-text-milestone/README.md) |
| CE/CF | 2026-10-18 UTC | Immutable `34d4da35`; complete CSV helper job, fresh compile/run and warm RunAsync | [CPython comparison, raw samples, immutable controls and cleanup](benchmarks/results/2026-10-18-csv-milestone/README.md) |
| CD | 2026-10-18 UTC | Four frozen runtime files on 22648c5a; existing bridge JSON file job; warm public RunAsync | [Default-call allocation, checkpoints, CPython comparison and cleanup](benchmarks/results/2026-10-18-json-default-dumps/README.md) |
| CC | 2026-10-18 UTC | One frozen text-reader file on 56526237; complete helper job; warm public RunAsync | [Admission, character limits, CPython comparison and cleanup](benchmarks/results/2026-10-18-text-read/README.md) |
| CB | 2026-10-17 UTC | One frozen key-comparer file on aa9fc0ea; complete helper job; warm public RunAsync | [Key identity correction, string dispatch, CPython controls and final-source comparison](benchmarks/results/2026-10-17-string-key-identity/README.md) |
| CA | 2026-10-17 UTC | One frozen JSON module file on 6c9a51df; complete helper job; warm public RunAsync | [Ordinary call binding, preceding CPU attribution, native allocations and CPython comparison](benchmarks/results/2026-10-17-json-default-call/README.md) |
| BZ | 2026-10-17 UTC | One frozen JSON runtime file on e470ccc2; complete helper job; warm public RunAsync | [Scratch placement, allocation controls, CPython comparison and cleanup](benchmarks/results/2026-10-17-json-number-scratch/README.md) |
| BY | 2026-10-17 UTC | Two frozen JSON runtime files on f728636a; complete helper job; warm public RunAsync | [Source identity, native allocations, CPython comparison and cleanup](benchmarks/results/2026-10-17-json-default-options/README.md) |
| BX | 2026-10-17 UTC | f728636a; two identical clean Lython controls; complete JSON helper job | [Immutable controls, exact outputs, identities and cleanup](benchmarks/results/2026-10-17-json-helper-warm-async/README.md) |
| BW | 2026-10-17 UTC | One frozen CSV runtime file on 1b6a1531; complete helper job; warm public RunAsync | [Working-source identity, CPython comparison, prior CPU attribution and cleanup](benchmarks/results/2026-10-17-csv-pull/README.md) |
| BV | 2026-10-17 UTC | 1b6a1531; complete CSV helper job; dedicated VM warm public RunAsync | [CPython comparison, unchanged outputs, identities and cleanup](benchmarks/results/2026-10-17-csv-helper-warm-async/README.md) |
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
| Complete JSON-lines aggregation with helper [CB], warm `RunAsync` | 4,096 records / 110,592 UTF-8 bytes | 10.953 ms | 4.259 ms | — | One frozen key-comparer file on aa9fc0ea; nominal 2.572×, unqualified; 1.99%/1.05% spreads |
| Complete JSON-lines aggregation with helper [CA], warm `RunAsync` | 4,096 records / 110,592 UTF-8 bytes | 11.631 ms | 4.245 ms | — | One frozen JSON module file on 6c9a51df; nominal 2.740×, unqualified; 1.67%/0.83% spreads |
| Complete JSON-lines aggregation with helper [BZ], warm `RunAsync` | 4,096 records / 110,592 UTF-8 bytes | 12.438 ms | 4.288 ms | — | One frozen JSON runtime file on e470ccc2; nominal 2.901×, unqualified; 5.45%/0.92% spreads |
| Complete JSON-lines aggregation with helper [BY], warm `RunAsync` | 4,096 records / 110,592 UTF-8 bytes | 12.993 ms | 4.520 ms | — | Two frozen JSON runtime files on f728636a; nominal 2.875×, unqualified; 2.23%/1.02% spreads |
| Complete JSON-lines aggregation with helper [BX], warm `RunAsync` | 4,096 records / 110,592 UTF-8 bytes | 12.223–12.340 ms | 4.309 ms | — | Identical f728636a controls differ by 0.95%; nominal 2.837–2.864×, unqualified |
| Complete CSV summary with annotated helper [BV], warm `RunAsync` | 4,096 records / 57,361 UTF-8 bytes | 10.346 ms | 3.756 ms | — | Dedicated-VM diagnostic at 1b6a1531; nominal 2.755×, unqualified; 2.03%/0.51% spreads |
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

The [sum cursor/acquisition change](benchmarks/results/2026-10-18-sum-cursor/README.md)
at `74804edc` plus its frozen one-file patch fixes iterator acquisition before
invalid-start rejection. General async sum also removes two forwarding objects,
saving 96 B per affected call; sync/exact-byte controls and 126 valid-path boundary
records match. Eight initial public compatibility/I/O failures are corrected.
Elapsed was not measured; no CPython, fresh-compilation or whole-job gain is claimed.

The [JSON dictionary-helper capture removal](benchmarks/results/2026-10-18-json-dictionary-capture/README.md)
at `15b4bc03` plus its frozen one-file patch avoids an 80-byte capture per value.
A 64-value sequence saves 5,200 B and 64 small dictionaries save 15,440 B in
both modes. Output/fuel/guard semantics match; incremental encoding and parsing
controls are unchanged. This is allocation evidence only: elapsed was not measured,
and no CPython, fresh-compilation or complete-job speedup is claimed.

The [dictionary view call change](benchmarks/results/2026-10-18-dict-view-call/README.md)
at `c362a081` plus its frozen patch avoids receiver wrappers for immediate
zero-argument `items`/`keys`/`values`. A 64-distinct-dictionary diagnostic allocates
13,112→6,456 B sync and 13,600→6,432 B async. Exposed/expanded/custom controls
and all 576 boundary records match. Elapsed was not measured; no CPython,
compile-to-result or whole-job speedup is claimed.

The [function-scope facts change](benchmarks/results/2026-10-18-scope-facts/README.md)
at `241dd167` plus its frozen five-file patch reuses existing compiled metadata.
A 64-class-method job allocates 789,400→680,856 bytes in both execution modes;
ordinary allocation controls and all native step arrays match. Timing is
inconclusive: noisy/slower controls remain visible, with no latency or CPython
gain claim. Unused definitions retain their facts earlier.

The [executable generic-function change](benchmarks/results/2026-10-18-generic-functions/README.md)
at `99eb28bf` plus its three-file runtime/frontend patch preserves existing type
scopes and captures. A 128-call native job allocates 92,008→37,088/37,128 bytes in
sync/async execution; ordinary control arrays match. One local public fresh
compile-to-result observation changes 0.664→0.612ms sync and 0.757→0.652ms async,
unqualified, with ordinary controls and spreads retained. This is no CPython,
startup or representative-job gain claim; execution-step counts also change.

The [async any/all cursor change](benchmarks/results/2026-10-18-any-all-cursor/README.md)
at `7c79191f` plus its frozen one-file runtime patch removes two enumeration
wrappers per general call. One native comparison saves 96 bytes for empty/full/
early-exit list and tuple inputs; sync, bytes and range controls are unchanged.
This is allocation evidence only, with no elapsed or CPython speedup claim.

The [general default-map builders](benchmarks/results/2026-10-18-default-map/README.md)
at `10d108eb` plus a four-line runtime patch pre-size existing dictionaries.
One native comparison saves 552 bytes with eight defaults and 912 bytes with 32,
for both helpers; empty/single and executable controls are unchanged. This is
allocation evidence only; the added counting pass has not been timed.

The [binding-plan constructor](benchmarks/results/2026-10-18-binding-plan/README.md)
at `de6efc09` plus its frozen one-file runtime patch fills final parameter arrays
directly. One native comparison saves 96, 160 and 304 bytes per empty, single and
mixed plan; context controls and 27 admission/rollback records are unchanged.
This is an allocation saving, with no elapsed or CPython speedup claim.

The [immutable sorted definition](benchmarks/results/2026-10-18-sorted-definition/README.md)
at `75ad9b2e` plus its frozen one-file runtime patch removes per-run callable/
signature construction. One fixed warm-context comparison reduces fresh-root
allocation from 16,240 to 16,080 bytes; child controls stay at 240 bytes. This is
an allocation saving, not an elapsed, startup, complete-job or CPython speedup.

The [ordinary join call path](benchmarks/results/2026-10-18-join-call/README.md)
at `c3d5a2d6` plus its frozen six-file patch avoids eligible bound-method/argument
packaging. One qualified native comparison saves 352 bytes per synchronous call
and 320 per asynchronous call. Exposed/expanded/custom controls and all 448 exact
behavior/fuel records are unchanged. This is an allocation result, with no
elapsed-time, complete-job or CPython speedup claim.

The [join correctness correction](benchmarks/results/2026-10-18-join-drain/README.md)
is tracked separately from speedups: both APIs now preserve iterable effects/error
order and fund scratch through output admission. At `deefa4a5` plus its frozen
patch, a16-part native diagnostic costs an additional416 bytes/synchronous call
and40 bytes/asynchronous call. Nine baseline defects are fixed; there is no
elapsed-time, complete-job or CPython speedup claim.

The [small JSON result conversion](benchmarks/results/2026-10-18-json-result-copy/README.md)
at `32867fac` plus its frozen patch replaces the temporary CLR string with bounded
managed spans for results up to 128 UTF-16 units. One fixed native capture saves
40–280 bytes per eligible call in both hosting APIs; larger controls and all 560
behavior/fuel records are unchanged. This is allocation against earlier Lython,
with no elapsed-time, complete-job or CPython speedup claim.

The [module namespace simplification](benchmarks/results/2026-10-18-builtin-namespace/README.md)
corrects builtin fallback after deletion and removes the copied builtin dictionary
and alias bookkeeping. One fixed warm context diagnostic reduces fresh allocation
from 19,176 to 16,240 bytes and child allocation from 264 to 240 bytes. All 1,491
focused cases pass. This is allocation against earlier Lython, not an elapsed,
compile-to-result, whole-job or CPython speedup measurement.

The [two-positional split diagnostic](benchmarks/results/2026-10-18-split-call-count/README.md)
at `7bf17b16` plus its frozen four-file runtime patch saves 336 bytes/sync call
and 304 bytes/async call. All 240 work/error records match; zero/one-argument and
exposed controls are unchanged. This native-loop allocation result makes no
elapsed-time, complete-job or CPython speedup claim.

The [int/float setup diagnostic](benchmarks/results/2026-10-18-builtin-signatures/README.md)
at `4e55ff0d` plus its frozen one-file patch reduces native warm context allocation
from 19,312 to 19,176 bytes; the child-context control stays at 264 bytes. This
is an allocation result, with no elapsed-time or CPython speedup claim.

The [native abs call diagnostic](benchmarks/results/2026-10-18-native-abs/README.md)
at `2a268ae8` plus its frozen four-file runtime patch saves 32 bytes per ordinary
integer/float/bool call on both APIs. Expanded/protocol controls and 132 exact
work/identity/error records are unchanged. This is a local Windows x64 native-loop
allocation result, not elapsed-time, complete-job or CPython performance evidence.

Executable compilation [lifetime evidence](benchmarks/results/2026-10-18-lowered-root/README.md)
no longer retains the unused whole lowered module through executable programs
and compiled runners. Source syntax and needed function/fallback data remain.
Three failed baseline lifetime cases now pass; this establishes reference
release, not elapsed, byte or RSS savings. No new timing comparison was run.

Default JSON encoding [CD](benchmarks/results/2026-10-18-json-default-dumps/README.md)
omits unobservable module default-call binding, encoder construction and member
packaging. Separate native scalar counts save 1,840 bytes per eligible call
through either API; general calls save 64 bytes from the stateless base default.
The complete file job records 1.514 → 1.571 ms, spreads 8.54%/7.24%: nominal
3.80% higher, within spread. Only the allocation reduction is established.

Governed text reads [CC](benchmarks/results/2026-10-18-text-read/README.md)
remove empty ownership hooks, duplicate UTF-16 window/cursor tracking and CLR
accumulation/re-encoding. Working capacity and final-result overlap use existing
admission and deterministic cleanup. The complete-job diagnostic records
11.042 → 10.854 ms, spreads 2.62%/1.77%: nominal 1.71% lower, within spread.
No elapsed benefit or allocation magnitude is established.

Builtin string-key comparison [CB](benchmarks/results/2026-10-17-string-key-identity/README.md)
omits thread-local guest-dispatch suppression and general numeric/structural
equality for callback-free strings. Key identity now also fixes non-reflexive
float/complex NaN lookup without changing ordinary value equality. Native controls
allocate zero bytes before and after; fixed operands permit JIT folding/hoisting,
so their elapsed figures do not establish per-call costs. The final complete job
records 11.700 → 10.953 ms, spreads 1.87%/1.99%: nominal 6.4% lower, unqualified.

Ordinary JSON loads [CA](benchmarks/results/2026-10-17-json-default-call/README.md)
removes the eight-slot binding array and default option/class forwarding per
single-positional call. Separate native counts save 11,264 bytes per 128 calls
through either API; keyword/explicit-class/hook controls are unchanged. The sole
complete-job capture records 11.893 → 11.631 ms, spreads 1.82%/1.67%: nominal
2.2% lower, unqualified. Preceding CPU attribution selects this source boundary;
it is not a prediction or before/after CPU measurement.

JSON numeric scratch placement [BZ](benchmarks/results/2026-10-17-json-number-scratch/README.md)
removes one temporary lease and reserve/release traffic from span conversions and
numeric hooks. A separate native diagnostic records 4,096 fewer bytes per 128
conversions; the allocating large-integer control is unchanged. The complete job
measures 12.283 → 12.438 ms, spreads 1.57%/5.45%: the 1.26% higher median is
inconclusive. No whole-job or embedding-latency improvement is established.

Shared JSON defaults [BY](benchmarks/results/2026-10-17-json-default-options/README.md)
remove 11,264 → 0 bytes per 128 default option bindings in a separate local native
diagnostic. False/custom strict values retain fresh records. The complete job
measures 13.007 → 12.993 ms, spreads 2.39%/2.23%: no elapsed improvement established.
The immutable BX controls are separate and must not be pooled with this capture.

The CSV pull consolidation [BW](benchmarks/results/2026-10-17-csv-pull/README.md)
removes one forwarding method and separately compares immutable 1b6a1531 with the
frozen candidate: 10.007 → 10.194 ms, spreads 4.31%/2.43%. The 1.87% higher median
is inconclusive. The preceding profile is source-selection evidence only; no
before/after CPU, allocation or complete-job speedup is claimed.

The complete CSV helper [BV](benchmarks/results/2026-10-17-csv-helper-warm-async/README.md)
separately compares 5b546b2e → 1b6a1531 through warm `RunAsync`:
10.379 → 10.346 ms, with 1.30%/2.03% spreads. The 0.31% lower median establishes
no complete-job improvement. The native loop-step diagnostic below remains a
separate boundary; its magnitude does not transfer to this CSV job.

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
| Native text operations through async hosting, 2026-10-18 UTC | 7149dac7 → frozen one-predicate patch; 2,048-comparison native loops | Local async-string median 2.7052 → 2.0524 ms, nominal 24.1% lower; allocations unchanged and controls pass. Unqualified; no complete-job or CPython timing claim | [Existing native dispatch, raw controls, protocols and owners](benchmarks/results/2026-10-18-native-text/README.md) |
| Native string separator calls, 2026-10-18 UTC | 217b3880 → frozen seven-file direct split patch; 128-call native loops | Allocation only: saves 264 bytes/sync call and 232 bytes/async call for separator/None; omitted/exposed controls unchanged. No elapsed or CPython timing claim | [Shared validation, exact work records and ownership controls](benchmarks/results/2026-10-18-string-split/README.md) |
| Bound async forwarding, 2026-10-18 UTC | ab6cb90b → frozen one-method forwarding patch | Allocation only: genuinely pending list/sum entry prefixes save 152 bytes each; completed 128-call controls unchanged. No elapsed, total suspended-allocation or CPython timing claim | [Removed state, actual suspension, binding and cleanup controls](benchmarks/results/2026-10-18-bound-async/README.md) |
| Typed JSON object keys, 2026-10-18 UTC | 35683fb4 → frozen one-file typed key patch; native 64-call parser samples | No improvement established: allocations unchanged; dictionary and array controls have wide timing spreads. No whole-job or CPython timing claim | [Removed generic awaits, exact boundaries and inconclusive timing](benchmarks/results/2026-10-18-json-keys/README.md) |
| Fixed builtin dictionary capacities, 2026-10-18 UTC | b818855b → frozen one-file capacity patch; 32 warm native context constructions | Allocation only: 26,088 → 19,312 bytes per context; shared owners return to zero. No elapsed or CPython timing claim | [Two capacity hints, isolation and raw samples](benchmarks/results/2026-10-18-builtin-capacity/README.md) |
| Ordinary string suffix calls, 2026-10-18 UTC | 015ee44a → frozen five-file affix operand patch; warm 128-call Unicode loop | Allocation only: immediate `endswith` 48,640 → 7,680 bytes sync / 44,520 → 7,656 async. Exposed-method and `startswith` controls unchanged. No elapsed or CPython timing claim | [Shared affix path, exact boundaries and samples](benchmarks/results/2026-10-18-string-suffix/README.md) |
| Private list handoff cleanup, 2026-10-18 UTC | 19cd6654 → frozen two-file cleanup plus class deletion; 128 native calls | Allocation only: known-capacity lists 53,248 → 45,056 bytes sync / 62,464 → 54,272 async; custom lists also save 8,192 bytes. Byte-sum control unchanged. No elapsed or CPython timing claim | [Deleted wrapper, frozen ordering, pending cleanup and samples](benchmarks/results/2026-10-18-list-handoff/README.md) |
| Sum/list operation-state cleanup, 2026-10-18 UTC | 92c5b9e6 → frozen four-file cleanup/rename; 128 native calls | Allocation only: byte checksum 22,528 → 12,288 bytes through either API; generic/custom sums save 10,240 bytes and list controls save 2,048. No elapsed or CPython timing claim | [Removed state, samples, ownership and checks](benchmarks/results/2026-10-18-sum-state/README.md) |
| Ordinary binary file writes, 2026-10-18 UTC | 92f98863 → frozen five-file call-transport patch; 128 writes | Allocation only: sync 21,672 → 17,208 bytes; async 64,312 → 17,208. Exposed methods and native text controls unchanged. No elapsed or CPython timing claim | [Samples, frozen checkpoints, ownership and boundary](benchmarks/results/2026-10-18-binary-write/README.md) |
| Complete CSV helper milestone, 2026-10-18 UTC | Immutable `1b6a1531` → immutable `34d4da35`; same script/harness | Nominal fresh 6.92% and warm 5.66% lower; single captures do not establish improvement or attribute it to one change | [Separate earlier-Lython results and spread](benchmarks/results/2026-10-18-csv-milestone/README.md#comparison-with-earlier-lython) |
| Bulk CSV writerows pipeline, 2026-10-18 UTC | 6bec0f34 → frozen one-file pipeline consolidation; one call / 128 rows | Allocation only: async native rows 153,856 → 144,640 bytes; sync and guest-generator controls unchanged. No elapsed or CPython timing claim | [Samples, exact bulk checkpoints and boundary](benchmarks/results/2026-10-18-csv-writerows/README.md) |
| Native rows in async CSV writerow, 2026-10-18 UTC | bca120a4 → frozen one-file converter-selection patch; 128 raw-row operations | Allocation only: async helper 153,600 → 144,384 bytes, matching sync. Conversion/publication controls unchanged; not a whole interpreter/job comparison. No elapsed or CPython timing claim | [Isolated phases, samples, guards and ownership boundary](benchmarks/results/2026-10-18-csv-native-conversion/README.md) |
| Ordinary CSV writerow calls, 2026-10-18 UTC | 75c03d02 → frozen six-file call-transport patch; 128 rows | Allocation only: sync 153,488 → 149,144 bytes; async interpreter 190,104 → 158,360. Exposed controls unchanged. No elapsed or CPython timing claim | [Samples, frozen checkpoints, ownership and boundary](benchmarks/results/2026-10-18-csv-writerow/README.md) |
| JSON string emission, 2026-10-18 UTC | 765a09fc → frozen three-file decode/preflight patch; 128 native dumps calls | Allocation only: 32-byte ASCII 117,760 → 106,496 bytes; 16-key dictionary 507,904 → 442,368. Large inputs add 32 bytes per operation for working admission; integer control unchanged. No elapsed or CPython timing claim | [Boundary, Unicode controls, refusal regressions and samples](benchmarks/results/2026-10-18-json-strings/README.md) |
| JSON integer emission, 2026-10-18 UTC | 9eb065eb → frozen four-file append/preflight patch; 128 native operations | Allocation only: default dumps `123456` 69,632 → 64,512 bytes, numeric key 111,616 → 106,496. `42`/float controls unchanged; large values add working leases. No elapsed or CPython timing claim | [All routes, samples, refusal regressions and ownership boundary](benchmarks/results/2026-10-18-json-integers/README.md) |
| Ordinary integer rendering, 2026-10-18 UTC | 2c381510 → frozen one-file rendering patch; 128 native conversions | Allocation only: `123456` 23,552 → 18,432 bytes; signed-64 maximum 28,672 → 20,480. `42` unchanged; large integers add a 32-byte working reservation per conversion. No elapsed or CPython timing claim | [All samples, refusal witness, tradeoffs and boundary](benchmarks/results/2026-10-18-integer-rendering/README.md) |
| Ordinary staged text-file writes, 2026-10-18 UTC | 2b68d4ea → frozen six-file call-transport patch; 128 native writes | Allocation only: sync 15,432 → 11,088 bytes; async interpreter 42,832 → 11,088 bytes. Exposed controls unchanged; public execution, whole-script time and CPython unmeasured | [Samples, frozen fuel/effects, source identities and boundary](benchmarks/results/2026-10-18-text-file-write/README.md) |
| Async loop-step dispatch, 2026-10-17 UTC | 5b546b2e → frozen two-file patch; warm4,096 native iterations | RunAsync range2.6685 →0.9742ms; list1.0530 →0.8592ms. One unqualified capture with wide spreads; allocations unchanged | [Exact fuel controls, all samples and boundary](benchmarks/results/2026-10-17-for-next/README.md) |
| Prepared async calls, 2026-10-17 UTC | 7ac0d4c0 → frozen two-file patch; warm128 sorts plus one range construction | Allocation only: RunAsync154,328 →142,976bytes; keyword156,376 →145,024bytes. Sync controls unchanged; no elapsed claim | [Samples, managed dispatch and boundary](benchmarks/results/2026-10-17-prepared-async/README.md) |
| Named builtin binding, 2026-10-17 UTC | 49b6b65e → frozen one-file patch; warm128-call native sorted loop | Allocation only: sync139,904 →133,760bytes; RunAsync160,472 →154,328bytes. Keyword control unchanged at the median; no elapsed claim | [Samples, semantic records and boundary](benchmarks/results/2026-10-17-named-binding/README.md) |
| Generator compiler metadata, 2026-10-17 UTC | d95638ba →72feb124 → frozen combined two-file patch;16 fresh compilations including validation/disposal | Allocation only: generator1,803,520 →1,762,560bytes;ordinary1,579,392 →1,577,344bytes. Lambda controls unchanged;no elapsed claim | [Separate phases,samples,sources and boundary](benchmarks/results/2026-10-17-generator-scope/README.md) |
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

The [native subscript forwarding candidate](benchmarks/results/2026-10-18-subscript-forward-rejected/README.md)
missed its fixed timing gate and was restored. Allocation and 320 work records are
unchanged; this is a rejected local diagnostic, not an accepted HEAD improvement.

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
