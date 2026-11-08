# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV validation/returned records† | 5,120 records / 68,625 B | 5.913–6.073 ms | 4.527 ms | 1.31–1.34× | Variable; 2.70% / 1.08% |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.947–3.954 ms | 3.397 ms | 1.16–1.16× | Diagnostic; 0.16% / 1.65% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.020–2.080 ms | 1.269 ms | 1.59–1.64× | Variable; 2.97% / 0.58% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.033–4.146 ms | 4.217 ms | 0.96–0.98× | Variable; 2.82% / 0.78% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.414–3.534 ms | 2.488 ms | 1.37–1.42× | Variable; 3.40% / 3.27% |
| Configuration read/edit/write | 16 paths / 181 B | 142.828–144.327 µs | 125.968 µs | 1.13–1.15× | Diagnostic; 1.04% / 2.43% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 297.894–299.911 µs | 211.699 µs | 1.41–1.42× | Diagnostic; 0.67% / 0.92% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 389.574–390.282 µs | 264.878 µs | 1.47–1.47× | Diagnostic; 0.18% / 1.79% |

**† rows: 2026-11-08, source `68d5bffe`; other rows: 2026-11-07, source
`824fc6bb`. CPython 3.13.16; .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete validation and prior-result cleanup. Startup and
fixture staging/reset are excluded. **Ratio = Lython elapsed / CPython elapsed;
above one means slower.** Ranges span two independent identical-build process
medians, not confidence intervals. Control is their absolute difference/candidate;
IQR is the largest role interquartile range/median, including CPython. Diagnostic/Variable label
short observations, not design acceptance thresholds. Inconclusive cases stay visible.

New † rows use frozen core `a0aef9ef`, matching source `68d5bffe` (informational
SourceLink `a34f56b5`). Full Windows/Ubuntu Release, actual .NET 11/10, original Stress and installed
core consumers pass CI42428096630 ([cached receipt](../benchmarks/results/2026-11-08-sequence-construction/delivery-ci.json)). The
[five-job targeted collection](../benchmarks/results/2026-11-08-returned-record-cpython/README.md)
fills the returned-record baseline and refreshes two warm controls; it does not
remeasure every older workload or explain previous adverse source pairs.

Frozen core `15c04e2a` matches the recorded 561-file production inventory for
source `824fc6bb`; informational SourceLink is precommit `5dd131bf`.
The measured source passes full Windows/Ubuntu Release, actual .NET 11/10,
original Stress and installed-package checks (CI42418518578). The [source and evidence](../benchmarks/results/2026-11-07-buffered-checkpoint/README.md)
record the eventual qualification status separately from this measured identity.
The normal optional 0.4 adapter and worker are unchanged; this is not new producer
certification. [Earlier source55e510ff tables](../benchmarks/results/2026-11-07-buffered-checkpoint/prior-BENCHMARKS.md)
and their qualification remain historical. Variable means process spread or any
role's IQR exceeded the descriptive limits; no inconclusive row is removed.

## Warm execution, compilation and startup

Warm execution reuses the compiled script. The last two inputs explicitly exercise
general integer conversion and do not replace ordinary CSV/JSON. Their compilation
and compile-to-result times were not refreshed in this checkpoint.

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records† | 5.681–5.817 ms | 4.320 ms | 1.32–1.35× | Variable; 2.32% / 1.07% |
| CSV summary/ordinary helper† | 3.737–3.817 ms | 3.209 ms | 1.16–1.19× | Variable; 2.14% / 1.21% |
| Text summary/helpers† | 1.939–1.950 ms | 1.163 ms | 1.67–1.68× | Diagnostic; 0.60% / 1.49% |
| JSON-lines/helpers | 3.979–4.059 ms | 4.168 ms | 0.95–0.97× | Variable; 2.02% / 0.70% |
| JSON-to-Markdown report | 3.368–3.406 ms | 2.348 ms | 1.43–1.45× | Variable; 1.10% / 4.73% |
| Configuration read/edit/write | 79.588–79.947 µs | 62.105 µs | 1.28–1.29× | Diagnostic; 0.45% / 1.97% |
| Protected file search | 224.949–226.076 µs | 136.362 µs | 1.65–1.66× | Diagnostic; 0.50% / 0.97% |
| Two-file copy/checksum/manifest | 244.581–246.603 µs | 155.827 µs | 1.57–1.58× | Diagnostic; 0.83% / 2.02% |
| CSV with padded numeric fields | 4.313–4.451 ms | 4.208 ms | 1.03–1.06× | Variable; 3.19% / 0.68% |
| JSON with numeric strings | 6.706–7.103 ms | 4.559 ms | 1.47–1.56× | Variable; 5.59% / 0.91% |

Compilation alone includes construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/returned records† | 70.400–72.059 µs | 65.559 µs | 1.07–1.10× | Variable; 2.36% / 4.59% |
| CSV summary/ordinary helper | 60.394–60.707 µs | 59.285 µs | 1.02–1.02× | Diagnostic; 0.52% / 1.35% |
| Text summary/helpers | 44.547–44.735 µs | 41.418 µs | 1.08–1.08× | Diagnostic; 0.42% / 1.18% |
| JSON-lines/helpers | 51.581–52.488 µs | 50.796 µs | 1.02–1.03× | Diagnostic; 1.73% / 1.12% |
| JSON-to-Markdown report | 42.122–42.167 µs | 37.595 µs | 1.12–1.12× | Diagnostic; 0.11% / 1.86% |
| Configuration read/edit/write | 38.406–38.704 µs | 34.402 µs | 1.12–1.13× | Variable; 0.77% / 5.13% |
| Protected file search | 28.917–29.253 µs | 30.755 µs | 0.94–0.95× | Diagnostic; 1.15% / 1.40% |
| Two-file copy/checksum/manifest | 72.811–73.113 µs | 56.337 µs | 1.29–1.30× | Diagnostic; 0.42% / 1.66% |

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** A combined checkpoint does not isolate each change's CPU benefit,
repay historical regression debt or explain earlier adverse controls. It describes
the recorded source, not future HEAD. Search exits after 1,025 of 2,049 lines; the
manifest copies only two files/4,107 B. These are not large-file throughput tests.

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 x86-64, four AMD EPYC 9V45 vCPUs,
one thread/core, 16 GiB/no swap. CPython GCC 13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime 11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal GC/tiering,
quiet endpoints; five-second warmup per role/job, seven rotated 150 ms rounds.
The source `824fc6bb` 23-job checkpoint completed in 478.32 seconds; the five-job
source `68d5bffe` update took 107.44 seconds. Returned-record input includes skip
and negative rows; all 5,120 are parsed and 3,072 accepted.
Buffered-line source-pair jobs are reported separately. Every worker joined and fixture
was removed; persistent archives were verified under the collection lease.

Both engines run identical scripts/fixtures and check complete stdout/file digests
on every invocation. Efficient host overrides mediate Lython file access. RAM
fixtures include guest reads/writes; staging/reset are outside elapsed clocks.
Warm includes invocation and prior-result cleanup; compile includes construction/
disposal. Protocol transport and final cached teardown are excluded. CPython
retains imported modules; Lython isolates execution imports. This is an embedding
comparison. Lython's timed file opens include host capability/path-component
validation; CPython uses ordinary builtin open. A source-7df9b465
[configuration CPU profile](../benchmarks/results/2026-11-07-invocation-setup/README.md)
finds substantial opening/path work. It does not isolate wall I/O latency or
attribute the whole gap; table revisions are marked above; they do not all describe HEAD.
Tables use synchronous Run; historical async observations use completed
mediated I/O and do not establish pending-I/O latency.

## Improvements within Lython

CPython is the primary external baseline above. These separate source pairs
measure affected workloads; percentages are not additive or universal CPU gains.

| Change / affected work | Established result | Limits / evidence |
| --- | --- | --- |
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
