# Historical guide: primary source15c06863

These tables describe source15c before the combined7265538c checkpoint. Separate later-source improvement rows are identified.

# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.516–5.581 ms | 4.465 ms | 1.24–1.25× | Diagnostic; 1.19% / 0.82% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.611–3.651 ms | 3.395 ms | 1.06–1.08× | Diagnostic; 1.11% / 1.06% |
| Text summary/helpers | 3,072 lines / 35,840 B | 1.979–2.001 ms | 1.293 ms | 1.53–1.55× | Variable; 1.12% / 3.08% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 3.998–4.007 ms | 4.202 ms | 0.95–0.95× | Diagnostic; 0.22% / 0.93% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.498–3.552 ms | 2.511 ms | 1.39–1.41× | Variable; 1.55% / 4.55% |
| Configuration read/edit/write | 16 paths / 181 B | 141.382–144.008 µs | 123.058 µs | 1.15–1.17× | Diagnostic; 1.86% / 2.81% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 294.135–298.576 µs | 197.468 µs | 1.49–1.51× | Diagnostic; 1.51% / 1.12% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 385.993–387.224 µs | 263.334 µs | 1.47–1.47× | Diagnostic; 0.32% / 1.53% |

**2026-11-08; measured source `15c06863`, CPython 3.13.16, .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete output validation and prior-result cleanup.
Startup and fixture staging/reset are excluded. **Ratio = Lython elapsed /
CPython elapsed; above one means slower.** Ranges span two independent Lython
process medians, not confidence intervals. Control is their absolute difference
relative to the candidate. Max IQR includes CPython. Diagnostic/Variable describe
short observations, not design acceptance thresholds; inconclusive cases remain.

Frozen core `1674a06c` matches the measured source's production inventory.
Windows/Ubuntu Release actual .NET11/.NET10, original Stress and installed core
consumers pass [cached CI42484943603](../2026-11-08-unified-json-encoding/delivery-ci.json).
Source qualification and measurement are separate evidence.
The [28-job checkpoint](../2026-11-08-unified-json-checkpoint/README.md)
records exact identities and verified raw observations. Optional0.4 adapter/worker
unchanged; no newer producer is certified. The [earlier source94cb8963 guide](../2026-11-08-unified-json-checkpoint/prior-BENCHMARKS.md)
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
| CSV validation/returned records | 5.296–5.345 ms | 4.280 ms | 1.24–1.25× | Diagnostic; 0.93% / 0.94% |
| CSV summary/ordinary helper | 3.447–3.469 ms | 3.214 ms | 1.07–1.08× | Diagnostic; 0.62% / 0.69% |
| Text summary/helpers | 1.851–1.862 ms | 1.165 ms | 1.59–1.60× | Diagnostic; 0.57% / 1.58% |
| JSON-lines/helpers | 3.713–3.754 ms | 4.061 ms | 0.91–0.92× | Diagnostic; 1.10% / 1.31% |
| JSON-to-Markdown report | 3.420–3.500 ms | 2.342 ms | 1.46–1.49× | Variable; 2.27% / 3.02% |
| Configuration read/edit/write | 79.112–80.161 µs | 61.591 µs | 1.28–1.30× | Variable; 1.33% / 3.46% |
| Protected file search | 225.854–229.205 µs | 141.571 µs | 1.60–1.62× | Diagnostic; 1.48% / 1.76% |
| Two-file copy/checksum/manifest | 241.421–243.112 µs | 155.219 µs | 1.56–1.57× | Diagnostic; 0.70% / 1.86% |
| Text summary/rsplit helper | 1.836–1.866 ms | 1.158 ms | 1.59–1.61× | Diagnostic; 1.63% / 1.11% |
| Text summary/guarded helper | 2.349–2.390 ms | 1.186 ms | 1.98–2.02× | Diagnostic; 1.74% / 0.95% |
| JSON normalization/eager output | 18.373–18.568 ms | 12.928 ms | 1.42–1.44× | Diagnostic; 1.05% / 1.64% |
| JSON normalization/streamed output | 18.432–18.474 ms | 27.536 ms | 0.67–0.67× | Diagnostic; 0.22% / 1.30% |

Compilation alone includes construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 70.423–72.024 µs | 65.501 µs | 1.08–1.10× | Variable; 2.22% / 1.28% |
| CSV summary/ordinary helper | 61.096–61.127 µs | 59.019 µs | 1.04–1.04× | Diagnostic; 0.05% / 1.72% |
| Text summary/helpers | 45.372–45.606 µs | 41.405 µs | 1.10–1.10× | Diagnostic; 0.52% / 1.12% |
| JSON-lines/helpers | 53.068–53.626 µs | 51.036 µs | 1.04–1.05× | Diagnostic; 1.04% / 1.67% |
| JSON-to-Markdown report | 42.128–42.225 µs | 37.903 µs | 1.11–1.11× | Diagnostic; 0.23% / 1.56% |
| Configuration read/edit/write | 38.481–38.681 µs | 34.764 µs | 1.11–1.11× | Diagnostic; 0.52% / 1.27% |
| Protected file search | 29.135–29.404 µs | 30.928 µs | 0.94–0.95× | Diagnostic; 0.92% / 0.97% |
| Two-file copy/checksum/manifest | 73.107–73.382 µs | 56.398 µs | 1.30–1.30× | Diagnostic; 0.38% / 2.29% |

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
All28 jobs completed in581.94seconds. Returned-record CSV parses5,120 rows and
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
| Shared closed-method preparation / text helpers | Allocation neutral; controlled rsplit elapsed −13.94–17.85% sync/−16.06–19.25% async, casefold sync −11.14–14.17% | Duplicate method inventory/dead fusion work removed. Fused/report controls inconclusive; CSV +0.42–1.03% possible debt. Full delivery qualified. [R89](../2026-11-08-native-method-facts/README.md) |
| Shared casing/split preparation / guarded text | Allocation −12.74%; controlled elapsed −3.48–4.84% sync/−6.78–8.01% async; coarse CPU agrees | Native-only fused variants removed. Other text/CSV controls inconclusive; frontend elapsed diagnostic. Full delivery qualified at05d81b83. [R92](../2026-11-08-shared-cased-split/README.md) |
| Direct comparison conditions / CSV and JSON | CSV allocation −3.72% sync/−15.25% async; controlled CSV elapsed −5–9% sync/−10–16% async, JSON −6–8% | Ordered predicate/name transport and primitive fallback removed. Text +1.28–3.50% elapsed debt retained; async CSV controls variable. Full delivery qualified atcb2b015e. [R95](../2026-11-08-direct-comparison-branches/README.md) |
| Direct operand references / complete scripts | Allocation neutral; controlled CSV elapsed −1.4–4.3%, JSON −1.7–4.3%; text favorable with variable baseline | Operand bundle/reader removed. Report possible elapsed debt; R95 text debt remains separately recorded. Full delivery qualified at94cb8963. [R96](../2026-11-08-direct-operand-transport/README.md) |
| Native acyclic helper returns / guarded text | Allocation neutral; controlled elapsed −13–17% across both APIs, coarse CPU supports the gain | One existing return proof; initial CSV regression did not recur after construction refinement, cause unresolved. Windows compilation uncertain; full qualification pending. [R106](../2026-11-08-native-conditional-helpers-final/README.md) |
| Native JSON constructor/call transport / normalization | Eager allocation −27–28%, elapsed −9–12%; streamed allocation −24%, elapsed −6–7%; coarse CPU agrees | One initializer, native keyword/method/value packaging removed. Config CPU adverse/inconclusive; sourcea7bd4103 fully qualified. [R105](../2026-11-08-native-json-construction/README.md) |
| Unified JSON encoder / normalization | Eager allocation +4.9%, elapsed +3–5%; streamed elapsed −2–6% with mixed coarse CPU | Duplicate renderer removed; eager regression accepted explicitly. Controls variable; source15c06863 fully qualified. [R102](../2026-11-08-unified-json-encoding/README.md) |
| Shared default-rsplit preparation / text reports | Lower allocation −16.46%, upper −11.88%; controlled lower elapsed −14–17% sync/about−13% async, upper −8–12%; coarse CPU agrees | Existing kernel reused; no new runtime mechanism. Report control inconclusive; full delivery qualified atc74c5802. [R99](../2026-11-08-default-rsplit-preparation/README.md) |
| Shared split construction / delimiter reports | Complete forward report allocation −5.47%, reverse −8.91–8.92% across both APIs | Temporary lists/conversion/copies removed; whole output admitted before fragments. Desktop elapsed inconclusive/adverse; full delivery qualified atcbe88fef. [R98](../2026-11-08-shared-split-construction/README.md) |
| Native workstate dispatch | Ordinary/guarded helper reports allocate about 42% less; controlled elapsed 11.8–14.4% lower sync/14.5–16.1% async; guarded 10.5–12.2% lower | Native calls/iteration avoid full contexts. CSV elapsed +0.25–1.92% accepted debt; native control inconclusive. [R79](../2026-11-08-native-workstate/README.md) |
| Shared string receiver declarations | Complete rsplit-helper report allocation −21.26%; controlled migration elapsed −14.5–16.8% sync/−14.3–14.7% async | Two providers/per-access closures/packing removed; callbacks genuinely suspend. Final duplicate-arm cleanup allocation-checked, not VM-retimed; report control inconclusive. [R78](../2026-11-08-string-receivers/README.md) |
| Direct token storage / text, CSV | Allocation about−9.4% text/−8–9% CSV; controlled async text elapsed−2.3–3.3% | Sync text inconclusive; float async+0.9–2.7% accepted debt, full delivery qualified. [R76](../2026-11-08-inline-utf8/README.md) |
| Native function returns / CSV, JSON | Allocation −19.92%/−19.31% | Elapsed remains diagnostic. [Source pair](../2026-11-02-native-function-returns/README.md) |
| Direct UTF-8 formatting | Allocation −8.67% on original source; controlled elapsed/CPU −5–8% | Original adverse elapsed result remains unexplained. [Confirmation](../2026-11-02-formatting-confirmation/README.md) |
| Completed CSV records | Allocation −7.31%/−6.55%; native-helper warm elapsed −7.72–8.04% | Controls mixed; independently mutable rows remain funded. [R25](../2026-11-07-csv-record-values/README.md) |
| Canonical formatted instructions / dynamic report | Allocation about −15.35%; warm elapsed −10–13% | CSV +0.13–2.36% possible debt. [R35](../2026-11-07-canonical-format-instructions/README.md) |
| Shared receiver methods / regex summary | Allocation about −29.47%; warm elapsed −8.64–10.80% | CSV/text/JSON controls variable; original adverse CSV retained. [R41](../2026-11-07-receiver-methods/README.md) |
| Validated JSON floating conversion / numeric report | Allocation neutral; controlled warm elapsed −11.40–12.03% sync/−10.05–14.08% async | Integer/text controls near baseline; old inconclusive and adverse desktop retained; source33b6c191 fully qualified CI42431587731. [R74](../2026-11-08-json-float-entry/README.md) |
| General UTF-8 integer parsing | Padded CSV warm −12.5–13.4%; numeric-string JSON −41.4–42.0% | Explicit affected inputs; ordinary controls mixed. [R45](../2026-11-07-integer-parsing/README.md) |
| Native integer width / numeric-string JSON | Allocation about −12.5%; warm elapsed −10.7–12.5% | Padded CSV +1.0–1.2% accepted debt. [R50](../2026-11-07-native-integer-width/README.md) |
| Native UTF-8 wildcard reports | Allocation −25%/−32%; fnmatch warm −12–13%, glob −16–17% | Ordinary JSON control +4–7% accepted unresolved debt; no matching-backend claim. [R51](../2026-11-07-wildcard-translation/README.md) |
| Immutable tuple unpacking / returned-record CSV | Allocation −13.57%sync/−14.40%async; controlled warm elapsed −6.45–7.45%sync/−7.01–9.34%async | Ordinary controls near baseline; adverse desktop text preserved; full delivery qualified. [R70](../2026-11-07-tuple-unpack/README.md) |
| Linear keyword/variadic calls / returned-record CSV | Allocation −13.5% sync/−12.6% async; corrected controlled sync flat, async elapsed −1.21–6.31% | Original adverse CSV preserved; after transport correction CSV −1.74 to +0.43%, coarse CPU flat. Full correction delivery qualified. [R71](../2026-11-08-call-transport/README.md) |
| Intrinsic sequence construction / returned-record CSV | Further allocation −23.48% sync/−21.69% async; controlled elapsed −1.92–4.79% sync/−2.76–6.49% async | Ordinary CSV near baseline, text mixed/coarse CPU adverse; combined delivery qualified. [R72](../2026-11-08-sequence-construction/README.md) |
| Shared buffered-line execution | Duplicate algorithms/state machines removed; controlled async text elapsed −2.32–8.26%, JSON −1.46–3.96% | Allocation neutral; variation/quantized CPU, sync controls mixed. [R66](../2026-11-07-buffered-lines/README.md) |
| Frontend validity/module-fact pruning | Compile allocation −3.15–4.07% then −3.53–4.64% on separate source pairs | R62 controlled compile elapsed −2.94–5.88%; R65 elapsed inconclusive. [R62](../2026-11-07-syntax-validation/README.md), [R65](../2026-11-07-module-class-facts/README.md) |
| Executable graph construction | Compile allocation −3.9–5.9%; controlled compilation elapsed −1.7–3.0% | Warm control inconclusive; process CPU mixed. [R58](../2026-11-07-executable-graph/README.md) |

[Prepared member/iterator/branch operands](../2026-11-08-prepared-consumers/README.md)
also remove emitted load dispatch with neutral allocation. Controlled text elapsed
is modestly lower; coarse CPU is mixed, report timing inconclusive, and possible
CSV elapsed regression up to1.50% remains explicit. This is implementation
consolidation, not demonstrated substantial CPU improvement.

[Canonical receiver declarations](../2026-11-08-receiver-contract/README.md)
remove separate callable forms and a special opcode. Complete casefold/title helper
reports allocate45–52%less, with controlled elapsed22–24%lower. Rsplit control is
inconclusive; possible CSV regression up to2.70%remains explicit. This source pair
does not update the named-source CPython tables above.

Shared compiler-fact correction, annotation demand pruning and smaller executable
transport are also retained; their compilation/source benefits do not prove broad
warm CPU gains. [Earlier source pairs](../2026-11-07-retained-checkpoint/earlier-source-pairs.md)
preserve detailed revisions, regressions and completed qualification. The original
failed B406 refresh and its partial data remain beside the corrected checkpoint;
it was not silently discarded or promoted.

[Experimental history](../../EXPERIMENTS.md) retains rejected designs,
adverse timings and allocation evidence. [Benchmark tooling](../../README.md)
describes qualification procedures; [tests](../../../tests/README.md) cover semantics,
ownership, cancellation and installed consumers. Timing does not prove containment.
