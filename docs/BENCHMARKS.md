# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 3.946–3.969 ms | 3.439 ms | 1.15–1.15× | Variable; 0.58% / 3.52% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.074–2.115 ms | 1.277 ms | 1.62–1.66× | Diagnostic; 1.94% / 1.12% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.063–4.165 ms | 4.201 ms | 0.97–0.99× | Variable; 2.50% / 2.13% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.520–3.619 ms | 2.498 ms | 1.41–1.45× | Variable; 2.81% / 4.61% |
| Configuration read/edit/write | 16 paths / 181 B | 151.620–152.538 µs | 123.683 µs | 1.23–1.23× | Variable; 0.60% / 5.93% |
| Protected file search | 2,049 lines; exits after 1,025 / 42,010 B | 302.017–302.135 µs | 212.110 µs | 1.42–1.42× | Diagnostic; 0.04% / 0.89% |
| Two-file copy/checksum/manifest | 2 files / 4,107 B | 403.168–406.873 µs | 265.101 µs | 1.52–1.53× | Diagnostic; 0.92% / 1.09% |

**2026-11-07; 0.11 development source `55e510ff`; CPython 3.13.16; .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete validation and prior-result cleanup. Startup and
fixture staging/reset are excluded. **Ratio = Lython elapsed / CPython elapsed;
above one means slower.** Ranges span two independent identical-build process
medians, not confidence intervals. Control is their absolute difference/candidate;
IQR is the larger Lython-role interquartile range/median. Diagnostic/Variable label
short observations, not design acceptance thresholds. Inconclusive cases stay visible.

Frozen core `83af401e` matches the recorded 561-file production inventory for this
commit; informational SourceLink predates the commit. Qualification status is
recorded with the [source and evidence](../benchmarks/results/2026-11-07-retained-checkpoint/README.md).
The normal optional 0.4 adapter and worker are unchanged; this is not new producer
certification. Earlier tables measured `8c9fd0d7`, preserved with their
[historical observations](../benchmarks/results/2026-11-07-integer-cpython/README.md).

## Warm execution, compilation and startup

Warm execution reuses the compiled script. The last two inputs explicitly exercise
general integer conversion and do not replace ordinary CSV/JSON. Their compilation
and compile-to-result times were not refreshed in this checkpoint.

| Workload | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 3.841–3.858 ms | 3.235 ms | 1.19–1.19× | Diagnostic; 0.43% / 0.70% |
| Text summary/helpers | 1.902–1.926 ms | 1.181 ms | 1.61–1.63× | Diagnostic; 1.21% / 0.40% |
| JSON-lines/helpers | 3.989–4.139 ms | 4.079 ms | 0.98–1.01× | Variable; 3.60% / 3.09% |
| JSON-to-Markdown report | 3.335–3.401 ms | 2.337 ms | 1.43–1.46× | Diagnostic; 1.94% / 1.93% |
| Configuration read/edit/write | 80.005–80.308 µs | 62.389 µs | 1.28–1.29× | Diagnostic; 0.38% / 2.09% |
| Protected file search | 219.799–222.813 µs | 137.193 µs | 1.60–1.62× | Diagnostic; 1.37% / 1.71% |
| Two-file copy/checksum/manifest | 244.071–246.505 µs | 154.254 µs | 1.58–1.60× | Diagnostic; 0.99% / 1.88% |
| CSV with padded numeric fields | 4.293–4.303 ms | 4.193 ms | 1.02–1.03× | Diagnostic; 0.24% / 0.65% |
| JSON with numeric strings | 6.712–6.739 ms | 4.587 ms | 1.46–1.47× | Diagnostic; 0.40% / 0.56% |

Compilation alone includes construction and disposal under the shared allowance:

| Workload | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 70.379–70.632 µs | 59.403 µs | 1.18–1.19× | Diagnostic; 0.36% / 0.88% |
| Text summary/helpers | 50.202–51.604 µs | 41.869 µs | 1.20–1.23× | Variable; 2.72% / 1.53% |
| JSON-lines/helpers | 61.223–61.445 µs | 50.749 µs | 1.21–1.21× | Diagnostic; 0.36% / 2.31% |
| JSON-to-Markdown report | 46.940–47.016 µs | 37.748 µs | 1.24–1.25× | Diagnostic; 0.16% / 2.43% |
| Configuration read/edit/write | 42.831–43.974 µs | 34.257 µs | 1.25–1.28× | Variable; 2.67% / 1.46% |
| Protected file search | 32.941–33.488 µs | 31.095 µs | 1.06–1.08× | Diagnostic; 1.63% / 2.22% |
| Two-file copy/checksum/manifest | 79.273–80.151 µs | 56.647 µs | 1.40–1.41× | Diagnostic; 1.10% / 2.70% |

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
The 23-job CPython checkpoint completed in 477.00 seconds.
Wildcard source-pair jobs are reported separately. Every worker joined and fixture
was removed; persistent archives were verified under the collection lease.

Both engines run identical scripts/fixtures and check complete stdout/file digests
on every invocation. Efficient host overrides mediate Lython file access. RAM
fixtures include guest reads/writes; staging/reset are outside elapsed clocks.
Warm includes invocation and prior-result cleanup; compile includes construction/
disposal. Protocol transport and final cached teardown are excluded. CPython
retains imported modules; Lython isolates execution imports. This is an embedding
comparison. Lython's timed file opens include host capability/path-component
validation; CPython uses ordinary builtin open. A later source-7df9b465
[configuration CPU profile](../benchmarks/results/2026-11-07-invocation-setup/README.md)
finds substantial opening/path work. It does not isolate wall I/O latency or
attribute the whole gap; these tables still describe source55e510ff.
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
| General UTF-8 integer parsing | Padded CSV warm −12.5–13.4%; numeric-string JSON −41.4–42.0% | Explicit affected inputs; ordinary controls mixed. [R45](../benchmarks/results/2026-11-07-integer-parsing/README.md) |
| Native integer width / numeric-string JSON | Allocation about −12.5%; warm elapsed −10.7–12.5% | Padded CSV +1.0–1.2% accepted debt. [R50](../benchmarks/results/2026-11-07-native-integer-width/README.md) |
| Native UTF-8 wildcard reports | Allocation −25%/−32%; fnmatch warm −12–13%, glob −16–17% | Ordinary JSON control +4–7% accepted unresolved debt; no matching-backend claim. [R51](../benchmarks/results/2026-11-07-wildcard-translation/README.md) |

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
