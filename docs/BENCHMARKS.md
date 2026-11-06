# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 4.446–4.451 ms | 3.403 ms | 1.31× | Diagnostic; control / IQR 0.12% / 0.55% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.220–2.324 ms | 1.267 ms | 1.75–1.83× | Variable; control / IQR 4.44% / 0.67% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 4.750–4.765 ms | 4.238 ms | 1.12× | Diagnostic; control / IQR 0.31% / 0.68% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 3.929–3.963 ms | 2.720 ms | 1.44–1.46× | Variable; control / IQR 0.86% / 3.49% |
| Configuration read/edit/write | 16 paths / 181 B | 149.097–149.807 µs | 122.946 µs | 1.21–1.22× | Diagnostic; control / IQR 0.47% / 2.32% |

**2026-11-06; 0.11 development runtime source `b6650f09`; CPython 3.13.16; .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete validation and prior-result cleanup. Startup and
fixture staging/reset are excluded. **Ratio=Lython elapsed/CPython elapsed; above
one means slower.**

Ranges are medians from two independent identical-build processes, not confidence
intervals. Control is their absolute difference relative to candidate; IQR is the
larger Lython-role interquartile range/median. Text fresh and report remain variable.
These measurements describe the recorded source, not future HEAD. Frozen core
`54b53c7f` matches the explicitly packed/installed Release package; this runtime
also passed Windows/Ubuntu, both runtimes and original Stress. Private development
regex adapters are outside the comparison.
[Identity, observations and qualification](../benchmarks/results/2026-11-06-delivery-cpython/README.md).

## Warm execution, compilation and startup

Same source/input and execution/I/O/validation/cleanup boundary, reusing compiled code:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4.354–4.512 ms | 3.244 ms | 1.34–1.39× | 3.50% / 1.15% |
| Text summary/helpers | 2.143–2.164 ms | 1.188 ms | 1.80–1.82× | 0.95% / 0.30% |
| JSON-lines/helpers | 4.816–4.905 ms | 4.097 ms | 1.18–1.20× | 1.81% / 0.85% |
| JSON-to-Markdown report | 3.769–3.808 ms | 2.375 ms | 1.59–1.60× | 1.02% / 4.77% |
| Configuration read/edit/write | 77.968–79.123 µs | 60.257 µs | 1.29–1.31× | 1.48% / 1.45% |

Compilation/program disposal alone; execution and I/O excluded. Do not subtract
warm and fresh medians to infer compilation: these are independent job blocks,
and JSON fresh is slightly below warm in this collection.

| Compile-only script | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 71.450–72.030 µs | 59.191 µs | 1.21–1.22× | 0.81% / 0.69% |
| Text summary/helpers | 53.554–54.654 µs | 41.573 µs | 1.29–1.31× | 2.05% / 1.26% |
| JSON-lines/helpers | 64.138–64.837 µs | 50.743 µs | 1.26–1.28× | 1.09% / 1.93% |
| JSON-to-Markdown report | 51.439–53.401 µs | 37.618 µs | 1.37–1.42× | 3.81% / 7.52% |
| Configuration read/edit/write | 44.483–45.147 µs | 34.341 µs | 1.30–1.31× | 1.47% / 0.94% |

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** This refresh measures the combined delivery and does not isolate each
change's CPU benefit. Earlier source-pair regressions and unstable controls remain
in their evidence; later absolute timings do not explain them.
[Previous primary comparison, source `9a152ee0`](../benchmarks/results/2026-11-06-deferred-contexts/README.md),
[text-provenance source pair and accepted debt](../benchmarks/results/2026-11-06-text-operation-provenance/README.md).

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB RAM/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal worker GC/tiering,
quiet endpoints; five-second warmup per role/job and seven rotated150ms rounds.
Fifteen jobs completed311.43s; all workers joined and fixtures were removed.

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
| Frame resumption / text helpers | Warm elapsed −3.00–5.59%; fresh −0.79% to +0.02% versus f2ce53db | Allocation flat. Unchanged compilation control −1.47–2.23% limits precision; no general CPU or CPython-baseline claim. [R21](../benchmarks/results/2026-11-06-frame-resume-boundaries/README.md) |
| Shared record keys / JSON and report | JSON warm elapsed −10.89–12.53%; report warm −5.36–8.80%, fresh −8.53–10.52% versus b542b601 | Separate complete-job allocation −14.98–14.99% JSON, −9.43% report. Text confirmation −1.21% to +5.53%; possible regression accepted as debt. CSV coarse CPU adverse. Source pair core67f84196; primary five-script baseline above remains b6650f09. [R20](../benchmarks/results/2026-11-06-record-layout/README.md) |
| Caller-owned regex captures / extraction | Warm elapsed −3.62–5.17%; fresh −4.58–6.40% versus b6650f09 | Same optional 0.4 producer; control −2.13% to +0.10%. Direct cursor allocation −20.34% is a separate measurement. Regex remains about 7× CPython. [U04](../benchmarks/results/2026-11-06-regex-capture-ownership/README.md) |
| Deferred helper contexts / CSV | Warm elapsed −5.85–6.24%; fresh −5.53–5.83% versus R10 | Warm CPU flat; warm text adverse, JSON/report mixed; exact allocation unmeasured. [R13](../benchmarks/results/2026-11-06-deferred-contexts/README.md) |
| Contextual key preparation / compound-key aggregation | Elapsed −15.5%/−20.5% versus F05 | CSV roughly flat; text uncertainty. [Q01](../benchmarks/results/2026-11-06-key-preparation/README.md) |
| Active structural path / compound-key aggregation | Elapsed −14.8–17.1% versus Q01 | CSV roughly flat; text control-limited. [R02](../benchmarks/results/2026-11-06-structural-active-path/README.md) |
| Final tuple backing / compound-key aggregation | Elapsed −6.17–12.29% versus R02 | CSV inconclusive; no process allocation percentage. [R04](../benchmarks/results/2026-11-06-executable-tuple-backing/README.md) |
| Ordinary method operands / method-heavy CSV | Allocation −7.26–7.28%; warm elapsed −3.67–4.61% | Separate clock boundaries; controls also improve. [F05](../benchmarks/results/2026-11-06-direct-method-operands/README.md) |

Native helper returns reduced process allocation19.92%/19.31%; elapsed remains
diagnostic. Direct UTF-8 formatting reduced allocation8.67%; controlled elapsed/CPU
supports retention, while the original adverse result remains unexplained.
[Full experimental evidence](../benchmarks/EXPERIMENTS.md) preserves earlier
source pairs, rejected designs and uncertainty. Substantial whole-script gaps
remain; the primary warm-text regression above is explicit debt.

## Historical measurements and unresolved workloads

The following were not refreshed to `9a152ee0` and do not describe its performance:

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
