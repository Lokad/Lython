# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.547–5.551 ms | 4.454 ms | 1.25–1.25× | Diagnostic; 0.06% / 1.00% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.570–3.633 ms | 3.418 ms | 1.04–1.06× | Diagnostic; 1.73% / 0.65% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.937–1.944 ms | 1.280 ms | 1.51–1.52× | Diagnostic; 0.38% / 0.74% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.937–3.951 ms | 4.237 ms | 0.93–0.93× | Diagnostic; 0.36% / 0.56% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.433–3.539 ms | 2.489 ms | 1.38–1.42× | Variable; 3.01% / 2.84% |
| Configuration read/edit/write | 16 paths / 181 B | 141.705–142.868 µs | 122.668 µs | 1.16–1.16× | Diagnostic; 0.82% / 2.92% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 296.982–299.900 µs | 199.115 µs | 1.49–1.51× | Diagnostic; 0.97% / 1.77% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 384.150–385.131 µs | 260.100 µs | 1.48–1.48× | Diagnostic; 0.26% / 1.43% |

**2026-11-08; measured source `7265538c`, CPython 3.13.16, .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete output validation and prior-result cleanup.
Startup and fixture staging/reset are excluded. **Ratio = Lython elapsed /
CPython elapsed; above one means slower.** Ranges span two independent Lython
process medians, not confidence intervals. Control is their absolute difference
relative to the candidate. Max IQR includes CPython. Diagnostic/Variable describe
short observations, not design acceptance thresholds; inconclusive cases remain.

Frozen core `1aa68ce9` matches the measured source's production inventory.
Windows/Ubuntu Release actual .NET11/.NET10, original Stress and installed core
consumers pass [cached CI42496463028](../benchmarks/results/2026-11-08-native-conditional-helpers-final/delivery-ci.json).
Source qualification and measurement are separate evidence.
The [28-job checkpoint](../benchmarks/results/2026-11-08-json-conditional-checkpoint/README.md)
records exact identities and verified raw observations. Optional0.4 adapter/worker
unchanged; no newer producer is certified. The [earlier source15c06863 guide](../benchmarks/results/2026-11-08-json-conditional-checkpoint/prior-BENCHMARKS.md)
preserves previous tables and inconclusive cases; older named measurements remain
linked there. These tables describe the recorded source, not a future HEAD.

## Warm execution, compilation and startup

Warm execution reuses the compiled script. The rsplit and guarded helper rows
share the text fixture. JSON normalization edits every record, creates nested
values and writes the complete JSON-lines file; eager and streamed outputs match.
Compilation and compile-to-result for these four additional rows are unmeasured.

The ordinary helper cases remain on the executable engine. Their extra elapsed
time does not, by itself, separate call/frame costs from string processing.

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.314–5.359 ms | 4.370 ms | 1.22–1.23× | Diagnostic; 0.83% / 0.85% |
| CSV summary/ordinary helper | 3.530–3.543 ms | 3.184 ms | 1.11–1.11× | Diagnostic; 0.36% / 1.88% |
| Text summary/helpers | 1.810–1.898 ms | 1.169 ms | 1.55–1.62× | Variable; 4.85% / 0.54% |
| JSON-lines/helpers | 3.776–3.815 ms | 4.103 ms | 0.92–0.93× | Diagnostic; 1.03% / 1.00% |
| JSON-to-Markdown report | 3.335–3.520 ms | 2.329 ms | 1.43–1.51× | Variable; 5.55% / 2.52% |
| Configuration read/edit/write | 78.349–78.497 µs | 61.938 µs | 1.26–1.27× | Diagnostic; 0.19% / 2.88% |
| Protected file search | 227.063–228.463 µs | 135.112 µs | 1.68–1.69× | Diagnostic; 0.61% / 2.78% |
| Two-file copy/checksum/manifest | 241.782–242.153 µs | 156.779 µs | 1.54–1.54× | Diagnostic; 0.15% / 1.54% |
| Text summary/rsplit helper | 1.827–1.876 ms | 1.181 ms | 1.55–1.59× | Variable; 2.70% / 0.84% |
| Text summary/guarded helper | 2.079–2.106 ms | 1.182 ms | 1.76–1.78× | Diagnostic; 1.33% / 1.05% |
| JSON normalization/eager output | 16.462–16.721 ms | 13.384 ms | 1.23–1.25× | Diagnostic; 1.58% / 2.09% |
| JSON normalization/streamed output | 17.061–17.136 ms | 27.784 ms | 0.61–0.62× | Diagnostic; 0.44% / 2.65% |

Compilation alone includes construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 72.002–72.299 µs | 65.506 µs | 1.10–1.10× | Diagnostic; 0.41% / 0.96% |
| CSV summary/ordinary helper | 61.390–61.473 µs | 59.259 µs | 1.04–1.04× | Diagnostic; 0.13% / 0.85% |
| Text summary/helpers | 44.963–45.409 µs | 41.244 µs | 1.09–1.10× | Diagnostic; 0.99% / 1.12% |
| JSON-lines/helpers | 52.781–53.146 µs | 51.136 µs | 1.03–1.04× | Diagnostic; 0.69% / 0.53% |
| JSON-to-Markdown report | 42.538–42.996 µs | 37.917 µs | 1.12–1.13× | Diagnostic; 1.07% / 1.47% |
| Configuration read/edit/write | 38.855–38.904 µs | 34.574 µs | 1.12–1.13× | Diagnostic; 0.13% / 1.81% |
| Protected file search | 29.648–29.656 µs | 30.872 µs | 0.96–0.96× | Diagnostic; 0.03% / 0.81% |
| Two-file copy/checksum/manifest | 73.293–74.183 µs | 56.767 µs | 1.29–1.31× | Diagnostic; 1.20% / 0.90% |

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
All28 jobs completed in581.73seconds. Returned-record CSV parses5,120 rows and
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
| Shared closed-method preparation / text helpers | Allocation neutral; controlled rsplit elapsed −13.94–17.85% sync/−16.06–19.25% async, casefold sync −11.14–14.17% | Duplicate method inventory/dead fusion work removed. Fused/report controls inconclusive; CSV +0.42–1.03% possible debt. Full delivery qualified. [R89](../benchmarks/results/2026-11-08-native-method-facts/README.md) |
| Shared casing/split preparation / guarded text | Allocation −12.74%; controlled elapsed −3.48–4.84% sync/−6.78–8.01% async; coarse CPU agrees | Native-only fused variants removed. Other text/CSV controls inconclusive; frontend elapsed diagnostic. Full delivery qualified at05d81b83. [R92](../benchmarks/results/2026-11-08-shared-cased-split/README.md) |
| Direct comparison conditions / CSV and JSON | CSV allocation −3.72% sync/−15.25% async; controlled CSV elapsed −5–9% sync/−10–16% async, JSON −6–8% | Ordered predicate/name transport and primitive fallback removed. Text +1.28–3.50% elapsed debt retained; async CSV controls variable. Full delivery qualified atcb2b015e. [R95](../benchmarks/results/2026-11-08-direct-comparison-branches/README.md) |
| Direct operand references / complete scripts | Allocation neutral; controlled CSV elapsed −1.4–4.3%, JSON −1.7–4.3%; text favorable with variable baseline | Operand bundle/reader removed. Report possible elapsed debt; R95 text debt remains separately recorded. Full delivery qualified at94cb8963. [R96](../benchmarks/results/2026-11-08-direct-operand-transport/README.md) |
| Known-layout record value growth / wide JSON report | Allocation −8.6% both APIs; controlled elapsed −4–10% sync/−6–8% async, coarse CPU supports | New 24-field fixtures; alternating records neutral allocation, ordinary controls uncertain. Full qualification pending. [R110](../benchmarks/results/2026-11-08-json-record-capacity/README.md) |
| Native acyclic helper returns / guarded text | Allocation neutral; controlled elapsed −13–17% across both APIs, coarse CPU supports the gain | One existing return proof; initial CSV regression did not recur after construction refinement, cause unresolved. Windows compilation uncertain; source7265538c fully qualified. [R106](../benchmarks/results/2026-11-08-native-conditional-helpers-final/README.md) |
| Native JSON constructor/call transport / normalization | Eager allocation −27–28%, elapsed −9–12%; streamed allocation −24%, elapsed −6–7%; coarse CPU agrees | One initializer, native keyword/method/value packaging removed. Config CPU adverse/inconclusive; sourcea7bd4103 fully qualified. [R105](../benchmarks/results/2026-11-08-native-json-construction/README.md) |
| Unified JSON encoder / normalization | Eager allocation +4.9%, elapsed +3–5%; streamed elapsed −2–6% with mixed coarse CPU | Duplicate renderer removed; eager regression accepted explicitly. Controls variable; source15c06863 fully qualified. [R102](../benchmarks/results/2026-11-08-unified-json-encoding/README.md) |
| Shared default-rsplit preparation / text reports | Lower allocation −16.46%, upper −11.88%; controlled lower elapsed −14–17% sync/about−13% async, upper −8–12%; coarse CPU agrees | Existing kernel reused; no new runtime mechanism. Report control inconclusive; full delivery qualified atc74c5802. [R99](../benchmarks/results/2026-11-08-default-rsplit-preparation/README.md) |
| Shared split construction / delimiter reports | Complete forward report allocation −5.47%, reverse −8.91–8.92% across both APIs | Temporary lists/conversion/copies removed; whole output admitted before fragments. Desktop elapsed inconclusive/adverse; full delivery qualified atcbe88fef. [R98](../benchmarks/results/2026-11-08-shared-split-construction/README.md) |
| Native workstate dispatch | Ordinary/guarded helper reports allocate about 42% less; controlled elapsed 11.8–14.4% lower sync/14.5–16.1% async; guarded 10.5–12.2% lower | Native calls/iteration avoid full contexts. CSV elapsed +0.25–1.92% accepted debt; native control inconclusive. [R79](../benchmarks/results/2026-11-08-native-workstate/README.md) |
| Shared string receiver declarations | Complete rsplit-helper report allocation −21.26%; controlled migration elapsed −14.5–16.8% sync/−14.3–14.7% async | Two providers/per-access closures/packing removed; callbacks genuinely suspend. Final duplicate-arm cleanup allocation-checked, not VM-retimed; report control inconclusive. [R78](../benchmarks/results/2026-11-08-string-receivers/README.md) |
| Direct token storage / text, CSV | Allocation about−9.4% text/−8–9% CSV; controlled async text elapsed−2.3–3.3% | Sync text inconclusive; float async+0.9–2.7% accepted debt, full delivery qualified. [R76](../benchmarks/results/2026-11-08-inline-utf8/README.md) |
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
