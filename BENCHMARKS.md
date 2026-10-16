# Lython performance against CPython

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| JSON configuration read/edit/write [AF], fresh compile/run | 16 existing paths / 181 UTF-8 input bytes | 2.817-3.142 ms | 1.572 ms | — | Two identical Lython controls differ by 11.5%; one diagnostic; no qualified ratio |
| Same JSON configuration job [AQ], warm `Run` | 16 existing paths / 181 UTF-8 input bytes | 3.309 ms | 1.695 ms | — | Short diagnostic at 4846928c; 0.9% change within spread; no improvement established |
| Complete CSV summary [C] | 4,096 records / 57,361 UTF-8 bytes | 7.530 ms | 3.483 ms | — | One warm diagnostic at f95fb734; inconclusive baseline spread (15.4%) |
| Complete text-summary file job [AN], warm | 3,072 lines / 35,840 UTF-8 bytes | 6.414 ms | 1.239 ms | — | Short diagnostic at 7caf4833; target-read consolidation retained; timing change inconclusive (baseline spread 12.4%) |
| Complete JSON-lines aggregation [AT], warm | 4,096 records / 110,592 UTF-8 bytes | 9.587 ms | 4.401 ms | — | Short diagnostic at dc950cde; 2.3% lower median within 17.6% baseline spread; inconclusive |
| Same JSON aggregation [AU], fresh compile/run | Same 4,096 records / 110,592 UTF-8 bytes | 9.880–10.024 ms | 4.359 ms | — | Two identical Lython controls; unqualified (21.3%/8.7% spread); startup excluded |
| Complete copy/checksum/manifest job [AD], warm | Two files / 4,107 input bytes | 6.080 ms | 0.4382 ms | — | Historical warm diagnostic; elapsed change within spread |
| File search with helper and protected early exit [AR], warm | 2,049 lines / 42,010 UTF-8 bytes | 3.223 ms | 0.2845 ms | — | Short diagnostic at 473aa396; inconclusive (57%/64% Lython spread); no improvement established |
| Same file search [P], fresh compile and run | Same input; stop after line 1,025 | 6.041 ms | 0.395 ms | — | Fresh code per job; inconclusive (42.4% spread); startup excluded |
| Complete uppercase-report file job [AB], warm | 1,024 lines / 23,552 UTF-8 bytes | 2.651 ms | 0.2444 ms | — | Historical warm diagnostic; elapsed change within spread |
| Branch/integer loop [AE], warm `RunAsync` | 16,384 iterations; modulo/comparison/add/subtract | 6.321 ms | 0.9599 ms | — | One warm async diagnostic; completed host operations; unqualified |
| Instance-method accumulator [AP], warm `Run` | 2,048 calls; construct, update field and print | 1.144 ms | 0.11515 ms | — | Short diagnostic at 58aece73; no improvement established; candidate median 2.8% higher |
| Generator-expression numeric reduction [AW], warm `Run` | 16,384 float terms / 73 UTF-8 source bytes | 4.252 ms | 0.5611 ms | — | Short diagnostic at fa2b72af; executable bodies; 1.8% change within spread, no speedup established |

These representative snapshots come from the named revisions below; they are
not measurements of today's HEAD. Inconclusive cases remain visible. See
[all diagnostic snapshots](benchmarks/EXPERIMENTS.md#diagnostic-comparisons-by-revision)
for the detailed history and [Lython improvements](#improvements-against-earlier-lython-revisions)
for changes against prior Lython versions.

**Ratio convention:** Lython elapsed time divided by CPython elapsed time;
above 1 means Lython takes longer. A dash means unavailable or unqualified,
never zero. No workload ratio is qualified here. AF and AU ranges contain the medians
of two identical Lython controls, not confidence intervals. Other entries show
one diagnostic median. The jobs show performance gaps in those runs; their
magnitudes and changes remain uncertain as marked.

## Which code and machine were measured?

The leading rows and historical Q/L campaigns used the same dedicated **Azure Standard_F4as_v7**: Ubuntu 24.04.4 LTS
x86_64, four AMD EPYC 9V45 vCPUs, one thread per core, 16 GiB RAM, no swap.
Lython was a clean Release build using **.NET SDK 10.0.401 / runtime 10.0.12**.
The primary external reference was **CPython 3.13.16**, built with GCC 13.3,
PGO/LTO, ordinary GIL and cyclic GC, without debug mode or JIT, launched with
`-I -S`. Workers kept ordinary GC/tiering and public Lython limits; optional
instruction fuel was unset. Both engines ran sequentially on that machine.
The linked receipts identify binaries, inputs, toolchains and sampling policies.

| Label | Date | Measured Lython revision | Evidence |
| --- | --- | --- | --- |
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

Except for the explicitly marked fresh compile/run rows AF, AU and P, the leading table uses
**warm execution**: both engines reuse compiled code,
create fresh invocation state, execute the complete module, capture/project
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
