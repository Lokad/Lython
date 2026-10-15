# Lython performance against CPython

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Stable sort with lambda key [AL], warm `Run` | 2,048 tuples; build, stable-sort and print | 1.595 ms | 0.3020 ms | — | One diagnostic; observed 10.2% lower Lython median; executable lambda bodies; comprehension unchanged |
| Expanded-call forwarding helper [AK], warm `Run` | 2,048 calls; tuple and keyword mapping | 2.077 ms | 0.2443 ms | — | One diagnostic; observed 13.9% lower Lython median; fallback/mirroring removed; general binder remains |
| Formatted report helper [AJ], warm `Run` | 2,048 calls; padded row and computed next value | 2.125 ms | 0.4201 ms | — | One diagnostic; observed 13.4% lower Lython median; ordinary f-string fallback removed |
| Expanded set/dictionary helper [AI], warm `Run` | 2,048 calls; mixed literal/expanded entries | 4.963 ms | 0.6326 ms | — | One diagnostic; observed 8.8% lower Lython median; fallbacks/mirroring removed |
| Ordinary starred-tuple helper [AH], warm `Run` | 2,048 calls; six-element immutable results | 2.629 ms | 0.3477 ms | — | One diagnostic; elapsed change within spread; fallback removed, conservative overlap funded |
| Ordinary starred-list helper [AG], warm `Run` | 2,048 calls; six-element merged lists | 2.179 ms | 0.3541 ms | — | One diagnostic; observed 18.2% lower Lython median; fallback/mirroring removed |
| JSON configuration read/edit/write [AF], fresh compile/run | 16 existing paths / 181 UTF-8 input bytes | 2.817-3.142 ms | 1.572 ms | — | Two identical Lython controls differ by 11.5%; one diagnostic; no qualified ratio |
| Branch/integer loop [AE], warm `RunAsync` | 16,384 iterations; modulo/comparison/add/subtract | 6.321 ms | 0.9599 ms | — | One diagnostic; observed 35.8% lower Lython median; remaining gap substantial |
| Complete copy/checksum/manifest job [AD], warm | Two files / 4,107 input bytes | 6.080 ms | 0.4382 ms | — | One diagnostic; difference within spread; byte-allocation removal only |
| Complete JSON-lines aggregation [AC], warm | 4,096 records / 110,592 UTF-8 bytes | 9.692 ms | 4.260 ms | — | One diagnostic; elapsed change within spread; redundant wrapper removed |
| Complete uppercase-report file job [AB], warm | 1,024 lines / 23,552 UTF-8 bytes | 2.651 ms | 0.2444 ms | — | One diagnostic; elapsed change within spread; allocation removal only |
| Complete text-summary file job [AA], warm | 3,072 lines / 35,840 UTF-8 bytes | 6.553 ms | 1.231 ms | — | One diagnostic; observed improvement near baseline spread; no qualified ratio |
| Default-argument helper [Z], fresh compile/run | 256 calls | 0.186 ms | 0.0373 ms | — | Historical diagnostic at 3b34fad2; sharing prototype was slower and removed; final cleanup unmeasured |
| Split lines, strip and sum lengths [X], warm `RunAsync` | 32,768 lines / 425,984 UTF-8 bytes | 23.464 ms | 3.132 ms | — | One default-strip diagnostic on 88e0992b; inconclusive, baseline/candidate spread 16.5%/23.3% |
| Unicode string-key word counts [W], warm `RunAsync` | 16,384 updates / four keys; completed host operations | 15.146 ms | 1.313 ms | — | One direct-get diagnostic on 7e2c1f27; observed 16.0% lower Lython median; remaining gap substantial |
| Append to distinct lists and total values [N], warm `RunAsync` | 16,384 lists/appends; completed host operations | 7.819 ms | 1.132 ms | — | One direct-append diagnostic on 99e8701f; observed 21.8% lower Lython median; no qualified speedup |
| Full-body parameter-update helper [B], warm `RunAsync` | 16,384 calls; completed host operations | 10.372 ms | 1.287 ms | — | One matching-local-storage diagnostic on f93e9258; observed 3.6% lower Lython median; no qualified speedup |
| Keyword helper [K], warm `Run` | 16,384 calls | 4.262 ms | 1.174 ms | — | One call-cache deletion diagnostic on 6262ea94; difference within spread; no timing claim |
| Helper using positional/keyword defaults [V], warm `RunAsync` | 16,384 calls; completed host operations | 9.555 ms | 1.258 ms | — | One correctness/admission diagnostic on 96596466; candidate median 4.2% higher; spreads 3.0%/3.9% |
| Tuple-key dictionary updates [D], warm `Run` | 16,384 updates | 30.573 ms | 2.418 ms | — | One method-identity diagnostic on 0eb587a9; difference from earlier Lython is within spread |
| Integer reduction loop [I], warm `RunAsync` | 16,384 iterations; completed host operations | 2.660 ms | 0.579 ms | — | One diagnostic on 88f78106; 14.4% lower median than earlier async Lython; candidate spread 6.0% |
| Integer helper calls [E], warm `Run` | 16,384 calls | 3.289 ms | 0.965 ms | — | One candidate diagnostic on 015184e6; sync change within spread |
| Same helper calls [R], warm `RunAsync` | Same input; completed host operations | 5.378 ms | 1.001 ms | — | One positional-transport diagnostic on 1f2ef306; 13.7% lower Lython median; baseline spread 9.4% |
| Helper with nested filtered list comprehension [H] | 1,024 inputs / 1,364 results | 0.816–0.825 ms | 0.0793–0.0990 ms | — | Warm diagnostics at 3cad4157; both executable candidates rejected; CPython spread 23.7–38.2% |
| File search with helper and protected early exit [P], warm | 2,049 lines / 42,010 UTF-8 bytes | 4.249 ms | 0.265 ms | — | One candidate diagnostic; inconclusive (56.4% spread) |
| Same file search [P], fresh compile and run | Same input; stop after line 1,025 | 6.041 ms | 0.395 ms | — | Fresh code per job; inconclusive (42.4% spread); startup excluded |
| Complete file copy/checksum manifest [F] | 2 files / 4,107 bytes | 6.103–6.254 ms | 0.442 ms | — | One warm diagnostic; two identical Lython controls, up to15.1% spread |
| Complete Unicode text summary [T] | 6,144 words / 35,840 UTF-8 bytes | 8.018 ms | 1.258 ms | — | One warm diagnostic at ca432c22; noisy ASCII candidate rejected |
| Complete JSON-lines summary [J] | 4,096 records / 110,592 UTF-8 bytes | 11.537 ms | 4.225 ms | — | One warm candidate diagnostic; timing inconclusive (11.8% spread), allocation reduction measured |
| Complete CSV summary [C] | 4,096 records / 57,361 UTF-8 bytes | 7.530 ms | 3.483 ms | — | One warm diagnostic at f95fb734; inconclusive baseline spread (15.4%) |
| CSV parse and discard [G] | 16 data rows | 0.0780 ms | 0.0285 ms | — | One short governed-memory diagnostic; uncommitted source, unqualified |
| Sum generated squares [A] | 20,000 items | 3.947 ms | 0.499 ms | — | One short diagnostic at 09d81bb1 source; no qualified multiplier |
| Integer reduction loop [M] | 16,384 iterations | 1.162 ms | 0.617 ms | — | Short diagnostic at b43664e0; one run, no qualified multiplier |
| Integer reduction loop [L] | 16,384 iterations | 3.58–3.72 ms | 0.578–0.666 ms | — | Historical diagnostic; failed control and cross-session stability |
| Keyword calls [Q] | 2,048 calls | 1.378–1.404 ms | 0.137–0.145 ms | — | Historical diagnostic; failed invocation control |
| Stable sort and output [Q] | 2,048 items | 5.150–7.889 ms | 0.306–0.313 ms | — | Historical diagnostic; spread/order checks also failed |
| ASCII text pipeline [Q] | 1,024 eleven-character blocks | 0.618 ms | 0.0415 ms | — | Historical diagnostic; only one session before idle gate failed |
| JSON transform and round trip [Q] | 128 records | 0.674–0.741 ms | 0.0610–0.0618 ms | — | Historical diagnostic; control floor/spread/interval checks failed |
| CSV parse and retain [Q] | 128 data rows | 1.037–1.185 ms | 0.102–0.108 ms | — | Historical diagnostic; control/spread/order/interval checks failed |
| Other CSV summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other Unicode text summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other JSON-lines summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other file copy/checksum manifest sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |

**Ratio convention:** Lython elapsed time divided by CPython elapsed time;
above 1 means Lython takes longer. A dash means unavailable or unqualified,
never zero. The historical generated reports use the inverse convention;
their ratios must not be copied into this column. Ranges above are the smallest
and largest retained **session medians**, rounded for readability, not confidence
intervals. The ASCII row has just one retained median.

The historical measurements show substantial performance gaps on these jobs,
including a loop whose cost grows with its iteration count. They support
investigating interpreter dispatch and application workloads. **No workload
multiplier qualified in either campaign. These timings do not describe today's
HEAD:** subsequent interpreter and containment changes have been integrated.
The newer AL, AK, AJ, AI, AH, AG, AF, AE, AD, AC, AB, AA, Z, X, W, N, B, K, R, V, D, I, E, H, P, F, T, J, C, G, A and M rows are separate short diagnostics at their named revisions; they do
not qualify a current overall or complete-script multiplier.
The F, T, J and C rows supply complete-script comparisons; their timing evidence
remains inconclusive. F is a range of medians from two identical Lython control
processes, not two revisions. No complete-script multiplier or overall speedup is qualified.

## Which code and machine were measured?

| Label | Date | Measured Lython revision | Evidence |
| --- | --- | --- | --- |
| AI | 2026-10-15 | Working expanded set/dict candidate on28c41f1d; two frozen compiler sources; both roles contain entry-order fix | [Executable displays, mapping suspension and order qualification](benchmarks/results/2026-10-15-expanded-set-dict-executable/README.md) |
| AJ | 2026-10-15 | Working f-string candidate on 0c367762; one frozen compiler file, shared report fixture | [Executable fields, async protocols and ownership qualification](benchmarks/results/2026-10-15-executable-fstrings/README.md) |
| AK | 2026-10-15 | Working expanded-call candidate on 42dfd774; two frozen compiler files; both roles contain argument-order fix | [Executable expansion, argument effects and ownership qualification](benchmarks/results/2026-10-15-executable-expanded-calls/README.md) |
| AL | 2026-10-15 | Working lambda-body candidate on 24e6442a; eleven frozen production files; both roles share function runtime | [Executable bodies, deleted callable and scope/lifetime qualification](benchmarks/results/2026-10-15-executable-lambdas/README.md) |
| AH | 2026-10-15 | Working tuple-lowering candidate on5cf03da9; one frozen compiler source; both roles include empty-tuple fix | [Immutable conversion, admission and identity checks](benchmarks/results/2026-10-15-starred-tuple-executable/README.md) |
| AG | 2026-10-15 | Working starred-list lowering candidate on8f290b57; two frozen compiler sources; identical helper/tooling | [Executable displays, ordered effects and ownership checks](benchmarks/results/2026-10-15-starred-list-executable/README.md) |
| AF | 2026-10-15 | 654b4f5a; two identical Lython controls, freshly compiled configuration job | [Compile-to-result boundary, controls and independent file goldens](benchmarks/results/2026-10-15-config-fresh/README.md) |
| AE | 2026-10-15 | Working exact-integer dispatch candidate on 60277f65; one frozen interpreter source; unchanged case/tooling | [Async integer operators, suspension and boundary checks](benchmarks/results/2026-10-15-async-integer-operators/README.md) |
| AD | 2026-10-15 | Working exact-bytes sum candidate on 64fecd53; one frozen source; unchanged script/tooling | [Complete manifest job, exact sums and ownership/preflight checks](benchmarks/results/2026-10-15-bytes-sum/README.md) |
| AC | 2026-10-15 | Working JSON value-forwarding candidate on 50583327; one frozen production source; unchanged script/tooling | [Complete JSON file job and callback/ownership checks](benchmarks/results/2026-10-15-json-value-dispatch/README.md) |
| AB | 2026-10-15 | Working UTF-8 upper candidate on 29a1862e; one frozen source; identical new report script/catalog in both roles | [Complete report, noise and expansion/ownership checks](benchmarks/results/2026-10-15-utf8-upper/README.md) |
| AA | 2026-10-15 | Working UTF-8 lower candidate on d083c8cf; two frozen production files; unchanged script/tooling | [Complete text job, noise and allocation/containment checks](benchmarks/results/2026-10-15-utf8-lower/README.md) |
| Z | 2026-10-15 | Baseline 3b34fad2; ten-source compiled-layout candidate rejected and removed | [Fresh helper timing, rejection and retained cleanup](benchmarks/results/2026-10-15-parameter-layout-rejected/README.md) |
| X | 2026-10-15 | Working direct-strip/trim-identity candidate on 88e0992b; seven performance files frozen; parser fix shared by both roles | [Inconclusive text timing, allocation removal and Unicode diagnostics](benchmarks/results/2026-10-15-direct-string-strip/README.md) |
| W | 2026-10-15 | Working direct-get candidate on 7e2c1f27; seven production files frozen by hashes; identical new word-count case for both roles | [Direct lookup, callback lifetimes and short diagnostic](benchmarks/results/2026-10-15-direct-dict-get/README.md) |
| N | 2026-10-15 | Working direct-list-append candidate on 99e8701f; nine production files frozen by hashes; identical new distinct-list case for both roles | [Direct call transport, keyword correction and validation](benchmarks/results/2026-10-15-direct-append/README.md) |
| B | 2026-10-15 | Working matching-local-storage candidate on f93e9258; three production files frozen by hashes; identical new full-body case for both roles | [Local-array removal and lifetime validation](benchmarks/results/2026-10-15-bound-local-reuse/README.md) |
| K | 2026-10-15 | Working call-cache deletion on 6262ea94; eight production files frozen by hashes; comparison tooling unchanged | [Mechanism removal and keyword-call diagnostic](benchmarks/results/2026-10-15-call-cache-deletion/README.md) |
| R | 2026-10-15 | Working direct-positional candidate on 1f2ef306; eight production files frozen by hashes; comparison tooling unchanged | [Operand transport, boundary tests and short diagnostic](benchmarks/results/2026-10-15-positional-calls/README.md) |
| V | 2026-10-15 | Working live-defaults candidate on 96596466; thirteen production files frozen by hashes; one catalog case shared by both roles | [Live defaults, ownership and CPython lifetime edge](benchmarks/results/2026-10-15-function-defaults/README.md) |
| D | 2026-10-15 | Working method-exposure candidate on 0eb587a9; seven production files frozen by hashes | [Correct method identity and retained immediate-call cache](benchmarks/results/2026-10-15-method-exposure/README.md) |
| I | 2026-10-15 | Working async integer-addition candidate on 88f78106; two production files frozen by hashes | [Async loop diagnostic and yielding-protocol correction](benchmarks/results/2026-10-15-async-integer-dispatch/README.md) |
| E | 2026-10-15 | Working async-function candidate on 015184e6; six production files frozen by hashes | [Separate `Run` / `RunAsync` diagnostics and qualification](benchmarks/results/2026-10-15-async-compiled-functions/README.md) |
| H | 2026-10-15 | 3cad4157 existing runtime; both uncommitted executable-list candidates rejected | [Helper comparison and retained prototypes](benchmarks/results/2026-10-15-list-comprehensions-rejected/README.md) |
| P | 2026-10-15 | Working protected-loop candidate on b2235259; two frontend source changes frozen by hashes | [Separate warm and compile/run diagnostics](benchmarks/results/2026-10-15-protected-loop-jumps/README.md), [source declaration](benchmarks/results/2026-10-15-protected-loop-jumps/comparison-declaration.json) |
| F | 2026-10-14 | ca432c22, two identical worker/binary controls; runtime tree matches7736b6c0 | [Complete file diagnostic](benchmarks/results/2026-10-14-representative-file/README.md), [loaded identities](benchmarks/results/2026-10-14-representative-file/micro.json) |
| T | 2026-10-14 | ca432c22, governed runtime and worker ownership; ASCII candidate rejected | [Complete text diagnostic](benchmarks/results/2026-10-14-representative-text/README.md), [loaded identities](benchmarks/results/2026-10-14-representative-text/micro.json) |
| J | 2026-10-14 | Working direct-decoder candidate on 64ec1fd4, with identical f95fb734 worker | [Complete JSON diagnostic](benchmarks/results/2026-10-14-representative-json/README.md), [timing identities](benchmarks/results/2026-10-14-representative-json/micro.json) |
| C | 2026-10-14 | f95fb734, governed runtime and worker ownership; parser candidate rejected | [Complete CSV diagnostic](benchmarks/results/2026-10-14-representative-csv/README.md), [loaded identities](benchmarks/results/2026-10-14-representative-csv/micro.json) |
| G | 2026-10-14 | Working migration on `9fc7e37e`, before local-import simplification | [Governed-memory diagnostic](benchmarks/results/2026-10-14-governed-memory/README.md), [loaded identities](benchmarks/results/2026-10-14-governed-memory/micro.json) |
| A | 2026-10-14 | `09d81bb153c006f3c248d8ba0dfdfe9cdbc70d63` source, built before commit | [Native state qualification](benchmarks/results/2026-10-14-native-working-state/README.md), [all attempts and loaded identities](benchmarks/results/2026-10-14-native-working-state/micro.json) |
| M | 2026-10-13 | `b43664e0f60aa78f3cbd638578e0ced28d926194` | [Shared frames and transfer cost](benchmarks/results/2026-10-13-shared-async-frames/README.md), [all attempts](benchmarks/results/2026-10-13-shared-async-frames/micro.json) |
| Q | 2026-10-09 | `9358c4e72f1a29e55703eee02b28ea404d01bc0a` | [Four-lane run](benchmarks/results/2026-10-09-quick/README.md), [warm medians and exclusions](benchmarks/results/2026-10-09-quick/warm.md) |
| L | 2026-10-09 | `56d4346d960b58f037da7d4590eb66c87ffec29e` | [Focused loop run](benchmarks/results/2026-10-09-core-loop/README.md), [all loop samples](benchmarks/results/2026-10-09-core-loop/warm.md) |
| S | 2026-10-12 | `87c1a94c347a88bc94927ea603a138cec4376170` (correctness, no timings) | [Complete-script validation](benchmarks/results/2026-10-12-script-negative-cases/README.md), [fixture and host contract](benchmarks/results/2026-10-12-containment-scope/README.md) |

K, R, V, D, I, E, H and P used the same VM and SDK/runtime/CPython versions described below.
I measures warm completed-host `RunAsync` against ordinary warm CPython code.
E separates warm `Run` and completed-host `RunAsync`; CPython executes ordinary
warm code in both rows. H preserves both rejected candidates and their existing
runtime controls. P separates fresh compile/run from warm reuse and includes
complete file staging/output checks. Their candidate hashes identify measured
source, rather than an arbitrary later HEAD.

Q and L used the same dedicated **Azure Standard_F4as_v7**: Ubuntu 24.04.4 LTS
x86_64, four AMD EPYC 9V45 vCPUs, one thread per core, 16 GiB RAM, no swap.
Lython was a clean Release build using **.NET SDK 10.0.401 / runtime 10.0.12**.
The primary external reference was **CPython 3.13.16**, built with GCC 13.3,
PGO/LTO, ordinary GIL and cyclic GC, without debug mode or JIT, launched with
`-I -S`. Workers kept ordinary GC/tiering and public Lython limits; optional
instruction fuel was unset. Both engines ran sequentially on that machine.
The linked receipts identify binaries, inputs, toolchains and sampling policies.
A ran on the shared Windows 11 x64 development workstation: i7-14700KF,
28 logical processors, 64 GiB RAM; SDK 10.0.300-preview.0.26177.108,
.NET 10.0.13 and CPython 3.13.2, ordinary GIL/GC, launched with `-I -S`.
Its 7.893-second owned group uses the existing generated-sum workload with
shared printed output. The candidate library's informational metadata names
the preceding commit; frozen hashes identify the added sum implementation.
Compilation and startup are excluded. This diagnostic does not qualify a
CPython multiplier or an improvement over earlier Lython.
M ran on that dedicated four-vCPU Ubuntu VM under .NET 10.0.12 with the same
CPython 3.13.16 reference. Its portable Windows Release libraries were built
using SDK **10.0.300-preview.0.26177.108**; its identical benchmark observer and
DLL hashes are frozen separately. Seven rounds in one 8.24-second owned group
provide diagnostic context, with compilation/startup outside the warm timer.

S verifies exact stdout and output-file bytes through Lython's public API in
both synchronous and genuinely yielded asynchronous execution, plus independent
CPython 3.13.2 checks on Windows. Its fixture repeats expand into different record/byte counts for
each script; they are not a shared row count. Correctness tests are not timing
measurements. The [original scripts](benchmarks/scripts) remain ordinary Python.

G uses the same shared Windows workstation/toolchain as A and CPython3.13.2.
Its warm CSV comparison excludes compilation/startup and is a single7.320-second
diagnostic; later local-import changes are unmeasured.

## Execution, compilation and startup are separate

Except for the explicitly marked fresh compile/run rows Z and P, the leading table uses
**warm execution**: both engines reuse compiled code,
create fresh invocation state, execute the complete module, capture/project
output and perform the cheap golden check inside the worker timer. Compilation,
worker launch, transport and full output hashing are outside it. For complete
file-backed F/T/J/C jobs, file reset/staging and exact output-file byte checks are
inside both worker timers, as defined in [COMPARISON.md](benchmarks/COMPARISON.md).
Module imports
retain their source positions and normal caches; invocation controls are never
subtracted. Historical Q/L cases have in-memory fixtures, not disk file I/O.

Lython's host boundary, memory governor and cooperative checks remain enabled.
CPython has no equivalent in-process containment. These are application costs
under the two ordinary execution contracts; neither a bare arithmetic test nor
a claim that the contracts provide identical protection.

| Boundary | Historical observation at Q | Status |
| --- | --- | --- |
| Warm invocation | See workload table above | No qualified workload ratios |
| Compile and run | Compile source, then execute fresh state in persistent workers | Mostly incomplete after idle gates; [retained results](benchmarks/results/2026-10-09-quick/compile-run.md) |
| Compilation only | Lython engine compilation versus CPython `compile()` | Mostly incomplete after idle gates; [retained results](benchmarks/results/2026-10-09-quick/compile.md) |
| Fresh-process empty control | Lython 170.361–175.524 ms; CPython 29.628–29.947 ms | Startup control; [retained results](benchmarks/results/2026-10-09-quick/fresh-process.md) |

Fresh-process timing includes parent-observed launch, compilation, execution,
pipe drain and exit, with OS file caches retained. It must not be compared with
warm execution. Its controls qualified, but workloads failed the required
separation from startup; no workload ratio was published. Compilation samples
and startup costs must not be folded into the warm table.

## Improvements against earlier Lython revisions

These compare Lython with earlier Lython, **not with CPython**. Each uses short
diagnostic collections, ordinary protection/GC/tiering and unchanged worker
boundaries. They have no milestone qualification or claimed confidence interval.
The percentages must not be added or multiplied into a current CPython ratio.

| Change and date | Baseline → candidate revision | Observed Lython time reduction | Evidence |
| --- | --- | ---: | --- |
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

The next useful comparison is the four complete CSV, text, JSON and file scripts,
with identical input/output bytes and a declared file-host boundary. Their
correctness is established. The [file-worker delivery](benchmarks/results/2026-10-12-file-workers/README.md)
at `ed987087` verifies all 12 cases against CPython 3.13.2 on Windows, in persistent
and fresh processes, with matched disk staging/reset and output checks. Quiet-VM
timing and qualification remain pending. The older retained-CSV
job is not evidence for bounded streaming or host latency.

Process-wide forced GC remains a containment concern. The separate
[embedding-impact experiment](benchmarks/results/2026-10-12-embedding-impact/README.md)
records host request tails, heartbeat stalls and collection counts; fewer full
collections alone do not establish better host latency. A separate
[Windows observer pilot](benchmarks/results/2026-10-12-embedding-observer-v2/README.md)
passes its fixed setup/output gates for async CSV and file jobs; shared-machine
data does not qualify host latency.
The [Linux async comparison at `4cdfd6b9`](benchmarks/results/2026-10-12-embedding-async/README.md)
qualifies all four complete jobs at that revision within its finite host model, while GC-independent
accounting and broader host-impact qualification remain open. The
[Linux sync/retained-pressure model at `f67419a8`](benchmarks/results/2026-10-13-embedding-sync-pressure/README.md)
meets the same latency targets in 30 original samples: no Gen2 in the four complete
streaming jobs, one Gen2 per fresh retained-pressure execution. It is a finite
embedding model; Windows impact and GC-independent reclamation remain open.
The [bound/exposed/retained-error model at `7f5fbf70`](benchmarks/results/2026-10-13-embedding-exception-pressure/README.md)
also meets those latency targets in 24 original samples, but incurs 128 Gen2
collections per completed job versus one for unbound catches. These cases
complete 12–14 jobs per nominal four-second window versus 1,250–1,267 for unbound catches;
each window finishes its last complete job.
The report also records failed memory recovery after reference withdrawal.
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
multiplier. Complete ownership and forced-GC independence remain open.

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
