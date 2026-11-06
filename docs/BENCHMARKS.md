# Performance against CPython

| Workload | Input | Lython | CPython | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4,096 records / 57,361 B | 4.637–4.651 ms | 3.376 ms | 1.37–1.38× | Diagnostic; control / IQR 0.31% / 0.54% |
| Text summary/helpers | 3,072 lines / 35,840 B | 2.441–2.507 ms | 1.295 ms | 1.88–1.94× | Mixed; control / IQR 2.72% / 0.63% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 5.024–5.220 ms | 4.204 ms | 1.20–1.24× | Variable; control / IQR 3.90% / 0.86% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 4.008–4.132 ms | 2.736 ms | 1.46–1.51× | Variable; control / IQR 3.09% / 2.63% |

**2026-11-06; 0.11 development runtime source `9a152ee0`; CPython 3.13.16; .NET 11 RC3.**
These are **compilation plus one execution in an already-running host**, including
mediated RAM-file I/O, complete validation and prior-result cleanup. Startup and
fixture staging/reset are excluded. **Ratio=Lython elapsed/CPython elapsed; above
one means slower.**

Ranges are medians from two independent identical-build processes, not confidence
intervals. Control is their absolute difference relative to candidate; IQR is the
larger Lython-role interquartile range/median. Report remains variable.
These results describe the recorded source, not future HEAD. The frozen worker
loaded core `1daf56c3`, built from the archived source patch subsequently committed
as `9a152ee0`. Full delivery qualification remains separate from the measurement.
[Identity](../benchmarks/results/2026-11-06-deferred-contexts/identity.json),
[original adverse results and qualification scope](../benchmarks/results/2026-11-06-deferred-contexts/README.md).

## Warm execution, compilation and startup

Same source/input and execution/I/O/validation/cleanup boundary, reusing compiled code:

| Warm workload | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 4.436–4.455 ms | 3.183 ms | 1.39–1.40× | 0.42% / 0.99% |
| Text summary/helpers | 2.370–2.440 ms | 1.171 ms | 2.02–2.08× | 2.94% / 0.61% |
| JSON-lines/helpers | 4.898–5.003 ms | 4.114 ms | 1.19–1.22× | 2.09% / 0.81% |
| JSON-to-Markdown report | 3.890–3.947 ms | 2.600 ms | 1.50–1.52× | 1.45% / 3.34% |

Compilation/program disposal alone; execution and I/O excluded. Do not subtract
warm and fresh medians to infer compilation:

| Compile-only script | Lython | CPython | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| CSV summary/ordinary helper | 72.520–74.122 µs | 59.347 µs | 1.22–1.25× | 2.16% / 2.53% |
| Text summary/helpers | 53.549–54.756 µs | 41.336 µs | 1.30–1.32× | 2.20% / 2.76% |
| JSON-lines/helpers | 64.105–64.757 µs | 51.006 µs | 1.26–1.27× | 1.02% / 6.66% |
| JSON-to-Markdown report | 51.267–51.567 µs | 37.852 µs | 1.35–1.36× | 0.58% / 2.85% |

**Startup, genuinely suspended-I/O latency, heap/RSS and embedding latency remain
unmeasured.** Against qualified R10, CSV elapsed improves5.5–6.2% in both roles;
warm CSV process CPU is flat. Warm text is3.4–6.5% slower, JSON mixed/adverse and
report variable. Identical-build differences still limit smaller conclusions.
These results do not establish a broad CPU improvement or an exact allocation
reduction. The earlier prototype's adverse/unstable observations remain visible.
[Paired observations and debt](../benchmarks/results/2026-11-06-deferred-contexts/README.md).

## Machine, included work and fairness

Dedicated Azure Standard_F4as_v7, Ubuntu24.04.4 x86-64, four AMD EPYC9V45 vCPUs,
one thread/core,16GiB RAM/no swap. CPython GCC13.3 PGO/LTO, ordinary GIL/GC,
no JIT/debug, `-I -S -X utf8`. SDK/runtime11.0.100-rc.3.26522.148 /
11.0.0-rc.3.26522.148. Independent sequential processes, normal worker GC/tiering,
quiet endpoints; five-second warmup per role/job and seven rotated150ms rounds.
Twelve jobs completed328.10s; all workers joined and fixtures were removed.

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
