# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.971–6.085 ms | 4.446 ms | 1.34–1.37× | Diagnostic; 1.91% / 2.33% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.954–3.983 ms | 3.398 ms | 1.16–1.17× | Diagnostic; 0.74% / 1.31% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.027–2.092 ms | 1.282 ms | 1.58–1.63× | Variable; 3.20% / 0.79% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.293–4.315 ms | 4.256 ms | 1.01–1.01× | Diagnostic; 0.51% / 1.05% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.530–3.688 ms | 2.478 ms | 1.42–1.49× | Variable; 4.31% / 3.18% |
| Configuration read/edit/write | 16 paths / 181 B | 142.254–142.958 µs | 124.868 µs | 1.14–1.14× | Variable; 0.49% / 3.11% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 298.866–300.182 µs | 211.686 µs | 1.41–1.42× | Diagnostic; 0.44% / 1.48% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 384.468–387.899 µs | 260.080 µs | 1.48–1.49× | Diagnostic; 0.88% / 1.27% |

**2026-11-08; measured source `91f15a3e`, CPython 3.13.16, .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete output validation and prior-result cleanup.
Startup and fixture staging/reset are excluded. **Ratio = Lython elapsed /
CPython elapsed; above one means slower.** Ranges span two independent Lython
process medians, not confidence intervals. Control is their absolute difference
relative to the candidate. Max IQR includes CPython. Diagnostic/Variable describe
short observations, not design acceptance thresholds; inconclusive cases remain.

Frozen core `4c072fed` matches the measured source's production inventory;
informational SourceLink is precommit `71445454`. Windows/Ubuntu Release actual
.NET11/.NET10, original Stress and installed core consumers pass
[cached CI42451722109](../benchmarks/results/2026-11-08-prepared-consumers/delivery-ci.json).
The benchmark and source qualification are separate evidence.
The [26-job checkpoint](../benchmarks/results/2026-11-08-runtime-checkpoint/README.md)
records exact identities and verified raw evidence. Optional0.4 adapter/worker
unchanged; no newer producer is certified. The [earlier measured-source38f53c33
guide](../benchmarks/results/2026-11-08-runtime-checkpoint/prior-BENCHMARKS.md)
preserves the previous comparison, including numeric-string warm cases.
These tables describe the recorded source, not later HEAD.

## Warm execution, compilation and startup

Warm execution reuses the compiled script. The last two rows use rsplit and a
whitespace guard in small helpers on the same text fixture. Their compilation
and compile-to-result times were not measured in this checkpoint.

The ordinary helper cases remain on the executable engine. Their extra elapsed
time does not, by itself, separate call/frame costs from string processing.

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.726–5.887 ms | 4.327 ms | 1.32–1.36× | Variable; 2.72% / 0.52% |
| CSV summary/ordinary helper | 3.835–3.928 ms | 3.249 ms | 1.18–1.21× | Variable; 2.43% / 0.72% |
| Text summary/helpers | 1.887–1.961 ms | 1.203 ms | 1.57–1.63× | Variable; 3.92% / 2.14% |
| JSON-lines/helpers | 3.988–4.074 ms | 4.067 ms | 0.98–1.00× | Variable; 2.11% / 0.69% |
| JSON-to-Markdown report | 3.510–3.616 ms | 2.335 ms | 1.50–1.55× | Variable; 2.92% / 4.22% |
| Configuration read/edit/write | 79.865–79.924 µs | 62.897 µs | 1.27–1.27× | Diagnostic; 0.07% / 1.81% |
| Protected file search | 226.616–227.012 µs | 139.601 µs | 1.62–1.63× | Diagnostic; 0.17% / 1.14% |
| Two-file copy/checksum/manifest | 242.192–243.121 µs | 155.211 µs | 1.56–1.57× | Diagnostic; 0.38% / 1.42% |
| Text summary/rsplit helper | 2.527–2.583 ms | 1.160 ms | 2.18–2.23× | Variable; 2.19% / 0.99% |
| Text summary/guarded helper | 2.546–2.594 ms | 1.201 ms | 2.12–2.16× | Diagnostic; 1.84% / 1.06% |

Compilation alone includes construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 70.268–71.335 µs | 65.395 µs | 1.07–1.09× | Diagnostic; 1.50% / 2.06% |
| CSV summary/ordinary helper | 61.180–61.386 µs | 59.353 µs | 1.03–1.03× | Diagnostic; 0.34% / 0.91% |
| Text summary/helpers | 44.740–45.281 µs | 41.415 µs | 1.08–1.09× | Diagnostic; 1.21% / 0.78% |
| JSON-lines/helpers | 51.942–52.730 µs | 50.842 µs | 1.02–1.04× | Diagnostic; 1.50% / 0.82% |
| JSON-to-Markdown report | 41.648–41.705 µs | 37.884 µs | 1.10–1.10× | Diagnostic; 0.14% / 1.54% |
| Configuration read/edit/write | 38.453–38.581 µs | 34.349 µs | 1.12–1.12× | Diagnostic; 0.33% / 1.62% |
| Protected file search | 29.125–29.228 µs | 30.886 µs | 0.94–0.95× | Diagnostic; 0.35% / 1.37% |
| Two-file copy/checksum/manifest | 71.368–72.327 µs | 56.088 µs | 1.27–1.29× | Diagnostic; 1.34% / 0.90% |

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** A combined checkpoint does not isolate each change's CPU benefit,
repay historical regression debt or explain earlier adverse controls. It describes
the recorded source, not future HEAD. Search exits after 1,025 of 2,049 lines; the
manifest copies only two files/4,107 B. These are not large-file throughput tests.

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Sequential independent processes, normal CLR GC/tiering,
quiet endpoints; five-second warmup per role/job, seven rotated150ms rounds.
All26 jobs completed in539.12seconds. Returned-record CSV parses5,120 rows and
accepts3,072; search exits after1,025 of2,049 lines, manifest copies two files.
These small maintained scripts do not establish large-file throughput.

Identical scripts/fixtures run on both engines; complete stdout/file digests are
checked every invocation. Lython uses efficient mediated filesystem overrides;
CPython uses builtin open. Timed Lython opens include capability/path-component
checks. RAM fixtures include guest reads/writes; fixture staging/reset are outside
clocks. Warm includes invocation and prior-result cleanup. Compilation includes
construction/disposal; compile-to-result adds it to execution. Protocol transport
and final cached teardown are excluded. CPython retains imported modules; Lython
isolates execution imports. This is an embedding comparison, not an isolated CPU
kernel. No wall-I/O decomposition, startup, heap/RSS or embedding-tail measurement.
Tables use synchronous Run; historical async timings use completed mediated I/O
and do not establish genuinely pending-I/O latency. All workers joined, fixtures
were removed and owned RAM unmounted before archival under lease/local verification.

## Improvements within Lython

CPython is the primary external baseline above. These separate source pairs
measure affected workloads; percentages are not additive or universal CPU gains.

| Change / affected work | Established result | Limits / evidence |
| --- | --- | --- |
| Native workstate dispatch | Ordinary/guarded helper reports allocate about 42% less; controlled elapsed 11.8–14.4% lower sync/14.5–16.1% async; guarded 10.5–12.2% lower | Native calls/iteration avoid full contexts. CSV elapsed +0.25–1.92% accepted debt; native control inconclusive. [R79](../benchmarks/results/2026-11-08-native-workstate/README.md) |
| Shared string receiver declarations | Complete rsplit-helper report allocation −21.26%; controlled migration elapsed −14.5–16.8% sync/−14.3–14.7% async | Two providers/per-access closures/packing removed; callbacks genuinely suspend. Final duplicate-arm cleanup allocation-checked, not VM-retimed; report control inconclusive. [R78](../benchmarks/results/2026-11-08-string-receivers/README.md) |
| Direct token storage / text, CSV | Allocation about−9.4% text/−8–9% CSV; controlled async text elapsed−2.3–3.3% | Sync text inconclusive; float async+0.9–2.7% accepted debt, full delivery pending. [R76](../benchmarks/results/2026-11-08-inline-utf8/README.md) |
| Native function returns / CSV, JSON | Allocation −19.92%/−19.31% | Elapsed remains diagnostic. [Source pair](../benchmarks/results/2026-11-02-native-function-returns/README.md) |
| Direct UTF-8 formatting | Allocation −8.67% on original source; controlled elapsed/CPU −5–8% | Original adverse elapsed result remains unexplained. [Confirmation](../benchmarks/results/2026-11-02-formatting-confirmation/README.md) |
| Completed CSV records | Allocation −7.31%/−6.55%; native-helper warm elapsed −7.72–8.04% | Controls mixed; independently mutable rows remain funded. [R25](../benchmarks/results/2026-11-07-csv-record-values/README.md) |
| Canonical formatted instructions / dynamic report | Allocation about −15.35%; warm elapsed −10–13% | CSV +0.13–2.36% possible debt. [R35](../benchmarks/results/2026-11-07-canonical-format-instructions/README.md) |
| Shared receiver methods / regex summary | Allocation about −29.47%; warm elapsed −8.64–10.80% | CSV/text/JSON controls variable; original adverse CSV retained. [R41](../benchmarks/results/2026-11-07-receiver-methods/README.md) |
| Validated JSON floating conversion / numeric report | Allocation neutral; controlled warm elapsed −11.40–12.03% sync/−10.05–14.08% async | Integer/text controls near baseline; old inconclusive and adverse desktop retained; source33b6c191 fully qualified CI42431587731. [R74](../benchmarks/results/2026-11-08-json-float-entry/README.md) |
| General UTF-8 integer parsing | Padded CSV warm −12.5–13.4%; numeric-string JSON −41.4–42.0% | Explicit affected inputs; ordinary controls mixed. [R45](../benchmarks/results/2026-11-07-integer-parsing/README.md) |
| Native integer width / numeric-string JSON | Allocation about −12.5%; warm elapsed −10.7–12.5% | Padded CSV +1.0–1.2% accepted debt. [R50](../benchmarks/results/2026-11-07-native-integer-width/README.md) |
| Native UTF-8 wildcard reports | Allocation −25%/−32%; fnmatch warm −12–13%, glob −16–17% | Ordinary JSON control +4–7% accepted unresolved debt; no matching-backend claim. [R51](../benchmarks/results/2026-11-07-wildcard-translation/README.md) |
| Immutable tuple unpacking / returned-record CSV | Allocation −13.57%sync/−14.40%async; controlled warm elapsed −6.45–7.45%sync/−7.01–9.34%async | Ordinary controls near baseline; adverse desktop text preserved; full delivery qualified. [R70](../benchmarks/results/2026-11-07-tuple-unpack/README.md) |
| Linear keyword/variadic calls / returned-record CSV | Allocation −13.5% sync/−12.6% async; corrected controlled sync flat, async elapsed −1.21–6.31% | Original adverse CSV preserved; after transport correction CSV −1.74 to +0.43%, coarse CPU flat. Full correction delivery qualified. [R71](../benchmarks/results/2026-11-08-call-transport/README.md) |
| Intrinsic sequence construction / returned-record CSV | Further allocation −23.48% sync/−21.69% async; controlled elapsed −1.92–4.79% sync/−2.76–6.49% async | Ordinary CSV near baseline, text mixed/coarse CPU adverse; combined delivery qualified. [R72](../benchmarks/results/2026-11-08-sequence-construction/README.md) |
| Shared buffered-line execution | Duplicate algorithms/state machines removed; controlled async text elapsed −2.32–8.26%, JSON −1.46–3.96% | Allocation neutral; variation/quantized CPU, sync controls mixed. [R66](../benchmarks/results/2026-11-07-buffered-lines/README.md) |
| Frontend validity/module-fact pruning | Compile allocation −3.15–4.07% then −3.53–4.64% on separate source pairs | R62 controlled compile elapsed −2.94–5.88%; R65 elapsed inconclusive. [R62](../benchmarks/results/2026-11-07-syntax-validation/README.md), [R65](../benchmarks/results/2026-11-07-module-class-facts/README.md) |
| Executable graph construction | Compile allocation −3.9–5.9%; controlled compilation elapsed −1.7–3.0% | Warm control inconclusive; process CPU mixed. [R58](../benchmarks/results/2026-11-07-executable-graph/README.md) |

[Prepared member/iterator/branch operands](../benchmarks/results/2026-11-08-prepared-consumers/README.md)
also remove emitted load dispatch with neutral allocation. Controlled text elapsed
is modestly lower; coarse CPU is mixed, report timing inconclusive, and possible
CSV elapsed regression up to1.50% remains explicit. This is implementation
consolidation, not demonstrated substantial CPU improvement.

[Canonical receiver declarations](../benchmarks/results/2026-11-08-receiver-contract/README.md)
remove separate callable forms and a special opcode. Complete casefold/title helper
reports allocate45–52%less, with controlled elapsed22–24%lower. Rsplit control is
inconclusive; possible CSV regression up to2.70%remains explicit. This source pair
does not update the named-source CPython tables above.

Shared compiler-fact correction, annotation demand pruning and smaller executable
transport are also retained; their compilation/source benefits do not prove broad
warm CPU gains. [Earlier source pairs](../benchmarks/results/2026-11-07-retained-checkpoint/earlier-source-pairs.md)
preserve detailed revisions, regressions and completed qualification. The original
failed B406 refresh and its partial data remain beside the corrected checkpoint;
it was not silently discarded or promoted.

[Experimental history](../benchmarks/EXPERIMENTS.md) retains rejected designs,
adverse timings and allocation evidence. [Benchmark tooling](../benchmarks/README.md)
describes qualification procedures; [tests](../tests/README.md) cover semantics,
ownership, cancellation and installed consumers. Timing does not prove containment.
