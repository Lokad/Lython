# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5,120 records / 68,625 B | 5.897–5.923 ms | 4.509 ms | 1.31–1.31× | Diagnostic; 0.45% / 0.56% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.920–3.982 ms | 3.404 ms | 1.15–1.17× | Diagnostic; 1.58% / 1.45% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.047–2.080 ms | 1.286 ms | 1.59–1.62× | Diagnostic; 1.61% / 2.91% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.232–4.265 ms | 4.122 ms | 1.03–1.03× | Diagnostic; 0.77% / 0.79% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.570–3.573 ms | 2.494 ms | 1.43–1.43× | Diagnostic; 0.09% / 2.79% |
| Configuration read/edit/write | 16 paths / 181 B | 143.233–144.301 µs | 123.564 µs | 1.16–1.17× | Diagnostic; 0.74% / 1.70% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 295.323–295.618 µs | 216.725 µs | 1.36–1.36× | Variable; 0.10% / 3.79% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 384.689–387.598 µs | 260.935 µs | 1.47–1.49× | Diagnostic; 0.75% / 2.44% |

**2026-11-08; measured source `38f53c33`, CPython 3.13.16, .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete output validation and prior-result cleanup.
Startup and fixture staging/reset are excluded. **Ratio = Lython elapsed /
CPython elapsed; above one means slower.** Ranges span two independent Lython
process medians, not confidence intervals. Control is their absolute difference
relative to the candidate. Max IQR includes CPython. Diagnostic/Variable describe
short observations, not design acceptance thresholds; inconclusive cases remain.

Frozen core `903eb346` matches the measured source's production inventory;
informational SourceLink is precommit `c384956f`. Windows/Ubuntu Release actual
.NET 11/10, original Stress and installed-package checks pass CI42439532114
([cached receipt](../benchmarks/results/2026-11-08-inline-utf8/delivery-ci.json)).
The [26-job checkpoint](../benchmarks/results/2026-11-08-inline-checkpoint/README.md)
records identities, original setup failures and verified raw evidence. The optional
0.4 adapter and workers are unchanged; this does not certify a newer producer.
[Earlier source824fc6bb/68d5bffe tables](../benchmarks/results/2026-11-08-inline-checkpoint/prior-BENCHMARKS.md)
remain historical. These tables describe the recorded source, not later HEAD.

## Warm execution, compilation and startup

Warm execution reuses the compiled script. The last two inputs explicitly exercise
general integer conversion and do not replace ordinary CSV/JSON. Their compilation
and compile-to-result times were not refreshed in this checkpoint.

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 5.752–5.757 ms | 4.331 ms | 1.33–1.33× | Diagnostic; 0.08% / 1.67% |
| CSV summary/ordinary helper | 3.800–3.886 ms | 3.237 ms | 1.17–1.20× | Variable; 2.20% / 1.17% |
| Text summary/helpers | 1.945–1.964 ms | 1.179 ms | 1.65–1.67× | Diagnostic; 0.99% / 1.28% |
| JSON-lines/helpers | 4.089–4.128 ms | 4.115 ms | 0.99–1.00× | Diagnostic; 0.94% / 1.11% |
| JSON-to-Markdown report | 3.399–3.462 ms | 2.354 ms | 1.44–1.47× | Variable; 1.84% / 3.52% |
| Configuration read/edit/write | 79.328–79.455 µs | 61.423 µs | 1.29–1.29× | Diagnostic; 0.16% / 1.26% |
| Protected file search | 223.808–226.434 µs | 136.179 µs | 1.64–1.66× | Diagnostic; 1.16% / 1.86% |
| Two-file copy/checksum/manifest | 242.160–244.303 µs | 156.085 µs | 1.55–1.57× | Diagnostic; 0.88% / 1.88% |
| CSV with padded numeric fields | 4.389–4.403 ms | 4.147 ms | 1.06–1.06× | Diagnostic; 0.31% / 1.07% |
| JSON with numeric strings | 6.769–6.815 ms | 4.558 ms | 1.48–1.50× | Diagnostic; 0.69% / 1.03% |

Compilation alone includes construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records | 70.493–72.482 µs | 65.550 µs | 1.08–1.11× | Variable; 2.74% / 1.98% |
| CSV summary/ordinary helper | 59.957–60.502 µs | 59.300 µs | 1.01–1.02× | Variable; 0.91% / 3.04% |
| Text summary/helpers | 44.599–44.966 µs | 40.991 µs | 1.09–1.10× | Diagnostic; 0.82% / 2.10% |
| JSON-lines/helpers | 52.098–52.320 µs | 50.767 µs | 1.03–1.03× | Diagnostic; 0.43% / 2.70% |
| JSON-to-Markdown report | 41.448–42.032 µs | 37.717 µs | 1.10–1.11× | Diagnostic; 1.41% / 1.19% |
| Configuration read/edit/write | 38.038–38.579 µs | 34.179 µs | 1.11–1.13× | Diagnostic; 1.42% / 1.20% |
| Protected file search | 29.183–29.315 µs | 30.776 µs | 0.95–0.95× | Diagnostic; 0.45% / 1.87% |
| Two-file copy/checksum/manifest | 71.134–71.655 µs | 56.592 µs | 1.26–1.27× | Variable; 0.73% / 3.94% |

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
All26 jobs completed in539.66seconds. Returned-record CSV parses5,120 rows and
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
