# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records /57,361B | 4.127–4.233 ms | 3.419 ms | 1.21–1.24× | Variable; 2.57% / 2.44% |
| Text summary/helpers | 3,072 lines /35,840B | 2.068–2.117 ms | 1.275 ms | 1.62–1.66× | Variable; 2.35% / 0.89% |
| JSON-lines/helpers | 4,096 records /110,592B | 4.366–4.485 ms | 4.199 ms | 1.04–1.07× | Variable; 2.66% / 1.00% |
| JSON-to-Markdown report | 3,072 records /189,441B | 3.733–3.784 ms | 2.758 ms | 1.35–1.37× | Variable; 1.33% / 3.81% |
| Configuration read/edit/write | 16 paths /181B | 147.486–149.834 µs | 122.524 µs | 1.20–1.22× | Diagnostic; 1.59% / 2.65% |

**2026-11-07; 0.11 development source `81036f25`; CPython3.13.16; .NET11RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete validation and prior-result cleanup. Startup and
fixture staging/reset are excluded. **Ratio=Lython elapsed/CPython elapsed; above
one means slower.**

Ranges are medians from two independent identical-build processes, not confidence
intervals. Control is their absolute difference relative to candidate; IQR is the
larger Lython-role interquartile range/median. Several cases remain variable: keep
their status/control columns. These timings describe the recorded source, not
future HEAD. Frozen core `32dcf141` has that exact production source, including
R26/R29. Source81036f25 passed Windows/Ubuntu, actual.NET11/10, original Stress
and installed-package consumers. Subsequent 0.11 changes and private development
regex adapters are outside this comparison.
[Identity, observations and qualification](../benchmarks/results/2026-11-07-completed-call-cpython/README.md).

## Warm execution, compilation and startup

Same source/input and execution/I/O/validation/cleanup boundary, reusing compiled code:

| Warm workload | Lython | CPython | Ratio | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 3.930–3.935 ms | 3.221 ms | 1.22–1.22× | Diagnostic; 0.13% / 1.08% |
| Text summary/helpers | 2.027–2.078 ms | 1.185 ms | 1.71–1.75× | Variable; 2.50% / 0.69% |
| JSON-lines/helpers | 4.274–4.298 ms | 4.069 ms | 1.05–1.06× | Diagnostic; 0.56% / 0.64% |
| JSON-to-Markdown report | 3.606–3.722 ms | 2.620 ms | 1.38–1.42× | Variable; 3.21% / 3.12% |
| Configuration read/edit/write | 75.538–75.894 µs | 61.068 µs | 1.24–1.24× | Diagnostic; 0.47% / 2.23% |

Compilation/program disposal alone; execution and I/O excluded. Do not subtract
warm and fresh medians to infer compilation: these are independent job blocks.

| Compile-only script | Lython | CPython | Ratio | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 72.269–72.296 µs | 59.534 µs | 1.21–1.21× | Diagnostic; 0.04% / 1.19% |
| Text summary/helpers | 53.627–54.169 µs | 41.586 µs | 1.29–1.30× | Diagnostic; 1.00% / 0.90% |
| JSON-lines/helpers | 63.958–64.085 µs | 51.278 µs | 1.25–1.25× | Diagnostic; 0.20% / 2.36% |
| JSON-to-Markdown report | 50.555–50.933 µs | 37.770 µs | 1.34–1.35× | Variable; 0.74% / 3.23% |
| Configuration read/edit/write | 44.372–44.615 µs | 34.571 µs | 1.28–1.29× | Diagnostic; 0.55% / 1.36% |

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** This refresh measures combined changes; it does not isolate each
change's CPU benefit or explain earlier adverse/unstable source-pair results.
[Previous primary comparison, source01aedc3b](../benchmarks/results/2026-11-07-current-cpython/README.md).

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB RAM/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal worker GC/tiering,
quiet endpoints; five-second warmup per role/job and seven rotated150ms rounds.
Fifteen jobs completed312.37s; all workers joined and fixtures were removed.

Both engines run identical scripts/fixtures, checking complete output/file digests
on every invocation. Efficient host overrides mediate Lython file access. RAM
fixtures include reads/writes; staging/reset is outside elapsed clocks. Warm
includes invocation and prior-result cleanup. Compile includes construction/
disposal; compile-to-result also executes a fresh script. Transport and final
program/result/process/fixture teardown are outside clocks. CPython retains imported
modules between invocations; Lython isolates execution imports. This is an embedding
comparison, not a claim of identical implementations or import caching.

These primary tables use `Run`. Historical `4ace68ff` `RunAsync` jobs use completed
mediated I/O: text2.630–2.636ms versus CPython1.203ms (2.19×), CSV5.586–5.670ms
versus3.220ms (1.74–1.76×). Async text elapsed is6.91–7.14% below Q02;
CSV is inconclusive. These do not measure genuinely pending-I/O latency.
[Historical async evidence](../benchmarks/results/2026-11-06-native-read-cpython/README.md),
[earlier async hash/read debt](../benchmarks/results/2026-11-06-async-key-reads/README.md)
and [original native-read comparison](../benchmarks/results/2026-11-06-native-read-prefix/README.md)
retain their distinct source pairs and adverse results.

## Improvements within Lython

CPython is the primary external baseline above. These separate source pairs
measure affected workloads; percentages are not additive or broad CPU claims:

| Change / affected workload | Measured result | Limits / evidence |
| --- | --- | --- |
| Direct formatted instructions / dynamic-width report | Warm elapsed −10.09–13.19% versus2f1f2715; about15.35% less allocation on both APIs | Labelled complete-script variant; static warm favorable, fresh/text variable. CSV+0.13–2.36% accepted possible debt. Full delivery pending; no broad CPU claim. [R35](../benchmarks/results/2026-11-07-canonical-format-instructions/README.md) |
| Completed native helper calls / text | Warm elapsed −4.25–7.61% sync / −7.63–8.64% async versus c4729a9f | Allocation/peaks flat. Report variable; CSV up to+3.04% against differing baseline processes. Coarse CPU is diagnostic. [R29](../benchmarks/results/2026-11-07-execution-transport/implementation.md) |
| Completed CSV record values | Warm elapsed −7.72–8.04% native helper / −4.13–4.78% ordinary helper versusec134e8c | Native CSV allocation −7.31/−6.55% sync/async. Text control spread2.52%; coarse CPU mixed. [R25](../benchmarks/results/2026-11-07-csv-record-values/README.md) |
| Validated regex programs / extraction | Warm elapsed −8.34–9.64%; compile-to-result −6.47–6.53% versus41c25958 | Programs remain charged until script disposal; larger compilation footprint. Text control spread2.78%, unchanged compilation also moves. [R23](../benchmarks/results/2026-11-06-regex-program-reuse/README.md) |
| Fixed builtin initialization / configuration | Warm elapsed −1.13–2.44%; fresh −1.43% to +0.13% versus4fad63c5 | Removes2,688B of reflected factory allocation per invocation. Compilation control −1.20–1.63%; text role spread4.10%. No broad CPU gain. [R22](../benchmarks/results/2026-11-06-builtin-initialization/README.md) |
| Frame resumption / text helpers | Warm elapsed −3.00–5.59%; fresh −0.79% to +0.02% versus f2ce53db | Allocation flat. Unchanged compilation control −1.47–2.23% limits precision; no general CPU or CPython-baseline claim. [R21](../benchmarks/results/2026-11-06-frame-resume-boundaries/README.md) |
| Shared record keys / JSON and report | JSON warm elapsed −10.89–12.53%; report warm −5.36–8.80%, fresh −8.53–10.52% versus b542b601 | Separate complete-job allocation −14.98–14.99% JSON, −9.43% report. Text confirmation −1.21% to +5.53%; possible regression accepted as debt. CSV coarse CPU adverse. Source pair core67f84196; later primary comparison above includes subsequent changes. [R20](../benchmarks/results/2026-11-06-record-layout/README.md) |
| Caller-owned regex captures / extraction | Warm elapsed −3.62–5.17%; fresh −4.58–6.40% versus b6650f09 | Same optional 0.4 producer; control −2.13% to +0.10%. Direct cursor allocation −20.34% is a separate measurement. Regex remains about 7× CPython. [U04](../benchmarks/results/2026-11-06-regex-capture-ownership/README.md) |
| Deferred helper contexts / CSV | Warm elapsed −5.85–6.24%; fresh −5.53–5.83% versus R10 | Warm CPU flat; warm text adverse, JSON/report mixed; exact allocation unmeasured. [R13](../benchmarks/results/2026-11-06-deferred-contexts/README.md) |
| Contextual key preparation / compound-key aggregation | Elapsed −15.5%/−20.5% versus F05 | CSV roughly flat; text uncertainty. [Q01](../benchmarks/results/2026-11-06-key-preparation/README.md) |
| Active structural path / compound-key aggregation | Elapsed −14.8–17.1% versus Q01 | CSV roughly flat; text control-limited. [R02](../benchmarks/results/2026-11-06-structural-active-path/README.md) |
| Final tuple backing / compound-key aggregation | Elapsed −6.17–12.29% versus R02 | CSV inconclusive; no process allocation percentage. [R04](../benchmarks/results/2026-11-06-executable-tuple-backing/README.md) |
| Ordinary method operands / method-heavy CSV | Allocation −7.26–7.28%; warm elapsed −3.67–4.61% | Separate clock boundaries; controls also improve. [F05](../benchmarks/results/2026-11-06-direct-method-operands/README.md) |

Native helper returns reduced process allocation19.92%/19.31%; elapsed remains
diagnostic. Direct UTF-8 formatting reduced allocation8.67%; controlled elapsed/CPU
supports retention, while the original adverse result remains unexplained.
[Subsequent canonical formatting](../benchmarks/results/2026-11-07-canonical-formatting/README.md)
fixes a stale type proof and removes duplicate execution with allocation flat;
its async report and text elapsed results are adverse/variable, with no CPU gain
claimed. It and R29 are included in the primary checkpoint above; individual source-pair limits remain.
[Full experimental evidence](../benchmarks/EXPERIMENTS.md) preserves earlier
source pairs, rejected designs and uncertainty. Substantial whole-script gaps
remain; possible earlier warm-text regression remains explicit debt.

## Historical measurements and unresolved workloads

The following were not refreshed to `81036f25` and do not describe its performance:

| Workload | Earlier revision/boundary | Lython / CPython | Status |
| --- | --- | ---: | --- |
| CSV/native helpers |13d69be6; RAM; compile-to-result|1.27–1.29×|Diagnostic|
| Config read/edit/write;1,024 paths |13d69be6; RAM; compile-to-result|1.03–1.04×|Variable|
| CSV long fields;16records/524,717B |13d69be6; RAM; compile-to-result|0.14×|Variable; does not describe ordinary CSV|
| Search with early exit;42,010B |bd3f9dee; ordinary filesystem|2.09×|Diagnostic|
| Unicode/ASCII filename filter;768names |bd3f9dee; ordinary filesystem|2.14×/2.70×|Variable/diagnostic; filters supplied names, not768files|
| Copy/checksum/manifest;4,107B |bd3f9dee; ordinary filesystem|1.69×|Diagnostic|

The [earlier construction checkpoint](../benchmarks/results/2026-11-06-construction-cpython/README.md)
retains794e552b and its original adverse conditions. The [previous call/frame checkpoint](../benchmarks/results/2026-11-06-call-frame-cpython/README.md)
retains the1134d6e8 results and qualification. The [earlier retained checkpoint](../benchmarks/results/2026-11-05-runtime-cpython-refresh/README.md)
keeps the previous four-script results, full data and closed qualification. Earlier
90785737 search remains inconclusive (fresh270%IQR/218% control drift, warm138%/77%):
[historical evidence](../benchmarks/HISTORY.md). Native process-varying hashes/buckets
can plausibly influence small-role differences; causation in these runs is unproven.

[Experimental history](../benchmarks/EXPERIMENTS.md) preserves rejected designs,
adverse timings and allocation evidence. [Benchmark tooling](../benchmarks/README.md)
describes existing qualification procedures; [tests](../tests/README.md) cover
semantic equivalence, ownership, cancellation and installed consumers. No timing
claim establishes containment or matching cancellation by itself.
